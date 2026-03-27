using System.Collections.Generic;

namespace RemuxForge.Core
{
    /// <summary>
    /// Mappa completa delle operazioni di edit prodotta dalla deep analysis.
    /// Descrive come riallineare le tracce lang al source
    /// </summary>
    public class EditMap
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public EditMap()
        {
            this.InitialDelayMs = 0;
            this.StretchFactor = "";
            this.Operations = new List<EditOperation>();
            this.AnalysisTimeMs = 0;
            this.SourceCutsAnalyzed = 0;
            this.LangCutsAnalyzed = 0;
            this.MatchedCuts = 0;
            this.BaselineMse = 0.0;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Delay iniziale in ms (offset del primo segmento)
        /// </summary>
        public int InitialDelayMs { get; set; }

        /// <summary>
        /// Stretch ratio come stringa per mkvmerge, vuoto se nessuno
        /// </summary>
        public string StretchFactor { get; set; }

        /// <summary>
        /// Lista ordinata per timestamp delle operazioni di edit
        /// </summary>
        public List<EditOperation> Operations { get; set; }

        /// <summary>
        /// Tempo di esecuzione analisi in ms
        /// </summary>
        public long AnalysisTimeMs { get; set; }

        /// <summary>
        /// Numero totale di scene cuts analizzate nel source
        /// </summary>
        public int SourceCutsAnalyzed { get; set; }

        /// <summary>
        /// Numero totale di scene cuts analizzate nel lang
        /// </summary>
        public int LangCutsAnalyzed { get; set; }

        /// <summary>
        /// Numero di scene cuts matchate con successo
        /// </summary>
        public int MatchedCuts { get; set; }

        /// <summary>
        /// MSE medio tra frame allineati (baseline qualita' match)
        /// </summary>
        public double BaselineMse { get; set; }

        #endregion
    }
}
