using RemuxForge.Core.Models;
using System.Collections.Generic;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Mapping diagnostico delle regioni DeepAnalysis
    /// </summary>
    public class DeepAnalysisRegionMapper
    {
        #region Metodi pubblici

        /// <summary>
        /// Converte regioni interne in DTO diagnostici pubblici
        /// </summary>
        public List<DeepAnalysisRegionDiagnostic> BuildDiagnostics(List<OffsetRegion> regions)
        {
            List<DeepAnalysisRegionDiagnostic> result = new List<DeepAnalysisRegionDiagnostic>();
            DeepAnalysisRegionDiagnostic item;
            if (regions == null)
            {
                return result;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                item = new DeepAnalysisRegionDiagnostic();
                item.Index = i + 1;
                item.StartSrcSec = regions[i].StartSrcSec;
                item.EndSrcSec = regions[i].EndSrcSec;
                item.OffsetMs = regions[i].OffsetMs;
                item.MatchCount = regions[i].MatchCount;
                result.Add(item);
            }

            return result;
        }

        #endregion
    }
}
