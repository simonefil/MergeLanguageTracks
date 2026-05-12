using RemuxForge.Core.Analysis.FrameSync;
using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Media;
using RemuxForge.Core.Models;
using RemuxForge.Core.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Servizio di analisi avanzata per file con edit diversi tra source e lang
    /// Rileva offset multi-segmento e produce una EditMap per il riallineamento
    /// </summary>
    public class DeepAnalysisService : VideoSyncServiceBase
    {
        #region Costanti

        private const double INITIAL_VISUAL_GUARD_FPS = 1.0;
        private const int INITIAL_VISUAL_GUARD_SEARCH_STEP_MS = 500;
        private const int INITIAL_VISUAL_GUARD_LOCAL_DISTINCT_OFFSET_MS = 1000;
        private const int INITIAL_VISUAL_GUARD_DISTINCT_OFFSET_MS = 5000;
        private const double INITIAL_VISUAL_GUARD_MAX_REGION_SEC = 60.0;
        private const double INITIAL_VISUAL_GUARD_TIMELINE_LEAD_IN_SEC = 20.0;
        private const int INITIAL_VISUAL_GUARD_TIMELINE_START_PADDING_MS = 500;
        private const int INITIAL_VISUAL_GUARD_FRAMESYNC_CONSENSUS_MS = 500;
        private const int INITIAL_VISUAL_GUARD_FRAMESYNC_REJECT_MS = 1000;
        private const double INITIAL_VISUAL_GUARD_STRONG_MSE = 3500.0;
        private const double INITIAL_VISUAL_GUARD_STRONG_MARGIN = 0.08;
        private const double INITIAL_VISUAL_GUARD_LOCAL_MARGIN = 0.04;
        private const double INITIAL_VISUAL_GUARD_RELATIVE_IMPROVEMENT = 0.60;
        private const double INITIAL_VISUAL_GUARD_TIMELINE_IMPROVEMENT = 0.75;
        private const double INITIAL_VISUAL_GUARD_BORDER_MARGIN = 0.12;
        private const int VISUAL_BASELINE_CONFLICT_THRESHOLD_MS = 250;

        #endregion

        #region Variabili di classe

        /// <summary>
        /// Riferimento alla configurazione DeepAnalysis (binding diretto, modifiche immediate)
        /// </summary>
        private readonly DeepAnalysisConfig _daConfig;

        /// <summary>
        /// Tempo di esecuzione analisi in ms
        /// </summary>
        private long _analysisTimeMs;

        /// <summary>
        /// Contatori prestazionali dell'analisi corrente
        /// </summary>
        private DeepAnalysisPerformanceDiagnostic _performanceDiagnostics;

        /// <summary>
        /// Lock per aggiornare i contatori prestazionali durante estrazioni parallele
        /// </summary>
        private readonly object _performanceDiagnosticsLock;

        /// <summary>
        /// Resolver stretch globale
        /// </summary>
        private readonly DeepStretchResolver _stretchResolver;

        /// <summary>
        /// Refiner transizioni DeepAnalysis
        /// </summary>
        private readonly DeepTransitionRefiner _transitionRefiner;

        /// <summary>
        /// Verificatore globale DeepAnalysis
        /// </summary>
        private readonly DeepGlobalVerifier _globalVerifier;

        /// <summary>
        /// Mapper diagnostica regioni DeepAnalysis
        /// </summary>
        private readonly DeepAnalysisRegionMapper _regionMapper;

        /// <summary>
        /// Servizio envelope audio locale
        /// </summary>
        private readonly DeepAudioEnvelopeService _audioEnvelopeService;

        /// <summary>
        /// Analyzer visuale frame-based DeepAnalysis
        /// </summary>
        private readonly DeepVisualFrameAnalyzer _visualFrameAnalyzer;

        /// <summary>
        /// Mapper timeline-first basato su anchor audio distribuiti
        /// </summary>
        private DeepTimelineAnchorMapper _timelineAnchorMapper;

        /// <summary>
        /// Risolutore centralizzato per mkvmerge e tool esterni
        /// </summary>
        private readonly ToolPathResolverService _toolPathResolver;

        /// <summary>
        /// True se l'analisi corrente usa la mappa timeline-first
        /// </summary>
        private bool _currentAnalysisUsesTimelineMap;

        /// <summary>
        /// True se la timeline corrente usa una traccia audio comune source/lang
        /// </summary>
        private bool _currentAnalysisHasCommonAudio;

        /// <summary>
        /// File source dell'analisi corrente
        /// </summary>
        private string _currentAnalysisSourceFile;

        /// <summary>
        /// File lang dell'analisi corrente
        /// </summary>
        private string _currentAnalysisLanguageFile;

        /// <summary>
        /// Indice stream audio source da usare nei refine audio locali
        /// </summary>
        private int _currentSourceAudioStreamIndex;

        /// <summary>
        /// Indice stream audio language da usare nei refine audio locali
        /// </summary>
        private int _currentLanguageAudioStreamIndex;

        /// <summary>
        /// Ultima mappa diagnostica prodotta, anche in caso di fallimento
        /// </summary>
        private EditMap _lastAnalysisMap;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso eseguibile ffmpeg</param>
        public DeepAnalysisService(string ffmpegPath, ToolPathResolverService toolPathResolver = null) : base(ffmpegPath, LogSection.Deep)
        {
            this._daConfig = AppSettingsService.Instance.Settings.Advanced.DeepAnalysis;
            this._analysisTimeMs = 0;
            this._performanceDiagnostics = new DeepAnalysisPerformanceDiagnostic();
            this._performanceDiagnosticsLock = new object();
            this._toolPathResolver = toolPathResolver ?? new ToolPathResolverService(AppSettingsService.Instance.ConfigFolder);
            this._stretchResolver = new DeepStretchResolver();
            this._transitionRefiner = new DeepTransitionRefiner(this.GetTransitionRefineRadiusSec, this.AudioDifferentialCrossover, this.DifferentialScanCrossover, this.DenseScanCrossover, this.RepeatedFrameCrossover, this.LinearScanConfirm, this.VerifyTransitionLocal);
            this._globalVerifier = new DeepGlobalVerifier(this._daConfig, this._vsConfig, this.TryComputeGlobalPointMse);
            this._regionMapper = new DeepAnalysisRegionMapper();
            this._audioEnvelopeService = new DeepAudioEnvelopeService(this._ffmpegPath, this.RecordAudioEnvelopeExtract);
            this._visualFrameAnalyzer = new DeepVisualFrameAnalyzer(this._daConfig, this.ExtractDeepSegment, this.ComputeSsim, this.ComputeMse);
            this._timelineAnchorMapper = new DeepTimelineAnchorMapper(this.ResolveMkvMergePath(), this._audioEnvelopeService, this.BuildVisualTimelineAnchor);
            this._currentAnalysisUsesTimelineMap = false;
            this._currentAnalysisHasCommonAudio = false;
            this._currentAnalysisSourceFile = "";
            this._currentAnalysisLanguageFile = "";
            this._currentSourceAudioStreamIndex = 0;
            this._currentLanguageAudioStreamIndex = 0;
            this._lastAnalysisMap = null;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Analizza source e lang per produrre una EditMap con policy stretch esplicita
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="sourceDefaultDurationNs">Default duration traccia video source in nanosecondi</param>
        /// <param name="langDefaultDurationNs">Default duration traccia video lang in nanosecondi</param>
        /// <param name="sourceDurationMs">Durata container source in millisecondi</param>
        /// <param name="manualStretchFactor">Stretch factor manuale, vuoto se assente</param>
        /// <param name="allowAutoStretch">True per consentire stretch automatico da metadata gia' validati</param>
        /// <returns>EditMap con operazioni di edit, null se analisi fallita</returns>
        public EditMap Analyze(string sourceFile, string langFile, long sourceDefaultDurationNs, long langDefaultDurationNs, int sourceDurationMs, string manualStretchFactor, bool allowAutoStretch)
        {
            EditMap result = null;
            Stopwatch stopwatch = new Stopwatch();
            double stretchRatio;
            double inverseRatio;
            string stretchFactor;
            List<OffsetRegion> regions = null;
            List<EditOperation> operations = null;
            bool verified;
            double baselineMse = 0.0;
            int acceptedAnchors;
            long phaseStartMs;
            DeepAnalysisDiagnostics diagnostics = new DeepAnalysisDiagnostics();
            DeepAnalysisGlobalVerificationDiagnostic globalVerification;
            List<DeepAnalysisTransitionDiagnostic> transitions;
            DeepAnalysisInitialAlignmentDiagnostic initialAlignment;
            DeepTimelineMapResult timelineMap;
            int visualStartOffsetMs;
            double visualStartEndSec;
            double visualStartMse;
            double visualStartSecondMse;
            int visualBaselineOffsetMs;
            FrameSyncResult visualBaselineResult;
            bool visualBaselineOnly;
            stopwatch.Start();
            this._lastAnalysisMap = null;
            lock (this._performanceDiagnosticsLock)
            {
                this._performanceDiagnostics = diagnostics.Performance;
            }
            this._currentAnalysisUsesTimelineMap = false;
            this._currentAnalysisHasCommonAudio = false;
            this._currentAnalysisSourceFile = sourceFile;
            this._currentAnalysisLanguageFile = langFile;
            this._currentSourceAudioStreamIndex = 0;
            this._currentLanguageAudioStreamIndex = 0;
            diagnostics.ManualStretchRequested = manualStretchFactor != null ? manualStretchFactor : "";
            diagnostics.AllowAutoStretch = allowAutoStretch;
            this.PrepareGeometryDrivenCrop(sourceFile, langFile);
            diagnostics.SourceGeometry = this._lastSourceGeometryInfo;
            diagnostics.LanguageGeometry = this._lastLanguageGeometryInfo;

            // Fase 1: Rilevamento stretch globale
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Phase, "  Fase 1: Rilevamento stretch...");
            ConsoleHelper.Progress(LogSection.Deep, 14, "Deep: stretch");
            phaseStartMs = stopwatch.ElapsedMilliseconds;
            if (!this.DetectStretch(sourceDefaultDurationNs, langDefaultDurationNs, manualStretchFactor, allowAutoStretch, out stretchRatio, out inverseRatio, out stretchFactor))
            {
                stopwatch.Stop();
                this._analysisTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }
            diagnostics.Timing.StretchMs = stopwatch.ElapsedMilliseconds - phaseStartMs;
            diagnostics.StretchRatio = stretchRatio;
            diagnostics.InverseRatio = inverseRatio;
            diagnostics.StretchFactor = stretchFactor;

            // Fase 2: Timeline-first audio/video
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Phase, "  Fase 2: Mappa timeline...");
            ConsoleHelper.Progress(LogSection.Deep, 25, "Deep: timeline");
            phaseStartMs = stopwatch.ElapsedMilliseconds;
            timelineMap = this.BuildTimelineMap(sourceFile, langFile, sourceDurationMs);
            diagnostics.Timing.TimelineMapMs = stopwatch.ElapsedMilliseconds - phaseStartMs;
            if (timelineMap != null && timelineMap.Diagnostic != null)
            {
                diagnostics.TimelineMap = timelineMap.Diagnostic;
            }

            if (timelineMap == null || !timelineMap.Success || timelineMap.Regions == null || timelineMap.Regions.Count == 0)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Timeline fallita: " + (timelineMap != null ? timelineMap.RejectReason : "risultato nullo"));
                this.StoreFailedAnalysis(stopwatch, diagnostics, stretchFactor, regions, operations, baselineMse);
                return result;
            }

            this._currentAnalysisUsesTimelineMap = true;
            this._currentAnalysisHasCommonAudio = string.Equals(timelineMap.Diagnostic.AnchorMode, "audio-common", StringComparison.Ordinal);
            this._currentSourceAudioStreamIndex = timelineMap.Diagnostic.SourceAudioStreamIndex;
            this._currentLanguageAudioStreamIndex = timelineMap.Diagnostic.LanguageAudioStreamIndex;
            regions = timelineMap.Regions;
            visualBaselineOffsetMs = this.ResolveVisualBaselineOffset(sourceFile, langFile, timelineMap, inverseRatio, out visualBaselineResult);
            visualBaselineOnly = this.ShouldUseVisualBaselineOnly(timelineMap, visualBaselineOffsetMs, inverseRatio);
            if (visualBaselineOnly)
            {
                regions = this.BuildConstantOffsetRegions(visualBaselineOffsetMs, sourceDurationMs);
                visualStartOffsetMs = int.MinValue;
                visualStartEndSec = 0.0;
                visualStartMse = 0.0;
                visualStartSecondMse = 0.0;
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Timeline audio-common scartata come edit operativo: baseline visuale FrameSync " + visualBaselineOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms");
            }
            else
            {
                this.ApplyInitialVisualGuard(sourceFile, langFile, sourceDurationMs, regions, visualBaselineResult, out visualStartOffsetMs, out visualStartEndSec, out visualStartMse, out visualStartSecondMse);
            }

            acceptedAnchors = timelineMap.Diagnostic.AcceptedAnchorCount;
            initialAlignment = new DeepAnalysisInitialAlignmentDiagnostic();
            initialAlignment.SceneCandidateAvailable = visualStartOffsetMs != int.MinValue || visualBaselineOnly;
            initialAlignment.SceneOffsetMs = visualBaselineOnly ? visualBaselineOffsetMs : (visualStartOffsetMs != int.MinValue ? visualStartOffsetMs : 0);
            initialAlignment.SceneVotes = visualBaselineOnly ? acceptedAnchors : (visualStartOffsetMs != int.MinValue ? (int)Math.Round(visualStartEndSec) : 0);
            initialAlignment.SelectedSource = visualBaselineOnly ? "framesync-visual-baseline" : (visualStartOffsetMs != int.MinValue ? "timeline+visual-start-guard" : "timeline");
            initialAlignment.SelectedOffsetMs = (int)Math.Round(regions[0].OffsetMs);
            initialAlignment.DecisionReason = visualBaselineOnly ? "timeline audio-common a plateau singolo in conflitto con baseline visuale globale" : (visualStartOffsetMs != int.MinValue ? "timeline-first vincolata dal match video iniziale (mse=" + visualStartMse.ToString("F1", CultureInfo.InvariantCulture) + ", second=" + visualStartSecondMse.ToString("F1", CultureInfo.InvariantCulture) + ")" : "timeline-first audio/video anchor map");
            diagnostics.InitialAlignment = initialAlignment;
            diagnostics.Regions = this.BuildRegionDiagnostics(regions);
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Timeline accettata: " + regions.Count.ToString(CultureInfo.InvariantCulture) + " regioni, " + acceptedAnchors.ToString(CultureInfo.InvariantCulture) + " anchor");

            // Fase 3: Raffinamento transizioni
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Phase, "  Fase 3: Raffinamento transizioni...");
            ConsoleHelper.Progress(LogSection.Deep, 70, "Deep: transizioni");
            phaseStartMs = stopwatch.ElapsedMilliseconds;
            if (visualBaselineOnly)
            {
                operations = new List<EditOperation>();
                transitions = new List<DeepAnalysisTransitionDiagnostic>();
            }
            else
            {
                operations = this.RefineTransitions(sourceFile, langFile, regions, inverseRatio, out transitions);
            }
            diagnostics.Timing.TransitionRefineMs = stopwatch.ElapsedMilliseconds - phaseStartMs;
            diagnostics.Transitions = transitions;

            if (!this.ValidateTimelineTransitions(transitions))
            {
                if (!this.TryFallbackTimelineToConstantInitial(sourceDurationMs, transitions, operations, ref regions, diagnostics))
                {
                    this.StoreFailedAnalysis(stopwatch, diagnostics, stretchFactor, regions, operations, baselineMse);
                    return result;
                }
            }

            // Fase 4: Verifica globale
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Phase, "  Fase 4: Verifica globale...");
            ConsoleHelper.Progress(LogSection.Deep, 82, "Deep: verifica globale");
            phaseStartMs = stopwatch.ElapsedMilliseconds;
            verified = this.VerifyGlobal(sourceFile, langFile, regions, operations, inverseRatio, sourceDurationMs, out baselineMse, out globalVerification);
            diagnostics.Timing.GlobalVerifyMs = stopwatch.ElapsedMilliseconds - phaseStartMs;
            diagnostics.GlobalVerification = globalVerification;

            if (!verified)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Verifica globale fallita");
                this.StoreFailedAnalysis(stopwatch, diagnostics, stretchFactor, regions, operations, baselineMse);
                return result;
            }

            // Costruisci EditMap
            stopwatch.Stop();
            this._analysisTimeMs = stopwatch.ElapsedMilliseconds;
            diagnostics.Timing.TotalMs = this._analysisTimeMs;

            result = new EditMap();
            result.InitialDelayMs = (int)Math.Round(regions[0].OffsetMs);
            result.StretchFactor = stretchFactor;
            result.Operations = operations;
            result.AnalysisTimeMs = this._analysisTimeMs;
            result.BaselineMse = baselineMse;
            result.Diagnostics = diagnostics;
            this._lastAnalysisMap = result;

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Success, "  EditMap: delay=" + result.InitialDelayMs + "ms, " + operations.Count + " operazioni, analisi " + this._analysisTimeMs + "ms");
            ConsoleHelper.Progress(LogSection.Deep, 88, "Deep: edit map");

            return result;
        }

        /// <summary>
        /// Degrada una timeline audio-common a offset costante quando le transizioni non sono confermate dal video
        /// </summary>
        private bool TryFallbackTimelineToConstantInitial(int sourceDurationMs, List<DeepAnalysisTransitionDiagnostic> transitions, List<EditOperation> operations, ref List<OffsetRegion> regions, DeepAnalysisDiagnostics diagnostics)
        {
            bool result = false;
            bool hasUnverifiedTransition = false;
            int initialOffsetMs;

            if (!this._currentAnalysisUsesTimelineMap || !this._currentAnalysisHasCommonAudio)
            {
                return result;
            }

            if (regions == null || regions.Count == 0 || transitions == null || operations == null)
            {
                return result;
            }

            if (operations.Count > 0)
            {
                return result;
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                if (Math.Abs(transitions[i].DeltaMs) < 500)
                {
                    continue;
                }

                if (string.Equals(transitions[i].Status, "SkippedUnverified", StringComparison.Ordinal))
                {
                    hasUnverifiedTransition = true;
                }
                else
                {
                    return result;
                }
            }

            if (!hasUnverifiedTransition)
            {
                return result;
            }

            initialOffsetMs = (int)Math.Round(regions[0].OffsetMs);
            regions = this.BuildConstantOffsetRegions(initialOffsetMs, sourceDurationMs);
            operations.Clear();

            if (diagnostics != null)
            {
                diagnostics.Regions = this.BuildRegionDiagnostics(regions);
                if (diagnostics.InitialAlignment != null)
                {
                    diagnostics.InitialAlignment.SelectedOffsetMs = initialOffsetMs;
                    diagnostics.InitialAlignment.SelectedSource = "timeline-constant-fallback";
                    diagnostics.InitialAlignment.DecisionReason = "timeline audio-common degradata a offset costante: transizioni non confermate dalla verifica video locale";
                }
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Timeline audio-common degradata a offset costante: delay=" + initialOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms, transizioni non verificate scartate");
            result = true;
            return result;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Tempo di esecuzione analisi in ms
        /// </summary>
        public long AnalysisTimeMs { get { return this._analysisTimeMs; } }

        /// <summary>
        /// Ultima mappa prodotta, inclusa diagnostica su fallimento
        /// </summary>
        public EditMap LastAnalysisMap { get { return this._lastAnalysisMap; } }

        #endregion

        #region Metodi privati — Fase 1: Stretch

        /// <summary>
        /// Rileva stretch globale dai default_duration delle tracce video
        /// </summary>
        /// <param name="sourceDefaultDurationNs">Default duration source in ns</param>
        /// <param name="langDefaultDurationNs">Default duration lang in ns</param>
        /// <param name="manualStretchFactor">Stretch factor manuale richiesto, vuoto se non configurato</param>
        /// <param name="allowAutoStretch">True se e' consentito rilevare stretch automatico</param>
        /// <param name="stretchRatio">Rapporto stretch (output)</param>
        /// <param name="inverseRatio">Rapporto inverso per compensazione drift (output)</param>
        /// <param name="stretchFactor">Stringa stretch per mkvmerge (output)</param>
        private bool DetectStretch(long sourceDefaultDurationNs, long langDefaultDurationNs, string manualStretchFactor, bool allowAutoStretch, out double stretchRatio, out double inverseRatio, out string stretchFactor)
        {
            return this._stretchResolver.Detect(sourceDefaultDurationNs, langDefaultDurationNs, manualStretchFactor, allowAutoStretch, out stretchRatio, out inverseRatio, out stretchFactor);
        }

        #endregion

        #region Metodi privati — Fase 2: Timeline

        /// <summary>
        /// Costruisce una mappa timeline-first basata su anchor audio distribuiti
        /// </summary>
        private DeepTimelineMapResult BuildTimelineMap(string sourceFile, string langFile, int sourceDurationMs)
        {
            this._timelineAnchorMapper = new DeepTimelineAnchorMapper(this.ResolveMkvMergePath(), this._audioEnvelopeService, this.BuildVisualTimelineAnchor);
            return this._timelineAnchorMapper.Build(sourceFile, langFile, sourceDurationMs);
        }

        /// <summary>
        /// Calcola una baseline visuale globale tramite FrameSync quando puo' servire da veto alla timeline audio-common
        /// </summary>
        private int ResolveVisualBaselineOffset(string sourceFile, string langFile, DeepTimelineMapResult timelineMap, double inverseRatio, out FrameSyncResult frameSyncResult)
        {
            int result = int.MinValue;
            FrameSyncService frameSyncService;
            frameSyncResult = null;

            if (timelineMap == null || timelineMap.Regions == null || timelineMap.Regions.Count != 1 || timelineMap.Diagnostic == null)
            {
                return result;
            }

            if (!string.Equals(timelineMap.Diagnostic.AnchorMode, "audio-common", StringComparison.Ordinal))
            {
                return result;
            }

            if (Math.Abs(inverseRatio - 1.0) > 0.0001)
            {
                return result;
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Baseline visuale: avvio FrameSync di controllo per timeline audio-common a plateau singolo");
            frameSyncService = new FrameSyncService(this._ffmpegPath);
            result = frameSyncService.RefineOffset(sourceFile, langFile);
            frameSyncResult = frameSyncService.LastResult;
            if (result == int.MinValue)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Baseline visuale: FrameSync non ha trovato un offset affidabile");
            }
            else
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Baseline visuale: FrameSync=" + result.ToString(CultureInfo.InvariantCulture) + "ms");
            }

            return result;
        }

        /// <summary>
        /// Decide se una timeline audio-common a plateau singolo deve essere sostituita dalla baseline visuale costante
        /// </summary>
        private bool ShouldUseVisualBaselineOnly(DeepTimelineMapResult timelineMap, int visualBaselineOffsetMs, double inverseRatio)
        {
            int timelineOffsetMs;

            if (visualBaselineOffsetMs == int.MinValue || timelineMap == null || timelineMap.Regions == null || timelineMap.Regions.Count != 1 || timelineMap.Diagnostic == null)
            {
                return false;
            }

            if (!string.Equals(timelineMap.Diagnostic.AnchorMode, "audio-common", StringComparison.Ordinal))
            {
                return false;
            }

            if (Math.Abs(inverseRatio - 1.0) > 0.0001)
            {
                return false;
            }

            timelineOffsetMs = (int)Math.Round(timelineMap.Regions[0].OffsetMs);
            return Math.Abs(timelineOffsetMs - visualBaselineOffsetMs) > VISUAL_BASELINE_CONFLICT_THRESHOLD_MS;
        }

        /// <summary>
        /// Crea una singola regione con offset costante per tutta la durata source
        /// </summary>
        private List<OffsetRegion> BuildConstantOffsetRegions(int offsetMs, int sourceDurationMs)
        {
            List<OffsetRegion> result = new List<OffsetRegion>();
            OffsetRegion region = new OffsetRegion();
            double sourceDurationSec = sourceDurationMs / 1000.0;

            region.StartSrcSec = 0.0;
            region.EndSrcSec = sourceDurationSec;
            region.SupportStartSrcSec = 0.0;
            region.SupportEndSrcSec = sourceDurationSec;
            region.OffsetMs = offsetMs;
            region.MatchCount = 1;
            result.Add(region);

            return result;
        }

        /// <summary>
        /// Impedisce alla timeline audio di estendere all'inizio un plateau non confermato dal video
        /// </summary>
        private void ApplyInitialVisualGuard(string sourceFile, string langFile, int sourceDurationMs, List<OffsetRegion> regions, FrameSyncResult frameSyncResult, out int visualStartOffsetMs, out double visualStartEndSec, out double visualStartMse, out double visualStartSecondMse)
        {
            int timelineOffsetMs;
            int frameSyncEvidenceOffsetMs;
            double visualStartLocalSecondMse;
            double timelineMse;
            bool visualStartBorderCandidate;
            double sourceDurationSec = sourceDurationMs / 1000.0;

            visualStartOffsetMs = int.MinValue;
            visualStartEndSec = 0.0;
            visualStartMse = 0.0;
            visualStartSecondMse = 0.0;

            if (regions == null || regions.Count == 0 || sourceDurationSec < 90.0)
            {
                return;
            }

            timelineOffsetMs = (int)Math.Round(regions[0].OffsetMs);
            if (!this.TryFindInitialVisualGuardOffset(sourceFile, langFile, sourceDurationSec, timelineOffsetMs, out visualStartOffsetMs, out visualStartEndSec, out visualStartMse, out visualStartSecondMse, out visualStartLocalSecondMse, out timelineMse, out visualStartBorderCandidate))
            {
                visualStartOffsetMs = int.MinValue;
                return;
            }

            if (Math.Abs(timelineOffsetMs - visualStartOffsetMs) <= 1000)
            {
                visualStartOffsetMs = int.MinValue;
                return;
            }

            frameSyncEvidenceOffsetMs = int.MinValue;
            if (frameSyncResult != null)
            {
                if (frameSyncResult.Success && frameSyncResult.OffsetMs != int.MinValue)
                {
                    frameSyncEvidenceOffsetMs = frameSyncResult.OffsetMs;
                }
                else if (frameSyncResult.Initial != null && frameSyncResult.Initial.Success && frameSyncResult.Initial.BestCandidate != null)
                {
                    frameSyncEvidenceOffsetMs = frameSyncResult.Initial.BestCandidate.OffsetMs;
                }
            }

            if (frameSyncEvidenceOffsetMs != int.MinValue &&
                Math.Abs(frameSyncEvidenceOffsetMs - timelineOffsetMs) <= INITIAL_VISUAL_GUARD_FRAMESYNC_CONSENSUS_MS &&
                Math.Abs(frameSyncEvidenceOffsetMs - visualStartOffsetMs) > INITIAL_VISUAL_GUARD_FRAMESYNC_REJECT_MS)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Start guard scartato: candidate=" + visualStartOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms, timeline=" + timelineOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms, FrameSync=" + frameSyncEvidenceOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms");
                visualStartOffsetMs = int.MinValue;
                return;
            }

            if (!double.IsPositiveInfinity(timelineMse) && visualStartMse > timelineMse * INITIAL_VISUAL_GUARD_TIMELINE_IMPROVEMENT)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Start guard scartato: candidate=" + visualStartOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms non migliora timeline " + timelineOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms (mse=" + visualStartMse.ToString("F1", CultureInfo.InvariantCulture) + ", timelineMse=" + timelineMse.ToString("F1", CultureInfo.InvariantCulture) + ")");
                visualStartOffsetMs = int.MinValue;
                return;
            }

            if (visualStartBorderCandidate && (visualStartMse > INITIAL_VISUAL_GUARD_STRONG_MSE || double.IsPositiveInfinity(visualStartLocalSecondMse) || ((visualStartLocalSecondMse - visualStartMse) / Math.Max(visualStartLocalSecondMse, 1.0)) < INITIAL_VISUAL_GUARD_BORDER_MARGIN))
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Start guard scartato: candidate=" + visualStartOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms e' sul bordo language (mse=" + visualStartMse.ToString("F1", CultureInfo.InvariantCulture) + ", localSecond=" + visualStartLocalSecondMse.ToString("F1", CultureInfo.InvariantCulture) + ")");
                visualStartOffsetMs = int.MinValue;
                return;
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Start guard: timeline initial " + timelineOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms corretto a " + visualStartOffsetMs.ToString(CultureInfo.InvariantCulture) + "ms sui primi " + visualStartEndSec.ToString("F0", CultureInfo.InvariantCulture) + "s");
            this.PrependInitialGuardRegion(regions, visualStartOffsetMs, visualStartEndSec, sourceDurationSec);
        }

        /// <summary>
        /// Cerca il miglior offset visuale sui primi secondi, dove sigla/logo iniziale non devono ereditare plateau successivi
        /// </summary>
        private bool TryFindInitialVisualGuardOffset(string sourceFile, string langFile, double sourceDurationSec, int timelineOffsetMs, out int offsetMs, out double guardEndSec, out double bestMse, out double secondMse, out double localSecondMse, out double timelineMse, out bool borderCandidate)
        {
            bool result = false;
            FrameSyncConfig frameSyncConfig = AppSettingsService.Instance.Settings.Advanced.FrameSync;
            double languageExtractDurationSec = frameSyncConfig.LangDurationSec;
            int sourceStartMs = frameSyncConfig.SourceStartSec * 1000;
            double sourceExtractDurationSec;
            int minOffsetMs;
            int maxOffsetMs;
            List<int> candidateOffsets = new List<int>();
            List<double> candidateMseValues = new List<double>();
            List<byte[]> sourceFrames;
            double[] sourceTimestampsMs;
            List<byte[]> langFrames;
            double[] langTimestampsMs;

            offsetMs = 0;
            if (timelineOffsetMs > sourceStartMs)
            {
                sourceStartMs = timelineOffsetMs + INITIAL_VISUAL_GUARD_TIMELINE_START_PADDING_MS;
            }

            sourceExtractDurationSec = Math.Min(sourceDurationSec - (sourceStartMs / 1000.0), frameSyncConfig.SourceDurationSec);
            guardEndSec = Math.Min(sourceDurationSec, INITIAL_VISUAL_GUARD_MAX_REGION_SEC);
            bestMse = double.PositiveInfinity;
            secondMse = double.PositiveInfinity;
            localSecondMse = double.PositiveInfinity;
            timelineMse = double.PositiveInfinity;
            borderCandidate = false;

            if (sourceExtractDurationSec < 20.0)
            {
                return result;
            }

            this.CalculateWindowOffsetRange(sourceStartMs, sourceExtractDurationSec, languageExtractDurationSec, out minOffsetMs, out maxOffsetMs);
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Start guard: ricerca visuale iniziale source start " + (sourceStartMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s, durata " + sourceExtractDurationSec.ToString("F0", CultureInfo.InvariantCulture) + "s/lang " + languageExtractDurationSec.ToString("F0", CultureInfo.InvariantCulture) + "s, range " + minOffsetMs + "ms.." + maxOffsetMs + "ms");

            this.ExtractDeepSegment(sourceFile, sourceStartMs, sourceExtractDurationSec, INITIAL_VISUAL_GUARD_FPS, this._geometryCropSourceToFourThree, out sourceFrames, out sourceTimestampsMs);
            this.ExtractDeepSegment(langFile, 0, languageExtractDurationSec, INITIAL_VISUAL_GUARD_FPS, this._geometryCropLanguageToFourThree, out langFrames, out langTimestampsMs);
            if (sourceFrames == null || langFrames == null || sourceFrames.Count < 20 || langFrames.Count < 20)
            {
                return result;
            }

            for (int candidateOffsetMs = minOffsetMs; candidateOffsetMs <= maxOffsetMs; candidateOffsetMs += INITIAL_VISUAL_GUARD_SEARCH_STEP_MS)
            {
                double languageStartMs = sourceStartMs - candidateOffsetMs;
                if (languageStartMs < 0.0)
                {
                    continue;
                }

                double mse = this.ComputeTimestampMatchedMseAtStart(sourceFrames, sourceTimestampsMs, langFrames, langTimestampsMs, languageStartMs, INITIAL_VISUAL_GUARD_FPS);
                if (double.IsPositiveInfinity(mse))
                {
                    continue;
                }

                candidateOffsets.Add(candidateOffsetMs);
                candidateMseValues.Add(mse);
                if (mse < bestMse)
                {
                    bestMse = mse;
                    offsetMs = candidateOffsetMs;
                    borderCandidate = languageStartMs <= INITIAL_VISUAL_GUARD_SEARCH_STEP_MS;
                }
            }

            for (int i = 0; i < candidateMseValues.Count; i++)
            {
                if (Math.Abs(candidateOffsets[i] - offsetMs) < INITIAL_VISUAL_GUARD_DISTINCT_OFFSET_MS)
                {
                    continue;
                }

                if (candidateMseValues[i] < secondMse)
                {
                    secondMse = candidateMseValues[i];
                }
            }

            for (int i = 0; i < candidateMseValues.Count; i++)
            {
                if (Math.Abs(candidateOffsets[i] - offsetMs) < INITIAL_VISUAL_GUARD_LOCAL_DISTINCT_OFFSET_MS)
                {
                    continue;
                }

                if (candidateMseValues[i] < localSecondMse)
                {
                    localSecondMse = candidateMseValues[i];
                }
            }

            double timelineLanguageStartMs = sourceStartMs - timelineOffsetMs;
            if (timelineLanguageStartMs >= 0.0)
            {
                timelineMse = this.ComputeTimestampMatchedMseAtStart(sourceFrames, sourceTimestampsMs, langFrames, langTimestampsMs, timelineLanguageStartMs, INITIAL_VISUAL_GUARD_FPS);
            }

            if (double.IsPositiveInfinity(bestMse) || double.IsPositiveInfinity(secondMse) || double.IsPositiveInfinity(localSecondMse))
            {
                return result;
            }

            double relativeMargin = (secondMse - bestMse) / Math.Max(secondMse, 1.0);
            double localMargin = (localSecondMse - bestMse) / Math.Max(localSecondMse, 1.0);
            bool absoluteStrong = bestMse <= INITIAL_VISUAL_GUARD_STRONG_MSE && relativeMargin >= INITIAL_VISUAL_GUARD_STRONG_MARGIN && localMargin >= INITIAL_VISUAL_GUARD_LOCAL_MARGIN;
            bool relativeStrong = !double.IsPositiveInfinity(timelineMse) && bestMse <= timelineMse * INITIAL_VISUAL_GUARD_RELATIVE_IMPROVEMENT && relativeMargin >= INITIAL_VISUAL_GUARD_STRONG_MARGIN && localMargin >= INITIAL_VISUAL_GUARD_LOCAL_MARGIN;

            if (absoluteStrong || relativeStrong)
            {
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Calcola il range offset coperto dalla finestra estratta
        /// </summary>
        private void CalculateWindowOffsetRange(int sourceStartMs, double sourceDurationSec, double langDurationSec, out int minOffsetMs, out int maxOffsetMs)
        {
            minOffsetMs = -sourceStartMs - (int)Math.Ceiling(sourceDurationSec * 1000.0);
            maxOffsetMs = (int)Math.Ceiling(langDurationSec * 1000.0) - sourceStartMs;
        }

        /// <summary>
        /// Inserisce una regione iniziale supportata dal video e lascia al refine il compito di localizzare il primo cambio
        /// </summary>
        private void PrependInitialGuardRegion(List<OffsetRegion> regions, int visualStartOffsetMs, double visualStartEndSec, double sourceDurationSec)
        {
            OffsetRegion firstRegion = regions[0];
            OffsetRegion guardRegion = new OffsetRegion();
            double guardEndSec = visualStartEndSec;
            double firstSupportStartSec = firstRegion.SupportStartSrcSec;
            double bridgedGuardEndSec;

            if (guardEndSec < 20.0) { guardEndSec = 20.0; }
            if (guardEndSec > sourceDurationSec) { guardEndSec = sourceDurationSec; }

            if (firstSupportStartSec > guardEndSec + INITIAL_VISUAL_GUARD_TIMELINE_LEAD_IN_SEC)
            {
                bridgedGuardEndSec = firstSupportStartSec - INITIAL_VISUAL_GUARD_TIMELINE_LEAD_IN_SEC;
                if (bridgedGuardEndSec > guardEndSec)
                {
                    guardEndSec = bridgedGuardEndSec;
                }
            }

            if (guardEndSec > firstRegion.EndSrcSec - 1.0 && firstRegion.EndSrcSec > 1.0)
            {
                guardEndSec = firstRegion.EndSrcSec - 1.0;
            }

            if (guardEndSec > sourceDurationSec) { guardEndSec = sourceDurationSec; }

            guardRegion.StartSrcSec = 0.0;
            guardRegion.EndSrcSec = guardEndSec;
            guardRegion.SupportStartSrcSec = 0.0;
            guardRegion.SupportEndSrcSec = guardEndSec;
            guardRegion.OffsetMs = visualStartOffsetMs;
            guardRegion.MatchCount = 1;

            firstRegion.StartSrcSec = guardEndSec;
            if (firstRegion.SupportStartSrcSec < guardEndSec)
            {
                firstRegion.SupportStartSrcSec = guardEndSec;
            }

            if (firstRegion.EndSrcSec <= firstRegion.StartSrcSec)
            {
                firstRegion.EndSrcSec = firstRegion.StartSrcSec + 1.0;
            }

            regions.Insert(0, guardRegion);
        }

        /// <summary>
        /// Costruisce un anchor timeline via confronto visuale distribuito
        /// </summary>
        private bool BuildVisualTimelineAnchor(string sourceFile, string langFile, double sourceCenterSec, int searchRadiusMs, int searchStepMs, out DeepAnalysisTimelineAnchorDiagnostic anchor)
        {
            bool result = false;
            double durationSec = 4.0;
            double targetFps = Math.Max(4.0, 1000.0 / Math.Max(searchStepMs, 1));
            double sourceStartSec = sourceCenterSec - (durationSec / 2.0);
            double languageWideStartSec;
            double languageWideDurationSec;
            int bestOffsetMs = 0;
            int secondOffsetMs = 0;
            double bestMse = double.PositiveInfinity;
            double secondMse = double.PositiveInfinity;
            double score;
            double margin;
            List<byte[]> sourceFrames;
            double[] sourceTs;
            List<byte[]> languageFrames;
            double[] languageTs;

            anchor = new DeepAnalysisTimelineAnchorDiagnostic();
            anchor.SourceCenterSec = sourceCenterSec;

            if (sourceStartSec < 0.0)
            {
                sourceStartSec = 0.0;
            }

            this.ExtractDeepSegment(sourceFile, (int)Math.Round(sourceStartSec * 1000.0), durationSec, targetFps, this._geometryCropSourceToFourThree, out sourceFrames, out sourceTs);
            if (sourceFrames == null || sourceFrames.Count == 0)
            {
                anchor.RejectReason = "frame source insufficienti";
                return result;
            }

            languageWideStartSec = sourceStartSec - (searchRadiusMs / 1000.0);
            if (languageWideStartSec < 0.0)
            {
                languageWideStartSec = 0.0;
            }

            languageWideDurationSec = durationSec + ((searchRadiusMs * 2.0) / 1000.0);
            this.ExtractDeepSegment(langFile, (int)Math.Round(languageWideStartSec * 1000.0), languageWideDurationSec, targetFps, this._geometryCropLanguageToFourThree, out languageFrames, out languageTs);
            if (languageFrames == null || languageFrames.Count == 0)
            {
                anchor.RejectReason = "frame language insufficienti";
                return result;
            }

            for (int offsetMs = -searchRadiusMs; offsetMs <= searchRadiusMs; offsetMs += searchStepMs)
            {
                double languageStartSec = sourceStartSec - (offsetMs / 1000.0);
                if (languageStartSec < 0.0)
                {
                    continue;
                }

                double mse = this.ComputeTimestampMatchedMseAtStart(sourceFrames, sourceTs, languageFrames, languageTs, languageStartSec * 1000.0, targetFps);
                if (mse < bestMse)
                {
                    secondMse = bestMse;
                    secondOffsetMs = bestOffsetMs;
                    bestMse = mse;
                    bestOffsetMs = offsetMs;
                }
                else if (mse < secondMse)
                {
                    secondMse = mse;
                    secondOffsetMs = offsetMs;
                }
            }

            if (double.IsPositiveInfinity(bestMse) || double.IsPositiveInfinity(secondMse))
            {
                anchor.RejectReason = "nessun candidato video valido";
                return result;
            }

            score = 1.0 / (1.0 + (bestMse / 2500.0));
            margin = !double.IsPositiveInfinity(secondMse) ? Math.Abs(secondMse - bestMse) / Math.Max(secondMse, 1.0) : 1.0;

            anchor.OffsetMs = bestOffsetMs;
            anchor.Score = score;
            anchor.Margin = margin;
            anchor.Accepted = score >= 0.35 && margin >= 0.04 && Math.Abs(bestOffsetMs - secondOffsetMs) >= searchStepMs;
            if (!anchor.Accepted)
            {
                anchor.RejectReason = "score/margine video bassi";
            }

            result = true;
            return result;
        }

        /// <summary>
        /// Calcola MSE medio tra frame source e una finestra language larga, usando uno start language candidato
        /// </summary>
        private double ComputeTimestampMatchedMseAtStart(List<byte[]> srcFrames, double[] srcTimestampsMs, List<byte[]> langFrames, double[] langTimestampsMs, double languageStartMs, double targetFps)
        {
            double result = 0.0;
            double total = 0.0;
            int count = 0;
            double toleranceMs = 1000.0 / Math.Max(targetFps, 1.0);

            if (srcFrames == null || langFrames == null || srcTimestampsMs == null || langTimestampsMs == null || srcFrames.Count == 0 || langFrames.Count == 0)
            {
                return double.PositiveInfinity;
            }

            for (int i = 0; i < srcFrames.Count && i < srcTimestampsMs.Length; i++)
            {
                double sourceRelativeMs = srcTimestampsMs[i] - srcTimestampsMs[0];
                double targetLangMs = languageStartMs + sourceRelativeMs;
                int languageIndex = NearestTimestampIndex(langTimestampsMs, targetLangMs);
                if (languageIndex < 0 || languageIndex >= langFrames.Count)
                {
                    continue;
                }

                if (Math.Abs(langTimestampsMs[languageIndex] - targetLangMs) > toleranceMs)
                {
                    continue;
                }

                total += this.ComputeMse(srcFrames[i], langFrames[languageIndex]);
                count++;
            }

            if (count >= Math.Max(3, srcFrames.Count / 2))
            {
                result = total / count;
            }
            else
            {
                result = double.PositiveInfinity;
            }

            return result;
        }

        #endregion

        #region Metodi privati — Fase 3: Raffinamento transizioni

        /// <summary>
        /// Raffina i punti di transizione tramite scansione locale audio/video
        /// </summary>
        private List<EditOperation> RefineTransitions(string sourceFile, string langFile, List<OffsetRegion> regions, double inverseRatio, out List<DeepAnalysisTransitionDiagnostic> transitions)
        {
            DeepAnalysisPerformanceDiagnostic performanceDiagnostics;
            lock (this._performanceDiagnosticsLock)
            {
                performanceDiagnostics = this._performanceDiagnostics;
            }

            return this._transitionRefiner.Refine(sourceFile, langFile, regions, inverseRatio, performanceDiagnostics, this._currentAnalysisUsesTimelineMap, this._currentAnalysisHasCommonAudio, out transitions);
        }

        /// <summary>
        /// Valida che le transizioni non scartate abbiano prodotto operazioni applicabili
        /// </summary>
        private bool ValidateTimelineTransitions(List<DeepAnalysisTransitionDiagnostic> transitions)
        {
            bool result = true;

            if (transitions == null)
            {
                return false;
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                if (Math.Abs(transitions[i].DeltaMs) < 500)
                {
                    continue;
                }

                if (string.Equals(transitions[i].Status, "SkippedUnverified", StringComparison.Ordinal))
                {
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Timeline rifiutata: transizione " + transitions[i].Index.ToString(CultureInfo.InvariantCulture) + " scartata dalla verifica locale (" + transitions[i].RejectReason + ")");
                    result = false;
                    break;
                }

                if (!string.Equals(transitions[i].Status, "Accepted", StringComparison.Ordinal) && !string.Equals(transitions[i].Status, "AcceptedAudio", StringComparison.Ordinal) && !string.Equals(transitions[i].Status, "AcceptedTimeline", StringComparison.Ordinal))
                {
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Timeline rifiutata: transizione " + transitions[i].Index.ToString(CultureInfo.InvariantCulture) + " non risolta (" + transitions[i].RejectReason + ")");
                    result = false;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Salva diagnostica completa anche quando l'analisi fallisce
        /// </summary>
        private void StoreFailedAnalysis(Stopwatch stopwatch, DeepAnalysisDiagnostics diagnostics, string stretchFactor, List<OffsetRegion> regions, List<EditOperation> operations, double baselineMse)
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }

            this._analysisTimeMs = stopwatch.ElapsedMilliseconds;
            diagnostics.Timing.TotalMs = this._analysisTimeMs;

            EditMap map = new EditMap();
            map.InitialDelayMs = regions != null && regions.Count > 0 ? (int)Math.Round(regions[0].OffsetMs) : 0;
            map.StretchFactor = stretchFactor != null ? stretchFactor : "";
            map.Operations = operations != null ? operations : new List<EditOperation>();
            map.AnalysisTimeMs = this._analysisTimeMs;
            map.BaselineMse = baselineMse;
            map.Diagnostics = diagnostics;
            this._lastAnalysisMap = map;
        }

        /// <summary>
        /// Calcola il raggio massimo di raffinamento attorno al breakpoint tra due regioni
        /// </summary>
        /// <param name="leftRegion">Regione precedente</param>
        /// <param name="rightRegion">Regione successiva</param>
        /// <returns>Raggio in secondi</returns>
        private double GetTransitionRefineRadiusSec(OffsetRegion leftRegion, OffsetRegion rightRegion)
        {
            double result;
            double maxRadius = 20.0;
            double deltaMs;
            if (this._daConfig.WideProbeToleranceSec > 0.0)
            {
                maxRadius = this._daConfig.WideProbeToleranceSec;
            }

            if (this._currentAnalysisUsesTimelineMap && maxRadius < 45.0)
            {
                maxRadius = 45.0;
            }

            deltaMs = Math.Abs(rightRegion.OffsetMs - leftRegion.OffsetMs);
            if (deltaMs <= 1500.0)
            {
                result = 10.0;
            }
            else if (deltaMs <= 8000.0)
            {
                result = this._currentAnalysisUsesTimelineMap ? 45.0 : 15.0;
            }
            else
            {
                result = this._currentAnalysisUsesTimelineMap ? 45.0 : 20.0;
            }

            if (result > maxRadius) { result = maxRadius; }
            if (result < 3.0) { result = 3.0; }

            return result;
        }

        /// <summary>
        /// Scansione densa del punto di transizione
        /// Estrae frame a fps fisso per l'intera regione e cerca il primo cluster
        /// di frame dove SSIM con old offset scende sotto soglia
        /// Robusto: non assume monotonicita' SSIM, trova dip anche in scene lente
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="searchStartSrc">Inizio finestra source in secondi</param>
        /// <param name="searchEndSrc">Fine finestra source in secondi</param>
        /// <param name="oldOffsetSec">Offset vecchio in secondi</param>
        /// <param name="inverseRatio">Rapporto inverso stretch</param>
        /// <returns>Timestamp source approssimato della transizione</returns>
        private double DenseScanCrossover(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double inverseRatio)
        {
            return this._visualFrameAnalyzer.DenseScanCrossover(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, inverseRatio, this._geometryCropSourceToFourThree, this._geometryCropLanguageToFourThree);
        }

        /// <summary>
        /// Raffina il crossover usando la durata dei run di frame ripetuti
        /// </summary>
        private double RepeatedFrameCrossover(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio)
        {
            return this._visualFrameAnalyzer.RepeatedFrameCrossover(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, newOffsetSec, inverseRatio, this._geometryCropSourceToFourThree, this._geometryCropLanguageToFourThree);
        }

        /// <summary>
        /// Risolve mkvmerge in modo centralizzato
        /// </summary>
        /// <returns>Percorso mkvmerge valido, oppure stringa vuota</returns>
        private string ResolveMkvMergePath()
        {
            string mkvMergePath = this._toolPathResolver.ResolveMkvMergePath(false);
            if (mkvMergePath.Length == 0)
            {
                mkvMergePath = AppSettingsService.Instance.Settings.Tools.MkvMergePath;
            }

            return mkvMergePath;
        }

        /// <summary>
        /// Cerca il crossover confrontando direttamente vecchio e nuovo offset nella finestra locale
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="searchStartSrc">Inizio finestra source in secondi</param>
        /// <param name="searchEndSrc">Fine finestra source in secondi</param>
        /// <param name="oldOffsetSec">Offset precedente in secondi</param>
        /// <param name="newOffsetSec">Offset successivo in secondi</param>
        /// <param name="inverseRatio">Rapporto inverso stretch</param>
        /// <returns>Timestamp source del crossover, oppure -1 se non conclusivo</returns>
        private double DifferentialScanCrossover(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio)
        {
            double result = -1.0;
            double duration = searchEndSrc - searchStartSrc;
            double scanFps = Math.Max(this._daConfig.DenseScanFps, 4.0);
            double langStartOld = searchStartSrc - oldOffsetSec;
            double langStartNew = searchStartSrc - newOffsetSec;
            List<byte[]> srcFrames;
            double[] sourceTimestampsMs;
            List<byte[]> langOldFrames;
            double[] langOldTimestampsMs;
            List<byte[]> langNewFrames;
            double[] langNewTimestampsMs;
            double toleranceMs = 1000.0 / scanFps * 2.0;
            int maxIdx;
            int consecutiveNewBetter = 0;
            int crossoverIdx = -1;
            double srcRelMs;
            double targetOldMs;
            double targetNewMs;
            int oldIdx;
            int newIdx;
            double oldDistMs;
            double newDistMs;
            double ssimOld;
            double ssimNew;
            double minNewSsim = Math.Max(this._daConfig.OffsetProbeMinSsim, 0.65);
            double minMargin = 0.05;
            int requiredFrames = Math.Max(2, Math.Min(this._daConfig.LinearScanConfirmFrames, 4));

            if (duration < 1.0)
            {
                return result;
            }

            if (Math.Abs(inverseRatio - 1.0) > 0.0001)
            {
                langStartOld = langStartOld * inverseRatio;
                langStartNew = langStartNew * inverseRatio;
            }

            if (langStartOld < 0.0) { langStartOld = 0.0; }
            if (langStartNew < 0.0) { langStartNew = 0.0; }

            this.ExtractDeepSegment(sourceFile, (int)(searchStartSrc * 1000), duration, scanFps, this._geometryCropSourceToFourThree, out srcFrames, out sourceTimestampsMs);
            this.ExtractDeepSegment(langFile, (int)(langStartOld * 1000), duration, scanFps, this._geometryCropLanguageToFourThree, out langOldFrames, out langOldTimestampsMs);
            this.ExtractDeepSegment(langFile, (int)(langStartNew * 1000), duration, scanFps, this._geometryCropLanguageToFourThree, out langNewFrames, out langNewTimestampsMs);

            if (srcFrames.Count < requiredFrames || langOldFrames.Count < requiredFrames || langNewFrames.Count < requiredFrames)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "    Scansione differenziale: frame insufficienti");
                return result;
            }

            maxIdx = srcFrames.Count;

            for (int i = 0; i < maxIdx; i++)
            {
                srcRelMs = sourceTimestampsMs[i] - sourceTimestampsMs[0];
                targetOldMs = langOldTimestampsMs[0] + srcRelMs;
                targetNewMs = langNewTimestampsMs[0] + srcRelMs;
                oldIdx = NearestTimestampIndex(langOldTimestampsMs, targetOldMs);
                newIdx = NearestTimestampIndex(langNewTimestampsMs, targetNewMs);

                if (oldIdx < 0 || oldIdx >= langOldFrames.Count || newIdx < 0 || newIdx >= langNewFrames.Count)
                {
                    consecutiveNewBetter = 0;
                    crossoverIdx = -1;
                    continue;
                }

                oldDistMs = Math.Abs(langOldTimestampsMs[oldIdx] - targetOldMs);
                newDistMs = Math.Abs(langNewTimestampsMs[newIdx] - targetNewMs);
                if (oldDistMs > toleranceMs || newDistMs > toleranceMs)
                {
                    consecutiveNewBetter = 0;
                    crossoverIdx = -1;
                    continue;
                }

                ssimOld = this.ComputeSsim(srcFrames[i], langOldFrames[oldIdx]);
                ssimNew = this.ComputeSsim(srcFrames[i], langNewFrames[newIdx]);

                if (ssimNew >= minNewSsim && ssimNew > ssimOld + minMargin)
                {
                    if (consecutiveNewBetter == 0)
                    {
                        crossoverIdx = i;
                    }

                    consecutiveNewBetter++;

                    if (consecutiveNewBetter >= requiredFrames)
                    {
                        result = sourceTimestampsMs[crossoverIdx] / 1000.0;
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Scansione differenziale: crossover a src " + result.ToString("F2", CultureInfo.InvariantCulture) + "s (new>old per " + consecutiveNewBetter + " frame)");
                        return result;
                    }
                }
                else
                {
                    consecutiveNewBetter = 0;
                    crossoverIdx = -1;
                }
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "    Scansione differenziale: non conclusiva");

            return result;
        }

        /// <summary>
        /// Refine locale audio: confronta source con language a vecchio e nuovo offset nella finestra
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file language</param>
        /// <param name="searchStartSrc">Inizio finestra source in secondi</param>
        /// <param name="searchEndSrc">Fine finestra source in secondi</param>
        /// <param name="oldOffsetSec">Offset precedente in secondi</param>
        /// <param name="newOffsetSec">Offset successivo in secondi</param>
        /// <param name="inverseRatio">Rapporto inverso stretch</param>
        /// <returns>Timestamp source dell'operazione, oppure -1 se non conclusivo</returns>
        private double AudioDifferentialCrossover(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio)
        {
            double result = -1.0;
            double duration = searchEndSrc - searchStartSrc;
            double langStartOld = searchStartSrc - oldOffsetSec;
            double langStartNew = searchStartSrc - newOffsetSec;
            double[] sourceEnvelope;
            double[] oldEnvelope;
            double[] newEnvelope;
            int windowMs = 50;
            int compareWindows = 200;
            int stepWindows = 10;
            int requiredWindows = 2;
            int maxIndex;
            int consecutive = 0;
            int crossoverIndex = -1;
            double oldScore;
            double newScore;
            double minScore = 0.70;
            double minMargin = 0.05;

            if (duration < 2.0)
            {
                return result;
            }

            if (Math.Abs(inverseRatio - 1.0) > 0.0001)
            {
                langStartOld = langStartOld * inverseRatio;
                langStartNew = langStartNew * inverseRatio;
            }

            if (langStartOld < 0.0) { langStartOld = 0.0; }
            if (langStartNew < 0.0) { langStartNew = 0.0; }

            sourceEnvelope = this.ExtractAudioEnvelope(sourceFile, searchStartSrc, duration, windowMs);
            oldEnvelope = this.ExtractAudioEnvelope(langFile, langStartOld, duration, windowMs);
            newEnvelope = this.ExtractAudioEnvelope(langFile, langStartNew, duration, windowMs);

            if (sourceEnvelope == null || oldEnvelope == null || newEnvelope == null)
            {
                return result;
            }

            maxIndex = Math.Min(sourceEnvelope.Length, Math.Min(oldEnvelope.Length, newEnvelope.Length)) - compareWindows;
            if (maxIndex <= compareWindows)
            {
                return result;
            }

            for (int i = 0; i < maxIndex; i += stepWindows)
            {
                oldScore = this.ScoreAudioEnvelopeWindow(sourceEnvelope, oldEnvelope, i, compareWindows);
                newScore = this.ScoreAudioEnvelopeWindow(sourceEnvelope, newEnvelope, i, compareWindows);

                if (newScore >= minScore && newScore > oldScore + minMargin)
                {
                    if (consecutive == 0)
                    {
                        crossoverIndex = i;
                    }

                    consecutive++;

                    if (consecutive >= requiredWindows)
                    {
                        result = searchStartSrc + ((crossoverIndex * windowMs) / 1000.0);
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Refine audio locale: new=" + newScore.ToString("F3", CultureInfo.InvariantCulture) + " old=" + oldScore.ToString("F3", CultureInfo.InvariantCulture));
                        return result;
                    }
                }
                else
                {
                    consecutive = 0;
                    crossoverIndex = -1;
                }
            }

            return result;
        }

        /// <summary>
        /// Estrae un envelope audio mono PCM tramite ffmpeg
        /// </summary>
        /// <param name="filePath">Percorso file</param>
        /// <param name="startSec">Start in secondi</param>
        /// <param name="durationSec">Durata in secondi</param>
        /// <param name="windowMs">Finestra envelope in ms</param>
        /// <returns>Envelope normalizzato, oppure null</returns>
        private double[] ExtractAudioEnvelope(string filePath, double startSec, double durationSec, int windowMs)
        {
            int audioStreamIndex = 0;
            if (this._currentAnalysisHasCommonAudio)
            {
                if (string.Equals(filePath, this._currentAnalysisSourceFile, StringComparison.Ordinal))
                {
                    audioStreamIndex = this._currentSourceAudioStreamIndex;
                }
                else if (string.Equals(filePath, this._currentAnalysisLanguageFile, StringComparison.Ordinal))
                {
                    audioStreamIndex = this._currentLanguageAudioStreamIndex;
                }
            }

            return this._audioEnvelopeService.Extract(filePath, startSec, durationSec, windowMs, audioStreamIndex);
        }

        /// <summary>
        /// Confronta due finestre envelope audio
        /// </summary>
        private double ScoreAudioEnvelopeWindow(double[] sourceEnvelope, double[] languageEnvelope, int startIndex, int count)
        {
            return this._audioEnvelopeService.ScoreWindow(sourceEnvelope, languageEnvelope, startIndex, count);
        }

        /// <summary>
        /// Registra una estrazione envelope audio nei contatori performance
        /// </summary>
        private void RecordAudioEnvelopeExtract(long elapsedMs)
        {
            lock (this._performanceDiagnosticsLock)
            {
                if (this._performanceDiagnostics != null)
                {
                    this._performanceDiagnostics.AudioEnvelopeExtractCalls++;
                    this._performanceDiagnostics.AudioEnvelopeExtractMs += elapsedMs;
                }
            }
        }

        /// <summary>
        /// Scansione lineare differenziale di conferma attorno al punto trovato dalla binaria
        /// Cerca il primo frame dove SSIM col nuovo offset supera SSIM col vecchio offset
        /// per almeno this._daConfig.LinearScanConfirmFrames consecutivi
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="approximateSrc">Punto approssimato dalla binaria in secondi source</param>
        /// <param name="oldOffsetSec">Offset vecchio in secondi</param>
        /// <param name="newOffsetSec">Offset nuovo in secondi</param>
        /// <param name="inverseRatio">Rapporto inverso stretch</param>
        /// <returns>Timestamp source raffinato del crossover in secondi</returns>
        private double LinearScanConfirm(string sourceFile, string langFile, double approximateSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio)
        {
            double result = approximateSrc;
            double scanStart = approximateSrc - this._daConfig.LinearScanWindowSec;
            if (scanStart < 0.0) { scanStart = 0.0; }
            double scanDuration = this._daConfig.LinearScanWindowSec * 2.0;
            List<byte[]> srcFrames;
            double[] sourceTimestampsMs;
            List<byte[]> langOldFrames;
            double[] langOldTimestampsMs;
            List<byte[]> langNewFrames = null;
            double[] langNewTimestampsMs = null;
            double toleranceMs;
            double srcRelMs;
            double targetLangOldMs;
            int nearestOldIdx;
            double nearestOldDistMs;
            double ssimOld;
            // Estrai frame source a fps nativo nella finestra (passthrough)
            this.ExtractDeepSegment(sourceFile, (int)(scanStart * 1000), scanDuration, 0.0, this._geometryCropSourceToFourThree, out srcFrames, out sourceTimestampsMs);
            if (srcFrames.Count < 4 || sourceTimestampsMs.Length < 4) { return result; }

            // Posizione lang col vecchio offset
            double langStartOld = scanStart - oldOffsetSec;
            if (Math.Abs(inverseRatio - 1.0) > 0.0001) { langStartOld = langStartOld * inverseRatio; }
            if (langStartOld < 0.0) { langStartOld = 0.0; }

            // Posizione lang col nuovo offset
            double langStartNew = scanStart - newOffsetSec;
            if (Math.Abs(inverseRatio - 1.0) > 0.0001) { langStartNew = langStartNew * inverseRatio; }
            if (langStartNew < 0.0) { langStartNew = 0.0; }

            // Estrai frame lang con entrambi gli offset (passthrough)
            this.ExtractDeepSegment(langFile, (int)(langStartOld * 1000), scanDuration, 0.0, this._geometryCropLanguageToFourThree, out langOldFrames, out langOldTimestampsMs);
            this.ExtractDeepSegment(langFile, (int)(langStartNew * 1000), scanDuration, 0.0, this._geometryCropLanguageToFourThree, out langNewFrames, out langNewTimestampsMs);

            if (langOldFrames.Count < 4 || langOldTimestampsMs.Length < 4) { return result; }

            int maxIdx = srcFrames.Count;

            // Tolleranza ampia: scanDuration lo fps nativo puo' variare; uso 100ms
            toleranceMs = 100.0;

            // Scorri e trova il primo frame dove old offset smette di funzionare
            // (SSIM_old < 0.5) per almeno this._daConfig.LinearScanConfirmFrames consecutivi
            // Matching per tempo relativo: robusto a VFR
            int consecutiveBad = 0;
            int crossoverIdx = -1;

            for (int i = 0; i < maxIdx; i++)
            {
                srcRelMs = sourceTimestampsMs[i] - sourceTimestampsMs[0];
                targetLangOldMs = langOldTimestampsMs[0] + srcRelMs;
                nearestOldIdx = NearestTimestampIndex(langOldTimestampsMs, targetLangOldMs);
                if (nearestOldIdx < 0 || nearestOldIdx >= langOldFrames.Count) { continue; }
                nearestOldDistMs = Math.Abs(langOldTimestampsMs[nearestOldIdx] - targetLangOldMs);
                if (nearestOldDistMs > toleranceMs) { continue; }

                ssimOld = this.ComputeSsim(srcFrames[i], langOldFrames[nearestOldIdx]);

                if (ssimOld < 0.5)
                {
                    if (consecutiveBad == 0)
                    {
                        crossoverIdx = i;
                    }
                    consecutiveBad++;

                    if (consecutiveBad >= this._daConfig.LinearScanConfirmFrames)
                    {
                        // Posizione reale dal pts del frame sorgente
                        result = sourceTimestampsMs[crossoverIdx] / 1000.0;
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Scansione lineare: crossover confermato a src " + result.ToString("F2", CultureInfo.InvariantCulture) + "s (" + consecutiveBad + " frame old<0.5)");
                        return result;
                    }
                }
                else
                {
                    consecutiveBad = 0;
                    crossoverIdx = -1;
                }
            }

            // Se non confermato, usa il punto della binaria
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "    Scansione lineare: conferma non raggiunta, uso punto binaria");
            return result;
        }

        #endregion

        #region Metodi privati — Fase 4: Verifica globale

        /// <summary>
        /// Verifica l'allineamento globale dopo aver applicato le regioni e le operazioni
        /// </summary>
        /// <param name="sourceFile">Percorso file source</param>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="regions">Regioni con offset</param>
        /// <param name="operations">Operazioni di edit</param>
        /// <param name="inverseRatio">Rapporto inverso stretch</param>
        /// <param name="sourceDurationMs">Durata source in ms</param>
        /// <param name="baselineMse">MSE baseline calcolato (output)</param>
        /// <param name="verification">Diagnostica della verifica globale (output)</param>
        /// <returns>True se la verifica ha successo</returns>
        private bool VerifyGlobal(string sourceFile, string langFile, List<OffsetRegion> regions, List<EditOperation> operations, double inverseRatio, int sourceDurationMs, out double baselineMse, out DeepAnalysisGlobalVerificationDiagnostic verification)
        {
            return this._globalVerifier.Verify(sourceFile, langFile, regions, operations, inverseRatio, sourceDurationMs, out baselineMse, out verification);
        }

        /// <summary>
        /// Calcola MSE per un punto della verifica globale
        /// </summary>
        private bool TryComputeGlobalPointMse(string sourceFile, string langFile, List<OffsetRegion> regions, double srcPointMs, double inverseRatio, out double mse)
        {
            return this._visualFrameAnalyzer.TryComputeGlobalPointMse(sourceFile, langFile, regions, srcPointMs, inverseRatio, this._daConfig.CoarseFps, this._geometryCropSourceToFourThree, this._geometryCropLanguageToFourThree, out mse);
        }

        /// <summary>
        /// Converte regioni interne in DTO diagnostici pubblici
        /// </summary>
        /// <param name="regions">Regioni interne</param>
        /// <returns>Lista diagnostica regioni</returns>
        private List<DeepAnalysisRegionDiagnostic> BuildRegionDiagnostics(List<OffsetRegion> regions)
        {
            return this._regionMapper.BuildDiagnostics(regions);
        }

        /// <summary>
        /// Wrapper diagnostico per estrazioni frame DeepAnalysis
        /// </summary>
        private void ExtractDeepSegment(string filePath, int startMs, double durationSec, double targetFps, bool geometryCropToFourThree, out List<byte[]> frames, out double[] timestampsMs)
        {
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            base.ExtractSegment(filePath, startMs, durationSec, targetFps, geometryCropToFourThree, out frames, out timestampsMs);
            stopwatch.Stop();

            lock (this._performanceDiagnosticsLock)
            {
                if (this._performanceDiagnostics != null)
                {
                    this._performanceDiagnostics.SegmentExtractCalls++;
                    this._performanceDiagnostics.SegmentExtractMs += stopwatch.ElapsedMilliseconds;
                }
            }
        }

        /// <summary>
        /// Verifica locale una transizione confrontando vecchio e nuovo offset prima/dopo il breakpoint
        /// </summary>
        private DeepAnalysisLocalVerificationDiagnostic VerifyTransitionLocal(string sourceFile, string langFile, double crossoverSrcSec, double oldOffsetSec, double newOffsetSec, double inverseRatio)
        {
            DeepAnalysisLocalVerificationDiagnostic result = new DeepAnalysisLocalVerificationDiagnostic();
            double offsetDeltaSec = newOffsetSec - oldOffsetSec;
            double transitionDurationSec = Math.Abs(offsetDeltaSec);
            bool insertSilenceTransition = this._currentAnalysisUsesTimelineMap && offsetDeltaSec > 0.0;
            double beforeSrcSec = crossoverSrcSec - 1.5;
            double afterSrcSec = crossoverSrcSec + 1.5;
            double forwardSrcSec = crossoverSrcSec + Math.Max(4.0, transitionDurationSec + 2.0);
            double oldTotal;
            double newTotal;
            double forwardImprovementRatio = 0.0;
            if (beforeSrcSec < 0.0) { beforeSrcSec = 0.0; }
            if (insertSilenceTransition)
            {
                // In INSERT_SILENCE il crossover e' il punto operativo: il nuovo offset diventa verificabile solo dopo la durata inserita.
                afterSrcSec = crossoverSrcSec + transitionDurationSec + 2.0;
                forwardSrcSec = crossoverSrcSec + transitionDurationSec + Math.Max(8.0, transitionDurationSec + 2.0);
            }

            result.BeforeSrcSec = beforeSrcSec;
            result.AfterSrcSec = afterSrcSec;
            result.ForwardSrcSec = forwardSrcSec;
            result.BeforeOldMse = this.ComputeLocalMseAt(sourceFile, langFile, beforeSrcSec, oldOffsetSec, inverseRatio);
            result.BeforeNewMse = this.ComputeLocalMseAt(sourceFile, langFile, beforeSrcSec, newOffsetSec, inverseRatio);
            result.AfterOldMse = this.ComputeLocalMseAt(sourceFile, langFile, afterSrcSec, oldOffsetSec, inverseRatio);
            result.AfterNewMse = this.ComputeLocalMseAt(sourceFile, langFile, afterSrcSec, newOffsetSec, inverseRatio);
            result.ForwardOldMse = this.ComputeLocalMseAt(sourceFile, langFile, forwardSrcSec, oldOffsetSec, inverseRatio);
            result.ForwardNewMse = this.ComputeLocalMseAt(sourceFile, langFile, forwardSrcSec, newOffsetSec, inverseRatio);

            if (insertSilenceTransition)
            {
                oldTotal = this.SumValidMse(result.AfterOldMse, result.ForwardOldMse, double.MaxValue);
                newTotal = this.SumValidMse(result.AfterNewMse, result.ForwardNewMse, double.MaxValue);
            }
            else
            {
                oldTotal = this.SumValidMse(result.BeforeOldMse, result.AfterOldMse, result.ForwardOldMse);
                newTotal = this.SumValidMse(result.BeforeNewMse, result.AfterNewMse, result.ForwardNewMse);
            }

            if (newTotal > 0.0)
            {
                result.ImprovementRatio = oldTotal / newTotal;
            }
            if (result.ForwardNewMse > 0.0)
            {
                forwardImprovementRatio = result.ForwardOldMse / result.ForwardNewMse;
            }

            if (this._currentAnalysisUsesTimelineMap)
            {
                if (insertSilenceTransition)
                {
                    result.Verified = (result.BeforeOldMse <= result.BeforeNewMse || result.ImprovementRatio >= 2.0) && (result.AfterNewMse <= result.AfterOldMse * 1.10 || forwardImprovementRatio >= 1.50) && (result.ImprovementRatio >= 1.05 || forwardImprovementRatio >= 1.50);
                }
                else
                {
                    result.Verified = result.AfterNewMse <= result.AfterOldMse * 1.02 && result.ForwardNewMse < result.ForwardOldMse && result.ImprovementRatio >= 1.05;
                }
            }
            else
            {
                result.Verified = result.BeforeOldMse <= result.BeforeNewMse && result.AfterNewMse < result.AfterOldMse && result.ForwardNewMse < result.ForwardOldMse;
            }

            return result;
        }

        /// <summary>
        /// Calcola MSE locale per un punto source e un offset candidato
        /// </summary>
        private double ComputeLocalMseAt(string sourceFile, string langFile, double srcSec, double offsetSec, double inverseRatio)
        {
            return this._visualFrameAnalyzer.ComputeLocalMseAt(sourceFile, langFile, srcSec, offsetSec, inverseRatio, this._daConfig.CoarseFps, this._geometryCropSourceToFourThree, this._geometryCropLanguageToFourThree);
        }

        /// <summary>
        /// Somma MSE validi ignorando valori non calcolabili
        /// </summary>
        private double SumValidMse(double a, double b, double c)
        {
            double result = 0.0;
            if (a < double.MaxValue) { result += a; }
            if (b < double.MaxValue) { result += b; }
            if (c < double.MaxValue) { result += c; }
            return result;
        }

        #endregion
    }
}
