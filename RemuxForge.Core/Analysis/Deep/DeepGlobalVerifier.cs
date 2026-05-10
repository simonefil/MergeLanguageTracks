using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Verifica globale DeepAnalysis su punti distribuiti della timeline source
    /// </summary>
    public class DeepGlobalVerifier
    {
        #region Delegati

        /// <summary>
        /// Calcola l'MSE di un punto globale
        /// </summary>
        public delegate bool PointMseCalculator(string sourceFile, string langFile, List<OffsetRegion> regions, double srcPointMs, double inverseRatio, out double mse);

        #endregion

        #region Variabili di classe

        private readonly DeepAnalysisConfig _deepAnalysisConfig;

        private readonly VideoSyncConfig _videoSyncConfig;

        private readonly PointMseCalculator _pointMseCalculator;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="deepAnalysisConfig">Configurazione DeepAnalysis</param>
        /// <param name="videoSyncConfig">Configurazione metrica video</param>
        /// <param name="pointMseCalculator">Callback calcolo MSE punto</param>
        public DeepGlobalVerifier(DeepAnalysisConfig deepAnalysisConfig, VideoSyncConfig videoSyncConfig, PointMseCalculator pointMseCalculator)
        {
            this._deepAnalysisConfig = deepAnalysisConfig;
            this._videoSyncConfig = videoSyncConfig;
            this._pointMseCalculator = pointMseCalculator;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Verifica che l'EditMap risultante produca match visuali coerenti lungo tutto il file
        /// </summary>
        public bool Verify(string sourceFile, string langFile, List<OffsetRegion> regions, List<EditOperation> operations, double inverseRatio, int sourceDurationMs, out double baselineMse, out DeepAnalysisGlobalVerificationDiagnostic verification)
        {
            bool verified;
            int validPoints = 0;
            double totalMse = 0.0;
            int pointsChecked;
            double stepMs;
            double maxMse = 0.0;
            double dynamicThreshold;
            List<double> allMse = new List<double>();
            double[] pointMse;
            bool[] pointValid;
            ParallelOptions parallelOptions;
            List<OffsetRegion> verificationRegions;
            baselineMse = 0.0;
            verification = new DeepAnalysisGlobalVerificationDiagnostic();
            verificationRegions = this.BuildOperationalRegions(regions, operations, sourceDurationMs);
            stepMs = sourceDurationMs / (double)(this._deepAnalysisConfig.GlobalVerifyPoints + 1);
            pointMse = new double[this._deepAnalysisConfig.GlobalVerifyPoints + 1];
            pointValid = new bool[this._deepAnalysisConfig.GlobalVerifyPoints + 1];
            parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = 4;

            Parallel.For(1, this._deepAnalysisConfig.GlobalVerifyPoints + 1, parallelOptions, p =>
            {
                double mse;
                double srcPointMs = stepMs * p;

                if (this._pointMseCalculator(sourceFile, langFile, verificationRegions, srcPointMs, inverseRatio, out mse))
                {
                    pointMse[p] = mse;
                    pointValid[p] = true;
                }
            });

            for (int i = 1; i < pointValid.Length; i++)
            {
                if (pointValid[i])
                {
                    allMse.Add(pointMse[i]);
                    totalMse += pointMse[i];
                    if (pointMse[i] > maxMse) { maxMse = pointMse[i]; }
                }
            }

            pointsChecked = allMse.Count;
            if (pointsChecked > 0)
            {
                baselineMse = totalMse / pointsChecked;
            }

            dynamicThreshold = baselineMse * this._deepAnalysisConfig.VerifyMseMultiplier;
            if (dynamicThreshold < this._videoSyncConfig.MseThreshold)
            {
                dynamicThreshold = this._videoSyncConfig.MseThreshold;
            }

            for (int i = 0; i < allMse.Count; i++)
            {
                if (allMse[i] < dynamicThreshold)
                {
                    validPoints++;
                }
            }

            double ratio = (pointsChecked > 0) ? (double)validPoints / pointsChecked : 0.0;
            verified = ratio >= this._deepAnalysisConfig.GlobalVerifyMinRatio;
            verification.Verified = verified;
            verification.ValidPoints = validPoints;
            verification.PointsChecked = pointsChecked;
            verification.Ratio = ratio;
            verification.BaselineMse = baselineMse;
            verification.DynamicThreshold = dynamicThreshold;
            verification.MaxMse = maxMse;

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Verifica: " + validPoints + "/" + pointsChecked + " punti OK (MSE baseline=" + baselineMse.ToString("F1", CultureInfo.InvariantCulture) + ", soglia=" + dynamicThreshold.ToString("F1", CultureInfo.InvariantCulture) + ", max=" + maxMse.ToString("F1", CultureInfo.InvariantCulture) + ")");

            return verified;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Costruisce le regioni realmente applicate dall'EditMap finale
        /// </summary>
        private List<OffsetRegion> BuildOperationalRegions(List<OffsetRegion> regions, List<EditOperation> operations, int sourceDurationMs)
        {
            List<OffsetRegion> result = new List<OffsetRegion>();
            double currentOffsetMs = regions != null && regions.Count > 0 ? regions[0].OffsetMs : 0.0;
            double currentStartSec = 0.0;
            double sourceDurationSec = sourceDurationMs / 1000.0;

            if (operations == null || operations.Count == 0)
            {
                OffsetRegion constantRegion = new OffsetRegion();
                constantRegion.StartSrcSec = 0.0;
                constantRegion.EndSrcSec = sourceDurationSec;
                constantRegion.OffsetMs = currentOffsetMs;
                result.Add(constantRegion);
                return result;
            }

            operations.Sort((a, b) => a.SourceTimestampMs.CompareTo(b.SourceTimestampMs));
            for (int i = 0; i < operations.Count; i++)
            {
                double operationSrcSec = operations[i].SourceTimestampMs / 1000.0;
                if (operationSrcSec > currentStartSec)
                {
                    OffsetRegion region = new OffsetRegion();
                    region.StartSrcSec = currentStartSec;
                    region.EndSrcSec = operationSrcSec;
                    region.OffsetMs = currentOffsetMs;
                    result.Add(region);
                }

                if (string.Equals(operations[i].Type, EditOperation.CUT_SEGMENT, System.StringComparison.Ordinal))
                {
                    currentOffsetMs -= operations[i].DurationMs;
                }
                else if (string.Equals(operations[i].Type, EditOperation.INSERT_SILENCE, System.StringComparison.Ordinal))
                {
                    currentOffsetMs += operations[i].DurationMs;
                }

                currentStartSec = operationSrcSec;
            }

            if (sourceDurationSec > currentStartSec)
            {
                OffsetRegion lastRegion = new OffsetRegion();
                lastRegion.StartSrcSec = currentStartSec;
                lastRegion.EndSrcSec = sourceDurationSec;
                lastRegion.OffsetMs = currentOffsetMs;
                result.Add(lastRegion);
            }

            return result.Count > 0 ? result : regions;
        }

        #endregion
    }
}
