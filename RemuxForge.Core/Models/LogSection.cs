namespace RemuxForge.Core
{
    /// <summary>
    /// Sezione operativa di un messaggio di log
    /// </summary>
    public enum LogSection
    {
        /// <summary>
        /// Flusso principale, messaggi generici
        /// </summary>
        General,

        /// <summary>
        /// Configurazione e validazione parametri
        /// </summary>
        Config,

        /// <summary>
        /// Correzione velocita' (SpeedCorrectionService)
        /// </summary>
        Speed,

        /// <summary>
        /// Deep analysis (DeepAnalysisService, TrackSplitService)
        /// </summary>
        Deep,

        /// <summary>
        /// Sincronizzazione frame (FrameSyncService)
        /// </summary>
        FrameSync,

        /// <summary>
        /// Conversione audio (AudioConversionService)
        /// </summary>
        Conv,

        /// <summary>
        /// Encoding video (VideoEncodingService)
        /// </summary>
        Encode,

        /// <summary>
        /// Merge tracce (MkvToolsService)
        /// </summary>
        Merge,

        /// <summary>
        /// Setup e download ffmpeg (FfmpegProvider)
        /// </summary>
        Ffmpeg,

        /// <summary>
        /// Report finale
        /// </summary>
        Report
    }
}
