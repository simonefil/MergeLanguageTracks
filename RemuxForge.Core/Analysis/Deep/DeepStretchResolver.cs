using RemuxForge.Core.Analysis.Speed;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Globalization;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Risolve lo stretch globale DeepAnalysis da manuale o default duration
    /// </summary>
    public class DeepStretchResolver
    {
        #region Metodi pubblici

        /// <summary>
        /// Rileva stretch globale dai default_duration delle tracce video
        /// </summary>
        public bool Detect(long sourceDefaultDurationNs, long langDefaultDurationNs, string manualStretchFactor, bool allowAutoStretch, out double stretchRatio, out double inverseRatio, out string stretchFactor)
        {
            bool result = false;
            double sourceFps;
            double langFps;
            double ratioDiff;
            string normalizedManualFactor;
            stretchRatio = 1.0;
            inverseRatio = 1.0;
            stretchFactor = "";

            if (manualStretchFactor != null && manualStretchFactor.Trim().Length > 0)
            {
                if (!SpeedCorrectionService.TryParseStretchFactor(manualStretchFactor, out stretchRatio, out normalizedManualFactor))
                {
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Stretch manuale non valido: " + manualStretchFactor);
                    return false;
                }

                inverseRatio = 1.0 / stretchRatio;
                stretchFactor = normalizedManualFactor;
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Stretch manuale: " + stretchRatio.ToString("F6", CultureInfo.InvariantCulture) + " (" + stretchFactor + ")");
            }
            else if (!allowAutoStretch)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Stretch: disabilitato");
            }
            else if (sourceDefaultDurationNs > 0 && langDefaultDurationNs > 0)
            {
                stretchRatio = (double)sourceDefaultDurationNs / langDefaultDurationNs;
                ratioDiff = Math.Abs(stretchRatio - 1.0);

                if (ratioDiff >= 0.001)
                {
                    inverseRatio = 1.0 / stretchRatio;
                    stretchFactor = sourceDefaultDurationNs + "/" + langDefaultDurationNs;
                    sourceFps = 1000000000.0 / sourceDefaultDurationNs;
                    langFps = 1000000000.0 / langDefaultDurationNs;

                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Stretch: " + stretchRatio.ToString("F6", CultureInfo.InvariantCulture) + " (" + sourceFps.ToString("F3", CultureInfo.InvariantCulture) + "fps -> " + langFps.ToString("F3", CultureInfo.InvariantCulture) + "fps)");
                }
                else
                {
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "  Stretch: nessuno (stesso fps)");
                }
            }

            result = true;
            return result;
        }

        #endregion
    }
}
