using RemuxForge.Core.Media.Mkv;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Costruisce una mappa timeline-first da anchor audio distribuiti su una traccia comune
    /// </summary>
    public class DeepTimelineAnchorMapper
    {
        #region Delegati

        /// <summary>
        /// Cerca un anchor visuale attorno a un centro source
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="sourceCenterSec">Centro finestra source</param>
        /// <param name="searchRadiusMs">Raggio ricerca offset</param>
        /// <param name="searchStepMs">Passo ricerca offset</param>
        /// <param name="anchor">Anchor diagnostico prodotto</param>
        /// <returns>True se il probe produce un anchor valido o diagnosticabile</returns>
        public delegate bool VisualAnchorProbe(string sourceFile, string langFile, double sourceCenterSec, int searchRadiusMs, int searchStepMs, out DeepAnalysisTimelineAnchorDiagnostic anchor);

        #endregion

        #region Costanti

        private const int WINDOW_MS = 50;
        private const double ANCHOR_WINDOW_SEC = 80.0;
        private const double ANCHOR_STEP_SEC = 60.0;
        private const double VIDEO_ANCHOR_STEP_SEC = 30.0;
        private const int MIN_SEARCH_RADIUS_MS = 20000;
        private const int MAX_SEARCH_RADIUS_MS = 120000;
        private const int SEARCH_RADIUS_PADDING_MS = 10000;
        private const int SEARCH_STEP_MS = 50;
        private const int VIDEO_SEARCH_STEP_MS = 50;
        private const double MIN_SCORE = 0.56;
        private const double MIN_MARGIN = 0.045;
        private const double WEAK_MIN_SCORE = 0.82;
        private const double WEAK_MIN_MARGIN = 0.010;
        private const int PLATEAU_TOLERANCE_MS = 100;
        private const int MIN_TIMELINE_TRANSITION_MS = 250;
        private const double DENSE_ANCHOR_STEP_SEC = 10.0;
        private const double DENSE_ANCHOR_WINDOW_SEC = 30.0;
        private const int MIN_ACCEPTED_ANCHORS = 5;
        private const int MIN_PLATEAU_ANCHORS = 1;
        private const int MIN_VIDEO_PLATEAU_ANCHORS = 2;
        private const double DENSE_VIDEO_ANCHOR_STEP_SEC = 5.0;
        private const double LEADING_BOOTSTRAP_MAX_SEC = 90.0;
        private const double LEADING_BOOTSTRAP_STEP_SEC = 20.0;
        private const double LEADING_BOOTSTRAP_MIN_SCORE = 0.75;
        private const double LEADING_BOOTSTRAP_MIN_MARGIN = 0.20;
        private const int MAX_PARALLEL_VIDEO_ANCHORS = 4;

        #endregion

        #region Variabili di classe

        private readonly string _mkvMergePath;
        private readonly DeepAudioEnvelopeService _audioEnvelopeService;
        private readonly VisualAnchorProbe _visualAnchorProbe;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="mkvMergePath">Percorso mkvmerge</param>
        /// <param name="audioEnvelopeService">Servizio envelope audio</param>
        /// <param name="visualAnchorProbe">Probe anchor visuale</param>
        public DeepTimelineAnchorMapper(string mkvMergePath, DeepAudioEnvelopeService audioEnvelopeService, VisualAnchorProbe visualAnchorProbe)
        {
            this._mkvMergePath = mkvMergePath;
            this._audioEnvelopeService = audioEnvelopeService;
            this._visualAnchorProbe = visualAnchorProbe;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Prova a costruire una timeline a offset costanti usando una traccia audio comune
        /// </summary>
        public DeepTimelineMapResult Build(string sourceFile, string langFile, int sourceDurationMs)
        {
            DeepTimelineMapResult result = new DeepTimelineMapResult();
            AudioTrackRef sourceTrack;
            AudioTrackRef languageTrack;
            MkvFileInfo sourceInfo;
            MkvFileInfo languageInfo;
            double sourceDurationSec = sourceDurationMs / 1000.0;
            double languageDurationSec;
            int searchRadiusMs;
            double[] sourceEnvelope;
            double[] languageEnvelope;
            List<DeepAnalysisTimelineAnchorDiagnostic> acceptedAnchors;
            result.Diagnostic.Status = "Skipped";

            if (string.IsNullOrEmpty(this._mkvMergePath) || !File.Exists(this._mkvMergePath))
            {
                result.RejectReason = "mkvmerge non disponibile per timeline audio";
                result.Diagnostic.RejectReason = result.RejectReason;
                return result;
            }

            sourceInfo = new MkvToolsService(this._mkvMergePath).GetFileInfo(sourceFile);
            languageInfo = new MkvToolsService(this._mkvMergePath).GetFileInfo(langFile);
            if (sourceInfo == null || languageInfo == null)
            {
                result.RejectReason = "metadata audio non leggibili";
                result.Diagnostic.RejectReason = result.RejectReason;
                return result;
            }

            languageDurationSec = languageInfo.ContainerDurationNs > 0 ? languageInfo.ContainerDurationNs / 1000000000.0 : sourceDurationSec;
            searchRadiusMs = this.ResolveSearchRadiusMs(sourceDurationSec, languageDurationSec);
            if (sourceDurationSec < ANCHOR_WINDOW_SEC * 2.0 || languageDurationSec < ANCHOR_WINDOW_SEC * 2.0)
            {
                result.RejectReason = "durata insufficiente per timeline";
                result.Diagnostic.RejectReason = result.RejectReason;
                return result;
            }

            result.Diagnostic.Status = "Running";

            if (this.TryFindCommonAudioTrack(sourceInfo, languageInfo, out sourceTrack, out languageTrack))
            {
                result.Diagnostic.AnchorMode = "audio-common";
                result.Diagnostic.TrackLanguage = sourceTrack.Language;
                result.Diagnostic.SourceAudioStreamIndex = sourceTrack.AudioStreamIndex;
                result.Diagnostic.LanguageAudioStreamIndex = languageTrack.AudioStreamIndex;
                result.Diagnostic.SourceTrackName = sourceTrack.Name;
                result.Diagnostic.LanguageTrackName = languageTrack.Name;

                sourceEnvelope = this._audioEnvelopeService.Extract(sourceFile, 0.0, sourceDurationSec, WINDOW_MS, sourceTrack.AudioStreamIndex);
                languageEnvelope = this._audioEnvelopeService.Extract(langFile, 0.0, languageDurationSec, WINDOW_MS, languageTrack.AudioStreamIndex);
                if (sourceEnvelope == null || languageEnvelope == null)
                {
                    result.RejectReason = "estrazione envelope timeline fallita";
                    result.Diagnostic.Status = "Rejected";
                    result.Diagnostic.RejectReason = result.RejectReason;
                    return result;
                }

                this.BuildAudioAnchors(sourceEnvelope, languageEnvelope, sourceDurationSec, searchRadiusMs, result.Diagnostic);
                this.DensifyAudioTransitionAnchors(sourceEnvelope, languageEnvelope, sourceDurationSec, searchRadiusMs, result.Diagnostic);
                this.AddLeadingVisualBootstrapAnchors(sourceFile, langFile, sourceDurationSec, searchRadiusMs, result.Diagnostic);
            }
            else
            {
                result.Diagnostic.AnchorMode = "video";
                result.Diagnostic.TrackLanguage = "video";
                if (!this.BuildVisualAnchors(sourceFile, langFile, sourceDurationSec, searchRadiusMs, result.Diagnostic))
                {
                    result.RejectReason = "anchor video insufficienti";
                    result.Diagnostic.Status = "Rejected";
                    result.Diagnostic.RejectReason = result.RejectReason;
                    return result;
                }

                this.DensifyVisualTransitionAnchors(sourceFile, langFile, sourceDurationSec, searchRadiusMs, result.Diagnostic);
            }

            acceptedAnchors = this.GetAcceptedAnchors(result.Diagnostic.Anchors);
            result.Diagnostic.AcceptedAnchorCount = acceptedAnchors.Count;
            result.Diagnostic.AnchorCount = result.Diagnostic.Anchors.Count;
            result.Diagnostic.AverageAcceptedScore = this.ComputeAverageScore(acceptedAnchors);

            if (acceptedAnchors.Count < MIN_ACCEPTED_ANCHORS)
            {
                result.RejectReason = "anchor timeline insufficienti";
                result.Diagnostic.Status = "Rejected";
                result.Diagnostic.RejectReason = result.RejectReason;
                return result;
            }

            this.BuildPlateaus(acceptedAnchors, result.Diagnostic);
            result.Diagnostic.PlateauCount = result.Diagnostic.Plateaus.Count;
            result.Regions = this.BuildRegionsFromPlateaus(result.Diagnostic.Plateaus, sourceDurationSec);

            if (result.Regions.Count == 0)
            {
                result.RejectReason = "nessun plateau timeline valido";
                result.Diagnostic.Status = "Rejected";
                result.Diagnostic.RejectReason = result.RejectReason;
                return result;
            }

            result.Success = true;
            result.Diagnostic.Status = "Accepted";
            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Cerca una traccia audio con lingua comune tra source e language
        /// </summary>
        /// <param name="sourceInfo">Metadata source</param>
        /// <param name="languageInfo">Metadata language</param>
        /// <param name="sourceTrack">Traccia audio source trovata</param>
        /// <param name="languageTrack">Traccia audio language trovata</param>
        /// <returns>True se esiste una lingua audio comune esplicita</returns>
        private bool TryFindCommonAudioTrack(MkvFileInfo sourceInfo, MkvFileInfo languageInfo, out AudioTrackRef sourceTrack, out AudioTrackRef languageTrack)
        {
            bool result = false;
            List<AudioTrackRef> sourceAudio = this.BuildAudioTrackRefs(sourceInfo);
            List<AudioTrackRef> languageAudio = this.BuildAudioTrackRefs(languageInfo);

            sourceTrack = null;
            languageTrack = null;

            // Le lingue non definite non sono abbastanza affidabili per dichiarare una traccia comune
            for (int s = 0; s < sourceAudio.Count; s++)
            {
                if (sourceAudio[s].Language.Length == 0 || string.Equals(sourceAudio[s].Language, "und", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (int l = 0; l < languageAudio.Count; l++)
                {
                    if (string.Equals(sourceAudio[s].Language, languageAudio[l].Language, StringComparison.OrdinalIgnoreCase))
                    {
                        sourceTrack = sourceAudio[s];
                        languageTrack = languageAudio[l];
                        result = true;
                        return result;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Costruisce riferimenti audio con stream index ffmpeg e lingua normalizzata
        /// </summary>
        /// <param name="info">Metadata file</param>
        /// <returns>Lista tracce audio</returns>
        private List<AudioTrackRef> BuildAudioTrackRefs(MkvFileInfo info)
        {
            List<AudioTrackRef> result = new List<AudioTrackRef>();
            int audioIndex = 0;
            for (int i = 0; i < info.Tracks.Count; i++)
            {
                TrackInfo track = info.Tracks[i];
                if (!string.Equals(track.Type, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // AudioStreamIndex e' l'indice tra le sole tracce audio, non l'ID Matroska globale
                AudioTrackRef audioTrack = new AudioTrackRef();
                audioTrack.AudioStreamIndex = audioIndex;
                audioTrack.Language = this.NormalizeLanguage(track.Language, track.LanguageIetf);
                audioTrack.Name = track.Name;
                result.Add(audioTrack);
                audioIndex++;
            }

            return result;
        }

        /// <summary>
        /// Normalizza lingua Matroska/IETF a codice base confrontabile
        /// </summary>
        /// <param name="language">Lingua Matroska</param>
        /// <param name="languageIetf">Lingua IETF</param>
        /// <returns>Codice lingua normalizzato</returns>
        private string NormalizeLanguage(string language, string languageIetf)
        {
            string result = language != null ? language.Trim().ToLowerInvariant() : "";
            int dashIndex;

            if (result.Length == 0 && languageIetf != null)
            {
                result = languageIetf.Trim().ToLowerInvariant();
                dashIndex = result.IndexOf("-", StringComparison.Ordinal);
                if (dashIndex > 0)
                {
                    result = result.Substring(0, dashIndex);
                }
            }

            return result;
        }

        /// <summary>
        /// Calcola il raggio ricerca offset in base alla differenza durata
        /// </summary>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="languageDurationSec">Durata language</param>
        /// <returns>Raggio ricerca in millisecondi</returns>
        private int ResolveSearchRadiusMs(double sourceDurationSec, double languageDurationSec)
        {
            int result = MIN_SEARCH_RADIUS_MS;
            int durationDeltaMs = (int)Math.Round(Math.Abs(languageDurationSec - sourceDurationSec) * 1000.0);

            if (durationDeltaMs + SEARCH_RADIUS_PADDING_MS > result)
            {
                result = durationDeltaMs + SEARCH_RADIUS_PADDING_MS;
            }

            if (result > MAX_SEARCH_RADIUS_MS)
            {
                result = MAX_SEARCH_RADIUS_MS;
            }

            return result;
        }

        /// <summary>
        /// Costruisce anchor audio distribuiti lungo il file
        /// </summary>
        /// <param name="sourceEnvelope">Envelope source</param>
        /// <param name="languageEnvelope">Envelope language</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void BuildAudioAnchors(double[] sourceEnvelope, double[] languageEnvelope, double sourceDurationSec, int searchRadiusMs, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            double centerSec = ANCHOR_WINDOW_SEC / 2.0;
            double endCenterSec = sourceDurationSec - (ANCHOR_WINDOW_SEC / 2.0);

            // Anchor lunghi e sovrapposti riducono falsi positivi su silenzi o sezioni ripetitive
            while (centerSec <= endCenterSec)
            {
                diagnostic.Anchors.Add(this.BuildAnchor(sourceEnvelope, languageEnvelope, centerSec, searchRadiusMs));
                centerSec += ANCHOR_STEP_SEC;
            }
        }

        /// <summary>
        /// Costruisce anchor video quando non esiste audio comune affidabile
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        /// <returns>True se la scansione visuale e' stata avviata</returns>
        private bool BuildVisualAnchors(string sourceFile, string langFile, double sourceDurationSec, int searchRadiusMs, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            bool result = false;
            double centerSec = ANCHOR_WINDOW_SEC / 2.0;
            double endCenterSec = sourceDurationSec - (ANCHOR_WINDOW_SEC / 2.0);
            List<double> centers = new List<double>();
            DeepAnalysisTimelineAnchorDiagnostic[] anchors;
            if (this._visualAnchorProbe == null)
            {
                return result;
            }

            while (centerSec <= endCenterSec)
            {
                // I centri visuali sono piu' fitti degli audio per compensare ambiguita' su anime/VFR
                centers.Add(centerSec);
                centerSec += VIDEO_ANCHOR_STEP_SEC;
            }

            anchors = this.BuildVisualAnchorsParallel(sourceFile, langFile, centers, searchRadiusMs, "anchor video non conclusivo");
            diagnostic.Anchors.AddRange(anchors);

            result = true;
            return result;
        }

        /// <summary>
        /// Aggiunge anchor visuali iniziali quando l'audio comune non copre bene l'inizio
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void AddLeadingVisualBootstrapAnchors(string sourceFile, string langFile, double sourceDurationSec, int searchRadiusMs, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            List<double> centers = new List<double>();
            DeepAnalysisTimelineAnchorDiagnostic[] bootstrapAnchors;
            List<DeepAnalysisTimelineAnchorDiagnostic> acceptedBootstrapAnchors = new List<DeepAnalysisTimelineAnchorDiagnostic>();

            if (this._visualAnchorProbe == null || this.HasAcceptedAnchorBefore(diagnostic.Anchors, LEADING_BOOTSTRAP_MAX_SEC))
            {
                return;
            }

            // Bootstrap limitato all'inizio: serve a evitare regioni iniziali troppo larghe prima del primo anchor audio
            double centerSec = LEADING_BOOTSTRAP_STEP_SEC;
            while (centerSec <= LEADING_BOOTSTRAP_MAX_SEC && centerSec < sourceDurationSec - 2.0)
            {
                centers.Add(centerSec);
                centerSec += LEADING_BOOTSTRAP_STEP_SEC;
            }

            if (centers.Count == 0)
            {
                return;
            }

            bootstrapAnchors = this.BuildVisualAnchorsParallel(sourceFile, langFile, centers, searchRadiusMs, "bootstrap video iniziale non conclusivo");
            for (int i = 0; i < bootstrapAnchors.Length; i++)
            {
                // Per bootstrap servono soglie piu' alte: un errore all'inizio sposta tutti i cut successivi
                if (bootstrapAnchors[i].Accepted && bootstrapAnchors[i].Score >= LEADING_BOOTSTRAP_MIN_SCORE && bootstrapAnchors[i].Margin >= LEADING_BOOTSTRAP_MIN_MARGIN)
                {
                    acceptedBootstrapAnchors.Add(bootstrapAnchors[i]);
                }
            }

            if (acceptedBootstrapAnchors.Count < MIN_VIDEO_PLATEAU_ANCHORS)
            {
                return;
            }

            diagnostic.Anchors.AddRange(acceptedBootstrapAnchors);
            diagnostic.Anchors.Sort(delegate (DeepAnalysisTimelineAnchorDiagnostic left, DeepAnalysisTimelineAnchorDiagnostic right)
            {
                return left.SourceCenterSec.CompareTo(right.SourceCenterSec);
            });
        }

        /// <summary>
        /// Aggiunge anchor video densi tra due anchor accettati con offset diverso
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void DensifyVisualTransitionAnchors(string sourceFile, string langFile, double sourceDurationSec, int searchRadiusMs, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            List<DeepAnalysisTimelineAnchorDiagnostic> baseAccepted = this.GetAcceptedAnchors(diagnostic.Anchors);
            List<double> centers = new List<double>();
            DeepAnalysisTimelineAnchorDiagnostic[] denseAnchors;
            if (this._visualAnchorProbe == null || baseAccepted.Count < 2)
            {
                return;
            }

            for (int i = 0; i < baseAccepted.Count - 1; i++)
            {
                // Densifichiamo solo dove c'e' una vera transizione di offset da localizzare
                if (Math.Abs(baseAccepted[i + 1].OffsetMs - baseAccepted[i].OffsetMs) < MIN_TIMELINE_TRANSITION_MS)
                {
                    continue;
                }

                double centerSec = baseAccepted[i].SourceCenterSec + DENSE_VIDEO_ANCHOR_STEP_SEC;
                while (centerSec < baseAccepted[i + 1].SourceCenterSec)
                {
                    if (centerSec > ANCHOR_WINDOW_SEC / 2.0 && centerSec < sourceDurationSec - (ANCHOR_WINDOW_SEC / 2.0) && !this.HasAnchorNear(diagnostic.Anchors, centerSec) && !this.HasCenterNear(centers, centerSec))
                    {
                        centers.Add(centerSec);
                    }

                    centerSec += DENSE_VIDEO_ANCHOR_STEP_SEC;
                }
            }

            if (centers.Count == 0)
            {
                return;
            }

            centers.Sort();
            denseAnchors = this.BuildVisualAnchorsParallel(sourceFile, langFile, centers, searchRadiusMs, "anchor video dense non conclusivo");
            diagnostic.Anchors.AddRange(denseAnchors);
            diagnostic.Anchors.Sort(delegate (DeepAnalysisTimelineAnchorDiagnostic left, DeepAnalysisTimelineAnchorDiagnostic right)
            {
                return left.SourceCenterSec.CompareTo(right.SourceCenterSec);
            });
        }

        /// <summary>
        /// Esegue probe visuali in parallelo su una lista di centri
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="centers">Centri source da analizzare</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="rejectReason">Motivo da assegnare agli anchor non conclusivi</param>
        /// <returns>Array anchor ordinato come i centri in input</returns>
        private DeepAnalysisTimelineAnchorDiagnostic[] BuildVisualAnchorsParallel(string sourceFile, string langFile, List<double> centers, int searchRadiusMs, string rejectReason)
        {
            DeepAnalysisTimelineAnchorDiagnostic[] result = new DeepAnalysisTimelineAnchorDiagnostic[centers.Count];
            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = MAX_PARALLEL_VIDEO_ANCHORS;

            Parallel.For(0, centers.Count, options, delegate (int i)
            {
                // Ogni slot dell'array e' scritto da una sola iterazione, quindi non serve lock
                DeepAnalysisTimelineAnchorDiagnostic anchor;
                if (this._visualAnchorProbe(sourceFile, langFile, centers[i], searchRadiusMs, VIDEO_SEARCH_STEP_MS, out anchor))
                {
                    result[i] = anchor;
                }
                else
                {
                    anchor = new DeepAnalysisTimelineAnchorDiagnostic();
                    anchor.SourceCenterSec = centers[i];
                    anchor.Accepted = false;
                    anchor.RejectReason = rejectReason;
                    result[i] = anchor;
                }
            });

            return result;
        }

        /// <summary>
        /// Verifica se un centro candidato e' gia' presente nella lista temporanea
        /// </summary>
        /// <param name="centers">Centri gia' pianificati</param>
        /// <param name="centerSec">Centro da verificare</param>
        /// <returns>True se esiste un centro entro mezzo secondo</returns>
        private bool HasCenterNear(List<double> centers, double centerSec)
        {
            bool result = false;
            for (int i = 0; i < centers.Count; i++)
            {
                if (Math.Abs(centers[i] - centerSec) < 0.5)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Verifica se esiste gia' un anchor accettato prima del timestamp indicato
        /// </summary>
        /// <param name="anchors">Anchor diagnostici</param>
        /// <param name="sourceCenterSec">Limite source</param>
        /// <returns>True se un anchor accettato precede il limite</returns>
        private bool HasAcceptedAnchorBefore(List<DeepAnalysisTimelineAnchorDiagnostic> anchors, double sourceCenterSec)
        {
            bool result = false;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Accepted && anchors[i].SourceCenterSec < sourceCenterSec)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Costruisce un anchor audio usando la finestra standard
        /// </summary>
        /// <param name="sourceEnvelope">Envelope source</param>
        /// <param name="languageEnvelope">Envelope language</param>
        /// <param name="sourceCenterSec">Centro source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <returns>Anchor diagnostico</returns>
        private DeepAnalysisTimelineAnchorDiagnostic BuildAnchor(double[] sourceEnvelope, double[] languageEnvelope, double sourceCenterSec, int searchRadiusMs)
        {
            return this.BuildAnchor(sourceEnvelope, languageEnvelope, sourceCenterSec, searchRadiusMs, ANCHOR_WINDOW_SEC);
        }

        /// <summary>
        /// Costruisce un anchor audio cercando l'offset con migliore correlazione differenziale
        /// </summary>
        /// <param name="sourceEnvelope">Envelope source</param>
        /// <param name="languageEnvelope">Envelope language</param>
        /// <param name="sourceCenterSec">Centro source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="windowSec">Durata finestra</param>
        /// <returns>Anchor diagnostico</returns>
        private DeepAnalysisTimelineAnchorDiagnostic BuildAnchor(double[] sourceEnvelope, double[] languageEnvelope, double sourceCenterSec, int searchRadiusMs, double windowSec)
        {
            DeepAnalysisTimelineAnchorDiagnostic result = new DeepAnalysisTimelineAnchorDiagnostic();
            int windowCount = (int)Math.Round((windowSec * 1000.0) / WINDOW_MS);
            int sourceStartIndex = (int)Math.Round(((sourceCenterSec - (windowSec / 2.0)) * 1000.0) / WINDOW_MS);
            int offsetMs = -searchRadiusMs;
            int bestOffsetMs = 0;
            double bestScore = 0.0;
            double secondScore = 0.0;
            result.SourceCenterSec = sourceCenterSec;

            if (sourceStartIndex < 0 || sourceStartIndex + windowCount >= sourceEnvelope.Length)
            {
                result.RejectReason = "finestra source fuori range";
                return result;
            }

            while (offsetMs <= searchRadiusMs)
            {
                // Offset positivo significa source piu' avanti di language: l'indice language arretra
                int languageStartIndex = sourceStartIndex - (offsetMs / WINDOW_MS);
                if (languageStartIndex >= 0 && languageStartIndex + windowCount < languageEnvelope.Length)
                {
                    double score = this.ScoreDifferentialWindow(sourceEnvelope, languageEnvelope, sourceStartIndex, languageStartIndex, windowCount);
                    if (score > bestScore)
                    {
                        secondScore = bestScore;
                        bestScore = score;
                        bestOffsetMs = offsetMs;
                    }
                    else if (score > secondScore)
                    {
                        secondScore = score;
                    }
                }

                offsetMs += SEARCH_STEP_MS;
            }

            result.OffsetMs = bestOffsetMs;
            result.Score = bestScore;
            result.Margin = bestScore - secondScore;
            result.Accepted = (bestScore >= MIN_SCORE && result.Margin >= MIN_MARGIN) || (bestScore >= WEAK_MIN_SCORE && result.Margin >= WEAK_MIN_MARGIN);
            if (!result.Accepted)
            {
                result.RejectReason = "score/margine bassi";
            }

            return result;
        }

        /// <summary>
        /// Aggiunge anchor audio densi tra plateau con offset differente
        /// </summary>
        /// <param name="sourceEnvelope">Envelope source</param>
        /// <param name="languageEnvelope">Envelope language</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <param name="searchRadiusMs">Raggio ricerca</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void DensifyAudioTransitionAnchors(double[] sourceEnvelope, double[] languageEnvelope, double sourceDurationSec, int searchRadiusMs, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            List<DeepAnalysisTimelineAnchorDiagnostic> baseAccepted = this.GetAcceptedAnchors(diagnostic.Anchors);
            List<DeepAnalysisTimelineAnchorDiagnostic> denseAnchors = new List<DeepAnalysisTimelineAnchorDiagnostic>();

            for (int i = 0; i < baseAccepted.Count - 1; i++)
            {
                // La densificazione serve solo a definire meglio dove cambia offset
                if (Math.Abs(baseAccepted[i + 1].OffsetMs - baseAccepted[i].OffsetMs) < MIN_TIMELINE_TRANSITION_MS)
                {
                    continue;
                }

                double centerSec = baseAccepted[i].SourceCenterSec + DENSE_ANCHOR_STEP_SEC;
                while (centerSec < baseAccepted[i + 1].SourceCenterSec)
                {
                    if (centerSec > DENSE_ANCHOR_WINDOW_SEC && centerSec < sourceDurationSec - DENSE_ANCHOR_WINDOW_SEC && !this.HasAnchorNear(diagnostic.Anchors, centerSec))
                    {
                        denseAnchors.Add(this.BuildAnchor(sourceEnvelope, languageEnvelope, centerSec, searchRadiusMs, DENSE_ANCHOR_WINDOW_SEC));
                    }

                    centerSec += DENSE_ANCHOR_STEP_SEC;
                }
            }

            for (int i = 0; i < denseAnchors.Count; i++)
            {
                diagnostic.Anchors.Add(denseAnchors[i]);
            }

            diagnostic.Anchors.Sort(delegate (DeepAnalysisTimelineAnchorDiagnostic left, DeepAnalysisTimelineAnchorDiagnostic right)
            {
                return left.SourceCenterSec.CompareTo(right.SourceCenterSec);
            });
        }

        /// <summary>
        /// Verifica se esiste gia' un anchor vicino a un centro
        /// </summary>
        /// <param name="anchors">Anchor esistenti</param>
        /// <param name="centerSec">Centro da verificare</param>
        /// <returns>True se un anchor e' gia' entro mezzo secondo</returns>
        private bool HasAnchorNear(List<DeepAnalysisTimelineAnchorDiagnostic> anchors, double centerSec)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                if (Math.Abs(anchors[i].SourceCenterSec - centerSec) < 0.5)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calcola correlazione normalizzata sulle differenze di envelope
        /// </summary>
        /// <param name="sourceEnvelope">Envelope source</param>
        /// <param name="languageEnvelope">Envelope language</param>
        /// <param name="sourceStartIndex">Indice iniziale source</param>
        /// <param name="languageStartIndex">Indice iniziale language</param>
        /// <param name="count">Numero campioni finestra</param>
        /// <returns>Score normalizzato 0..1</returns>
        private double ScoreDifferentialWindow(double[] sourceEnvelope, double[] languageEnvelope, int sourceStartIndex, int languageStartIndex, int count)
        {
            double result = 0.0;
            double sourceMean = 0.0;
            double languageMean = 0.0;
            double sourceValue;
            double languageValue;
            double numerator = 0.0;
            double sourceNorm = 0.0;
            double languageNorm = 0.0;
            int safeCount = count - 1;

            // Usiamo il differenziale dell'envelope per ridurre differenze di mix/volume tra lingue
            for (int i = 1; i < count; i++)
            {
                sourceMean += Math.Abs(sourceEnvelope[sourceStartIndex + i] - sourceEnvelope[sourceStartIndex + i - 1]);
                languageMean += Math.Abs(languageEnvelope[languageStartIndex + i] - languageEnvelope[languageStartIndex + i - 1]);
            }

            sourceMean = sourceMean / safeCount;
            languageMean = languageMean / safeCount;

            for (int i = 1; i < count; i++)
            {
                sourceValue = Math.Abs(sourceEnvelope[sourceStartIndex + i] - sourceEnvelope[sourceStartIndex + i - 1]) - sourceMean;
                languageValue = Math.Abs(languageEnvelope[languageStartIndex + i] - languageEnvelope[languageStartIndex + i - 1]) - languageMean;
                numerator += sourceValue * languageValue;
                sourceNorm += sourceValue * sourceValue;
                languageNorm += languageValue * languageValue;
            }

            if (sourceNorm <= 0.0000001 || languageNorm <= 0.0000001)
            {
                return result;
            }

            result = numerator / Math.Sqrt(sourceNorm * languageNorm);
            result = (result + 1.0) / 2.0;
            if (result < 0.0) { result = 0.0; }
            if (result > 1.0) { result = 1.0; }

            return result;
        }

        /// <summary>
        /// Filtra gli anchor accettati
        /// </summary>
        /// <param name="anchors">Anchor diagnostici</param>
        /// <returns>Solo anchor accettati</returns>
        private List<DeepAnalysisTimelineAnchorDiagnostic> GetAcceptedAnchors(List<DeepAnalysisTimelineAnchorDiagnostic> anchors)
        {
            List<DeepAnalysisTimelineAnchorDiagnostic> result = new List<DeepAnalysisTimelineAnchorDiagnostic>();

            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Accepted)
                {
                    result.Add(anchors[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Calcola lo score medio di una lista anchor
        /// </summary>
        /// <param name="anchors">Anchor accettati</param>
        /// <returns>Score medio</returns>
        private double ComputeAverageScore(List<DeepAnalysisTimelineAnchorDiagnostic> anchors)
        {
            double result = 0.0;
            if (anchors.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                result += anchors[i].Score;
            }

            return result / anchors.Count;
        }

        /// <summary>
        /// Raggruppa anchor consecutivi in plateau a offset stabile
        /// </summary>
        /// <param name="anchors">Anchor accettati ordinati</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void BuildPlateaus(List<DeepAnalysisTimelineAnchorDiagnostic> anchors, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            List<DeepAnalysisTimelineAnchorDiagnostic> current = new List<DeepAnalysisTimelineAnchorDiagnostic>();
            int currentOffset = anchors[0].OffsetMs;

            for (int i = 0; i < anchors.Count; i++)
            {
                // Quando l'offset si allontana dalla mediana corrente, il plateau e' chiuso
                if (current.Count > 0 && Math.Abs(anchors[i].OffsetMs - currentOffset) > PLATEAU_TOLERANCE_MS)
                {
                    this.AddPlateau(current, diagnostic);
                    current.Clear();
                }

                current.Add(anchors[i]);
                currentOffset = this.MedianOffset(current);
            }

            if (current.Count > 0)
            {
                this.AddPlateau(current, diagnostic);
            }

            this.MergeAdjacentPlateaus(diagnostic);
        }

        /// <summary>
        /// Aggiunge un plateau se contiene abbastanza anchor per la modalita' corrente
        /// </summary>
        /// <param name="anchors">Anchor del plateau</param>
        /// <param name="diagnostic">Diagnostica timeline da aggiornare</param>
        private void AddPlateau(List<DeepAnalysisTimelineAnchorDiagnostic> anchors, DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            int minAnchors = string.Equals(diagnostic.AnchorMode, "video", StringComparison.Ordinal) ? MIN_VIDEO_PLATEAU_ANCHORS : MIN_PLATEAU_ANCHORS;

            if (anchors.Count < minAnchors)
            {
                return;
            }

            DeepAnalysisTimelinePlateauDiagnostic plateau = new DeepAnalysisTimelinePlateauDiagnostic();
            double halfStepSec = string.Equals(diagnostic.AnchorMode, "video", StringComparison.Ordinal) ? VIDEO_ANCHOR_STEP_SEC / 2.0 : ANCHOR_STEP_SEC / 2.0;
            // I bordi plateau sono stimati a meta' passo attorno al primo/ultimo anchor
            plateau.Index = diagnostic.Plateaus.Count;
            plateau.StartSrcSec = anchors[0].SourceCenterSec - halfStepSec;
            plateau.EndSrcSec = anchors[anchors.Count - 1].SourceCenterSec + halfStepSec;
            if (plateau.StartSrcSec < 0.0) { plateau.StartSrcSec = 0.0; }
            plateau.OffsetMs = this.MedianOffset(anchors);
            plateau.AnchorCount = anchors.Count;
            plateau.AverageScore = this.ComputeAverageScore(anchors);
            diagnostic.Plateaus.Add(plateau);
        }

        /// <summary>
        /// Fonde plateau adiacenti con offset ancora equivalente
        /// </summary>
        /// <param name="diagnostic">Diagnostica contenente i plateau da fondere</param>
        private void MergeAdjacentPlateaus(DeepAnalysisTimelineMapDiagnostic diagnostic)
        {
            List<DeepAnalysisTimelinePlateauDiagnostic> merged = new List<DeepAnalysisTimelinePlateauDiagnostic>();

            if (diagnostic.Plateaus == null || diagnostic.Plateaus.Count == 0)
            {
                return;
            }

            for (int i = 0; i < diagnostic.Plateaus.Count; i++)
            {
                DeepAnalysisTimelinePlateauDiagnostic current = diagnostic.Plateaus[i];
                if (merged.Count > 0 && Math.Abs(merged[merged.Count - 1].OffsetMs - current.OffsetMs) < MIN_TIMELINE_TRANSITION_MS)
                {
                    // Media pesata per numero anchor: mantiene piu' influenza ai plateau piu' supportati
                    DeepAnalysisTimelinePlateauDiagnostic previous = merged[merged.Count - 1];
                    int totalAnchors = previous.AnchorCount + current.AnchorCount;
                    previous.EndSrcSec = current.EndSrcSec;
                    previous.OffsetMs = (int)Math.Round(((previous.OffsetMs * previous.AnchorCount) + (current.OffsetMs * current.AnchorCount)) / (double)totalAnchors);
                    previous.AverageScore = ((previous.AverageScore * previous.AnchorCount) + (current.AverageScore * current.AnchorCount)) / totalAnchors;
                    previous.AnchorCount = totalAnchors;
                }
                else
                {
                    current.Index = merged.Count;
                    merged.Add(current);
                }
            }

            diagnostic.Plateaus.Clear();
            diagnostic.Plateaus.AddRange(merged);
        }

        /// <summary>
        /// Calcola la mediana offset degli anchor
        /// </summary>
        /// <param name="anchors">Anchor da valutare</param>
        /// <returns>Offset mediano in millisecondi</returns>
        private int MedianOffset(List<DeepAnalysisTimelineAnchorDiagnostic> anchors)
        {
            List<int> values = new List<int>();

            for (int i = 0; i < anchors.Count; i++)
            {
                values.Add(anchors[i].OffsetMs);
            }

            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>
        /// Converte plateau timeline in regioni source continue
        /// </summary>
        /// <param name="plateaus">Plateau validi</param>
        /// <param name="sourceDurationSec">Durata source</param>
        /// <returns>Regioni offset continue</returns>
        private List<OffsetRegion> BuildRegionsFromPlateaus(List<DeepAnalysisTimelinePlateauDiagnostic> plateaus, double sourceDurationSec)
        {
            List<OffsetRegion> result = new List<OffsetRegion>();

            for (int i = 0; i < plateaus.Count; i++)
            {
                // Il confine tra due plateau e' il punto medio tra fine plateau precedente e inizio successivo
                OffsetRegion region = new OffsetRegion();
                region.StartSrcSec = i == 0 ? 0.0 : (plateaus[i - 1].EndSrcSec + plateaus[i].StartSrcSec) / 2.0;
                region.EndSrcSec = i == plateaus.Count - 1 ? sourceDurationSec : (plateaus[i].EndSrcSec + plateaus[i + 1].StartSrcSec) / 2.0;
                region.SupportStartSrcSec = plateaus[i].StartSrcSec;
                region.SupportEndSrcSec = plateaus[i].EndSrcSec;
                region.OffsetMs = plateaus[i].OffsetMs;
                region.MatchCount = plateaus[i].AnchorCount;
                if (region.EndSrcSec > region.StartSrcSec)
                {
                    result.Add(region);
                }
            }

            return result;
        }

        #endregion

        #region Classi annidate

        /// <summary>
        /// Riferimento interno a una traccia audio utilizzabile per anchor timeline
        /// </summary>
        private class AudioTrackRef
        {
            /// <summary>
            /// Indice stream audio per ffmpeg
            /// </summary>
            public int AudioStreamIndex { get; set; }

            /// <summary>
            /// Lingua normalizzata
            /// </summary>
            public string Language { get; set; }

            /// <summary>
            /// Nome traccia
            /// </summary>
            public string Name { get; set; }
        }

        #endregion
    }
}
