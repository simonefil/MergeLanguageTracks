namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Risultato della fingerprint audio globale usata come metrica di consenso
    /// </summary>
    public class AudioGlobalFingerprintResult
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public AudioGlobalFingerprintResult()
        {
            this.Success = false;
            this.OffsetMs = int.MinValue;
            this.Score = 0.0;
            this.Margin = 0.0;
            this.Coverage = 0.0;
            this.EnvelopeScore = 0.0;
            this.SilenceScore = 0.0;
            this.OnsetScore = 0.0;
            this.DerivativeScore = 0.0;
            this.SilenceRunScore = 0.0;
            this.ChunkScore = 0.0;
            this.VideoOffsetMs = int.MinValue;
            this.AudioVideoDeltaMs = int.MinValue;
            this.ConfirmedVideoInitial = false;
            this.RejectedVideoInitial = false;
            this.CandidateCount = 0;
            this.WindowMs = 0;
            this.TimingMs = 0;
            this.ExtractionMs = 0;
            this.CorrelationMs = 0;
            this.SourceCacheHit = false;
            this.LanguageCacheHit = false;
            this.FailureReason = "";
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// True se l'audio ha prodotto un candidato abbastanza netto
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Offset interno in millisecondi, langTime - sourceTime
        /// </summary>
        public int OffsetMs { get; set; }

        /// <summary>
        /// Score globale normalizzato
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Margine tra primo e secondo candidato separato
        /// </summary>
        public double Margin { get; set; }

        /// <summary>
        /// Copertura temporale media usata nel confronto
        /// </summary>
        public double Coverage { get; set; }

        /// <summary>
        /// Correlazione envelope RMS/energia
        /// </summary>
        public double EnvelopeScore { get; set; }

        /// <summary>
        /// Concordanza maschera silenzi
        /// </summary>
        public double SilenceScore { get; set; }

        /// <summary>
        /// Correlazione onset/variazioni energia
        /// </summary>
        public double OnsetScore { get; set; }

        /// <summary>
        /// Correlazione derivata signed dell'envelope
        /// </summary>
        public double DerivativeScore { get; set; }

        /// <summary>
        /// Concordanza forma run-length dei silenzi
        /// </summary>
        public double SilenceRunScore { get; set; }

        /// <summary>
        /// Score medio distribuito su chunk temporali
        /// </summary>
        public double ChunkScore { get; set; }

        /// <summary>
        /// Offset video iniziale confrontato con l'audio, se disponibile
        /// </summary>
        public int VideoOffsetMs { get; set; }

        /// <summary>
        /// Delta assoluto tra offset audio e offset video iniziale
        /// </summary>
        public int AudioVideoDeltaMs { get; set; }

        /// <summary>
        /// True se l'audio conferma l'offset video iniziale
        /// </summary>
        public bool ConfirmedVideoInitial { get; set; }

        /// <summary>
        /// True se l'audio boccia un offset video iniziale debole
        /// </summary>
        public bool RejectedVideoInitial { get; set; }

        /// <summary>
        /// Numero offset valutati
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Dimensione finestra fingerprint in millisecondi
        /// </summary>
        public int WindowMs { get; set; }

        /// <summary>
        /// Tempo di calcolo in millisecondi
        /// </summary>
        public long TimingMs { get; set; }

        /// <summary>
        /// Tempo estrazione/costruzione fingerprint in millisecondi
        /// </summary>
        public long ExtractionMs { get; set; }

        /// <summary>
        /// Tempo correlazione e ricerca offset in millisecondi
        /// </summary>
        public long CorrelationMs { get; set; }

        /// <summary>
        /// True se fingerprint sorgente recuperata da cache
        /// </summary>
        public bool SourceCacheHit { get; set; }

        /// <summary>
        /// True se fingerprint lingua recuperata da cache
        /// </summary>
        public bool LanguageCacheHit { get; set; }

        /// <summary>
        /// Motivo fallimento
        /// </summary>
        public string FailureReason { get; set; }

        #endregion
    }
}
