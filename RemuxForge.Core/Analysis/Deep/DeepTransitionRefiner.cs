using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Raffina le transizioni tra regioni DeepAnalysis e produce operazioni EditMap
    /// </summary>
    public class DeepTransitionRefiner
    {
        #region Delegati

        /// <summary>
        /// Calcola il raggio di ricerca locale tra due regioni offset
        /// </summary>
        /// <param name="current">Regione corrente</param>
        /// <param name="next">Regione successiva</param>
        /// <returns>Raggio ricerca in secondi</returns>
        public delegate double TransitionRadiusResolver(OffsetRegion current, OffsetRegion next);

        /// <summary>
        /// Cerca un crossover confrontando due offset candidati con metrica differenziale
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="searchStartSrc">Inizio ricerca source</param>
        /// <param name="searchEndSrc">Fine ricerca source</param>
        /// <param name="oldOffsetSec">Offset precedente</param>
        /// <param name="newOffsetSec">Offset successivo</param>
        /// <param name="inverseRatio">Rapporto inverso speed correction</param>
        /// <returns>Crossover source in secondi, oppure -1</returns>
        public delegate double DifferentialCrossoverScanner(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio);

        /// <summary>
        /// Cerca un crossover con scansione densa sul vecchio offset
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="searchStartSrc">Inizio ricerca source</param>
        /// <param name="searchEndSrc">Fine ricerca source</param>
        /// <param name="oldOffsetSec">Offset precedente</param>
        /// <param name="inverseRatio">Rapporto inverso speed correction</param>
        /// <returns>Crossover source in secondi</returns>
        public delegate double DenseCrossoverScanner(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double inverseRatio);

        /// <summary>
        /// Cerca un crossover usando run di frame ripetuti
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="searchStartSrc">Inizio ricerca source</param>
        /// <param name="searchEndSrc">Fine ricerca source</param>
        /// <param name="oldOffsetSec">Offset precedente</param>
        /// <param name="newOffsetSec">Offset successivo</param>
        /// <param name="inverseRatio">Rapporto inverso speed correction</param>
        /// <returns>Crossover source in secondi, oppure -1</returns>
        public delegate double RepeatedFrameCrossoverScanner(string sourceFile, string langFile, double searchStartSrc, double searchEndSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio);

        /// <summary>
        /// Conferma linearmente un crossover approssimativo
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="approximateSrc">Crossover approssimativo source</param>
        /// <param name="oldOffsetSec">Offset precedente</param>
        /// <param name="newOffsetSec">Offset successivo</param>
        /// <param name="inverseRatio">Rapporto inverso speed correction</param>
        /// <returns>Crossover confermato source</returns>
        public delegate double LinearCrossoverConfirmer(string sourceFile, string langFile, double approximateSrc, double oldOffsetSec, double newOffsetSec, double inverseRatio);

        /// <summary>
        /// Verifica localmente una transizione candidata
        /// </summary>
        /// <param name="sourceFile">File sorgente</param>
        /// <param name="langFile">File lingua</param>
        /// <param name="crossoverSrcSec">Crossover source</param>
        /// <param name="oldOffsetSec">Offset precedente</param>
        /// <param name="newOffsetSec">Offset successivo</param>
        /// <param name="inverseRatio">Rapporto inverso speed correction</param>
        /// <returns>Diagnostica verifica locale</returns>
        public delegate DeepAnalysisLocalVerificationDiagnostic LocalTransitionVerifier(string sourceFile, string langFile, double crossoverSrcSec, double oldOffsetSec, double newOffsetSec, double inverseRatio);

        #endregion

        #region Variabili di classe

        private readonly TransitionRadiusResolver _radiusResolver;

        private readonly DifferentialCrossoverScanner _audioCrossoverScanner;

        private readonly DifferentialCrossoverScanner _visualCrossoverScanner;

        private readonly DenseCrossoverScanner _denseCrossoverScanner;

        private readonly RepeatedFrameCrossoverScanner _repeatedFrameCrossoverScanner;

        private readonly LinearCrossoverConfirmer _linearCrossoverConfirmer;

        private readonly LocalTransitionVerifier _localTransitionVerifier;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="radiusResolver">Risolutore raggio transizione</param>
        /// <param name="audioCrossoverScanner">Scanner crossover audio</param>
        /// <param name="visualCrossoverScanner">Scanner crossover visuale</param>
        /// <param name="denseCrossoverScanner">Scanner denso visuale</param>
        /// <param name="repeatedFrameCrossoverScanner">Scanner frame ripetuti</param>
        /// <param name="linearCrossoverConfirmer">Confermatore lineare</param>
        /// <param name="localTransitionVerifier">Verificatore locale</param>
        public DeepTransitionRefiner(TransitionRadiusResolver radiusResolver, DifferentialCrossoverScanner audioCrossoverScanner, DifferentialCrossoverScanner visualCrossoverScanner, DenseCrossoverScanner denseCrossoverScanner, RepeatedFrameCrossoverScanner repeatedFrameCrossoverScanner, LinearCrossoverConfirmer linearCrossoverConfirmer, LocalTransitionVerifier localTransitionVerifier)
        {
            this._radiusResolver = radiusResolver;
            this._audioCrossoverScanner = audioCrossoverScanner;
            this._visualCrossoverScanner = visualCrossoverScanner;
            this._denseCrossoverScanner = denseCrossoverScanner;
            this._repeatedFrameCrossoverScanner = repeatedFrameCrossoverScanner;
            this._linearCrossoverConfirmer = linearCrossoverConfirmer;
            this._localTransitionVerifier = localTransitionVerifier;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Raffina i punti di transizione tramite scansione locale audio/video
        /// </summary>
        public List<EditOperation> Refine(string sourceFile, string langFile, List<OffsetRegion> regions, double inverseRatio, DeepAnalysisPerformanceDiagnostic performanceDiagnostics, bool timelineMode, bool allowAudioLocalOverride, out List<DeepAnalysisTransitionDiagnostic> transitions)
        {
            List<EditOperation> operations = new List<EditOperation>();
            transitions = new List<DeepAnalysisTransitionDiagnostic>();
            double oldOffsetSec;
            double newOffsetSec;
            double bestCrossover;
            double breakpointSrc;
            double searchStartSrc;
            double searchEndSrc;
            double searchRadiusSec;
            double validationStartSrc;
            bool audioCrossover;
            int durationMs;
            int minOffsetChangeMs;
            int langTimestampMs;
            int sourceTimestampMs;
            string operationType;
            string refineMethod;
            double boundaryToleranceSec;
            double unsupportedGapStartSrc;
            double unsupportedGapEndSrc;
            DeepAnalysisTransitionDiagnostic transition;
            // Ogni coppia di regioni adiacenti puo' generare un cut o un insert silence
            for (int r = 0; r < regions.Count - 1; r++)
            {
                performanceDiagnostics.TransitionRefineCount++;
                oldOffsetSec = regions[r].OffsetMs / 1000.0;
                newOffsetSec = regions[r + 1].OffsetMs / 1000.0;
                durationMs = (int)Math.Abs(Math.Round((newOffsetSec - oldOffsetSec) * 1000.0));
                transition = new DeepAnalysisTransitionDiagnostic();
                transition.Index = r + 1;
                transition.OldOffsetMs = regions[r].OffsetMs;
                transition.NewOffsetMs = regions[r + 1].OffsetMs;
                transition.DeltaMs = (int)Math.Round(regions[r + 1].OffsetMs - regions[r].OffsetMs);
                transition.DurationMs = durationMs;
                transitions.Add(transition);

                minOffsetChangeMs = 250;

                // Delta molto piccoli sono rumore rispetto alla precisione effettiva della timeline
                if (durationMs < minOffsetChangeMs)
                {
                    transition.Status = "Skipped";
                    transition.RejectReason = "Delta sotto soglia timeline";
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Transizione " + (r + 1) + ": delta offset " + durationMs + "ms sotto soglia=" + minOffsetChangeMs + ", skip");
                    continue;
                }

                breakpointSrc = (regions[r].EndSrcSec + regions[r + 1].StartSrcSec) / 2.0;
                searchRadiusSec = this._radiusResolver(regions[r], regions[r + 1]);
                searchStartSrc = breakpointSrc - searchRadiusSec;
                searchEndSrc = breakpointSrc + searchRadiusSec;
                transition.BreakpointSrcSec = breakpointSrc;
                transition.SearchStartSrcSec = searchStartSrc;
                transition.SearchEndSrcSec = searchEndSrc;

                // La ricerca resta confinata alle due regioni coinvolte
                if (searchStartSrc < regions[r].StartSrcSec) { searchStartSrc = regions[r].StartSrcSec; }
                if (searchEndSrc > regions[r + 1].EndSrcSec) { searchEndSrc = regions[r + 1].EndSrcSec; }
                if (searchStartSrc < 0.0) { searchStartSrc = 0.0; }

                unsupportedGapStartSrc = regions[r].SupportEndSrcSec;
                unsupportedGapEndSrc = regions[r + 1].SupportStartSrcSec;
                if (timelineMode && unsupportedGapStartSrc > 0.0 && unsupportedGapEndSrc > unsupportedGapStartSrc && (unsupportedGapEndSrc - unsupportedGapStartSrc) > (searchEndSrc - searchStartSrc))
                {
                    searchStartSrc = unsupportedGapStartSrc;
                    searchEndSrc = unsupportedGapEndSrc + 90.0;
                    if (searchEndSrc > regions[r + 1].EndSrcSec) { searchEndSrc = regions[r + 1].EndSrcSec; }
                }

                transition.SearchStartSrcSec = searchStartSrc;
                transition.SearchEndSrcSec = searchEndSrc;

                if (searchEndSrc <= searchStartSrc || (searchEndSrc - searchStartSrc) < 1.0)
                {
                    transition.Status = "Rejected";
                    transition.RejectReason = "Finestra refine invalida";
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Transizione " + (r + 1) + ": finestra refine invalida attorno a src " + breakpointSrc.ToString("F1", CultureInfo.InvariantCulture) + "s, skip");
                    continue;
                }

                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Transizione " + (r + 1) + ": scansione densa in src " + searchStartSrc.ToString("F1", CultureInfo.InvariantCulture) + "-" + searchEndSrc.ToString("F1", CultureInfo.InvariantCulture) + "s, breakpoint " + breakpointSrc.ToString("F1", CultureInfo.InvariantCulture) + "s (offset " + ((int)(oldOffsetSec * 1000)) + " -> " + ((int)(newOffsetSec * 1000)) + "ms)");

                audioCrossover = false;
                validationStartSrc = searchStartSrc;
                refineMethod = "";
                bestCrossover = -1.0;

                if (allowAudioLocalOverride)
                {
                    // L'audio locale puo' prevalere solo nei percorsi in cui esiste audio comune affidabile
                    bestCrossover = this._audioCrossoverScanner(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, newOffsetSec, inverseRatio);

                    if (bestCrossover >= 0.0)
                    {
                        audioCrossover = true;
                        refineMethod = "audio";
                        performanceDiagnostics.TransitionAudioRefineCount++;
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Refine audio locale: crossover src " + bestCrossover.ToString("F2", CultureInfo.InvariantCulture) + "s");
                    }
                }

                if (bestCrossover < 0.0)
                {
                    // Primo fallback visuale: confronto differenziale tra vecchio e nuovo offset
                    bestCrossover = this._visualCrossoverScanner(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, newOffsetSec, inverseRatio);
                    if (bestCrossover >= 0.0)
                    {
                        refineMethod = "visual-differential";
                        performanceDiagnostics.TransitionVisualRefineCount++;
                    }
                }

                if (bestCrossover < 0.0 && timelineMode && !allowAudioLocalOverride && this._repeatedFrameCrossoverScanner != null)
                {
                    // In timeline video-only i frame ripetuti aiutano su anime e VFR con pose statiche
                    bestCrossover = this._repeatedFrameCrossoverScanner(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, newOffsetSec, inverseRatio);
                    if (bestCrossover >= 0.0)
                    {
                        refineMethod = "repeated-frame";
                        performanceDiagnostics.TransitionVisualRefineCount++;
                    }
                }

                if (bestCrossover < 0.0)
                {
                    // Ultimo percorso: dip denso e conferma lineare sul tratto locale
                    bestCrossover = this._denseCrossoverScanner(sourceFile, langFile, searchStartSrc, searchEndSrc, oldOffsetSec, inverseRatio);
                    bestCrossover = this._linearCrossoverConfirmer(sourceFile, langFile, bestCrossover, oldOffsetSec, newOffsetSec, inverseRatio);
                    refineMethod = "dense-linear";
                    performanceDiagnostics.TransitionDenseLinearRefineCount++;
                }
                else if (newOffsetSec > oldOffsetSec)
                {
                    // Per insert silence il punto operativo in language precede il crossover source del delta
                    if (!audioCrossover || (Math.Abs(bestCrossover - breakpointSrc) > 2.0 && (newOffsetSec - oldOffsetSec) >= 2.0))
                    {
                        bestCrossover = bestCrossover - (newOffsetSec - oldOffsetSec);
                        validationStartSrc = searchStartSrc - (newOffsetSec - oldOffsetSec) - 1.0;
                    }
                }

                boundaryToleranceSec = timelineMode ? Math.Max(2.0, (durationMs / 1000.0) + 1.5) : 0.0;
                if (bestCrossover < validationStartSrc - boundaryToleranceSec || bestCrossover > searchEndSrc + boundaryToleranceSec)
                {
                    transition.Status = "Rejected";
                    transition.RejectReason = "Crossover fuori finestra";
                    transition.ValidationStartSrcSec = validationStartSrc;
                    transition.CrossoverSrcSec = bestCrossover;
                    transition.AudioCrossover = audioCrossover;
                    transition.RefineMethod = refineMethod;
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Transizione " + (r + 1) + ": crossover fuori finestra (" + bestCrossover.ToString("F2", CultureInfo.InvariantCulture) + "s fuori " + validationStartSrc.ToString("F1", CultureInfo.InvariantCulture) + "-" + searchEndSrc.ToString("F1", CultureInfo.InvariantCulture) + "s), skip");
                    continue;
                }

                sourceTimestampMs = (int)Math.Round(bestCrossover * 1000.0);
                langTimestampMs = (int)Math.Round((bestCrossover - oldOffsetSec) * 1000.0);
                if (Math.Abs(inverseRatio - 1.0) > 0.0001)
                {
                    langTimestampMs = (int)Math.Round(langTimestampMs * inverseRatio);
                }

                if (newOffsetSec > oldOffsetSec)
                {
                    operationType = EditOperation.INSERT_SILENCE;
                }
                else
                {
                    operationType = EditOperation.CUT_SEGMENT;
                }

                // L'operazione viene aggiunta prima della verifica per mantenere diagnostica completa e poi rimossa se non valida
                EditOperation op = new EditOperation();
                op.Type = operationType;
                op.LangTimestampMs = langTimestampMs;
                op.DurationMs = durationMs;
                op.SourceTimestampMs = sourceTimestampMs;
                operations.Add(op);

                transition.Status = "Accepted";
                transition.ValidationStartSrcSec = validationStartSrc;
                transition.AudioCrossover = audioCrossover;
                transition.RefineMethod = refineMethod;
                transition.CrossoverSrcSec = bestCrossover;
                transition.OperationType = operationType;
                transition.LangTimestampMs = langTimestampMs;
                transition.SourceTimestampMs = sourceTimestampMs;
                transition.DurationMs = durationMs;
                transition.LocalVerification = this._localTransitionVerifier(sourceFile, langFile, bestCrossover, oldOffsetSec, newOffsetSec, inverseRatio);

                if (transition.LocalVerification == null || !transition.LocalVerification.Verified)
                {
                    if (timelineMode)
                    {
                        transition.Status = "SkippedUnverified";
                        transition.RejectReason = "Verifica locale timeline-first fallita, operazione scartata";
                        transition.OperationType = "";
                        operations.RemoveAt(operations.Count - 1);
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Transizione " + (r + 1) + ": verifica locale fallita, operazione timeline scartata");
                        continue;
                    }
                    else
                    {
                        transition.Status = "Rejected";
                        transition.RejectReason = "Verifica locale transizione fallita";
                        transition.OperationType = "";
                        operations.RemoveAt(operations.Count - 1);
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Transizione " + (r + 1) + ": verifica locale fallita, operazione scartata");
                        continue;
                    }
                }

                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Transizione " + (r + 1) + ": " + operationType + " @ lang " + (langTimestampMs / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + "s, durata " + durationMs + "ms (crossover src " + bestCrossover.ToString("F2", CultureInfo.InvariantCulture) + "s)");
            }

            return operations;
        }

        #endregion
    }
}
