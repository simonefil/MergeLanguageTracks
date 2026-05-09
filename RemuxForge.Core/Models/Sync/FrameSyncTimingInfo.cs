namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Timing diagnostici della sincronizzazione frame-sync
    /// </summary>
    public class FrameSyncTimingInfo
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FrameSyncTimingInfo()
        {
            this.TotalMs = 0;
            this.VideoInfoMs = 0;
            this.GeometryMs = 0;
            this.InitialSearchMs = 0;
            this.InitialExtractMs = 0;
            this.InitialSceneCutMs = 0;
            this.InitialVotingMs = 0;
            this.InitialCandidateVerifyMs = 0;
            this.AudioGlobalMs = 0;
            this.CheckpointsMs = 0;
            this.CheckpointsBaseMs = 0;
            this.CheckpointsRetryMs = 0;
            this.VideoExtractCalls = 0;
            this.VideoExtractCacheHits = 0;
            this.VideoExtractCacheMisses = 0;
            this.VideoExtractCachedMs = 0;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Tempo totale frame-sync
        /// </summary>
        public long TotalMs { get; set; }

        /// <summary>
        /// Tempo lettura informazioni video
        /// </summary>
        public long VideoInfoMs { get; set; }

        /// <summary>
        /// Tempo analisi geometria
        /// </summary>
        public long GeometryMs { get; set; }

        /// <summary>
        /// Tempo totale ricerca delay iniziale
        /// </summary>
        public long InitialSearchMs { get; set; }

        /// <summary>
        /// Tempo estrazione frame ricerca iniziale
        /// </summary>
        public long InitialExtractMs { get; set; }

        /// <summary>
        /// Tempo rilevamento scene-cut ricerca iniziale
        /// </summary>
        public long InitialSceneCutMs { get; set; }

        /// <summary>
        /// Tempo voting e clusterizzazione ricerca iniziale
        /// </summary>
        public long InitialVotingMs { get; set; }

        /// <summary>
        /// Tempo verifica candidati iniziali
        /// </summary>
        public long InitialCandidateVerifyMs { get; set; }

        /// <summary>
        /// Tempo fingerprint audio globale
        /// </summary>
        public long AudioGlobalMs { get; set; }

        /// <summary>
        /// Tempo totale checkpoint
        /// </summary>
        public long CheckpointsMs { get; set; }

        /// <summary>
        /// Tempo primo passaggio checkpoint
        /// </summary>
        public long CheckpointsBaseMs { get; set; }

        /// <summary>
        /// Tempo retry checkpoint
        /// </summary>
        public long CheckpointsRetryMs { get; set; }

        /// <summary>
        /// Numero richieste estrazione video tramite cache
        /// </summary>
        public int VideoExtractCalls { get; set; }

        /// <summary>
        /// Numero richieste soddisfatte dalla cache segmenti
        /// </summary>
        public int VideoExtractCacheHits { get; set; }

        /// <summary>
        /// Numero richieste che hanno lanciato ffmpeg
        /// </summary>
        public int VideoExtractCacheMisses { get; set; }

        /// <summary>
        /// Tempo totale nel wrapper cache/estrazione video
        /// </summary>
        public long VideoExtractCachedMs { get; set; }

        #endregion
    }
}
