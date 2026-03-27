using System.Collections.Generic;

namespace RemuxForge.Core
{
    /// <summary>
    /// Configurazione percorsi tool esterni
    /// </summary>
    public class ToolsConfig
    {
        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public ToolsConfig()
        {
            this.MkvMergePath = "";
            this.FfmpegPath = "";
            this.MediaInfoPath = "";
            this.TempFolder = "";
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Percorso mkvmerge
        /// </summary>
        public string MkvMergePath { get; set; }

        /// <summary>
        /// Percorso ffmpeg
        /// </summary>
        public string FfmpegPath { get; set; }

        /// <summary>
        /// Percorso mediainfo
        /// </summary>
        public string MediaInfoPath { get; set; }

        /// <summary>
        /// Percorso cartella file temporanei
        /// </summary>
        public string TempFolder { get; set; }

        #endregion
    }

    /// <summary>
    /// Configurazione compressione FLAC
    /// </summary>
    public class FlacConfig
    {
        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public FlacConfig()
        {
            this.CompressionLevel = 8;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Livello compressione (0-12)
        /// </summary>
        public int CompressionLevel { get; set; }

        #endregion
    }

    /// <summary>
    /// Configurazione bitrate Opus per canale
    /// </summary>
    public class OpusBitrateConfig
    {
        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public OpusBitrateConfig()
        {
            this.Mono = 128;
            this.Stereo = 256;
            this.Surround51 = 510;
            this.Surround71 = 768;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Bitrate mono in kbps
        /// </summary>
        public int Mono { get; set; }

        /// <summary>
        /// Bitrate stereo in kbps
        /// </summary>
        public int Stereo { get; set; }

        /// <summary>
        /// Bitrate surround 5.1 in kbps
        /// </summary>
        public int Surround51 { get; set; }

        /// <summary>
        /// Bitrate surround 7.1 in kbps
        /// </summary>
        public int Surround71 { get; set; }

        #endregion
    }

    /// <summary>
    /// Configurazione codec Opus
    /// </summary>
    public class OpusConfig
    {
        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public OpusConfig()
        {
            this.Bitrate = new OpusBitrateConfig();
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Bitrate per canale
        /// </summary>
        public OpusBitrateConfig Bitrate { get; set; }

        #endregion
    }

    /// <summary>
    /// Configurazione interfaccia utente
    /// </summary>
    public class UiConfig
    {
        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public UiConfig()
        {
            this.Theme = "nord";
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Tema grafico selezionato (kebab-case)
        /// </summary>
        public string Theme { get; set; }

        #endregion
    }

    /// <summary>
    /// Modello impostazioni applicazione per serializzazione JSON
    /// </summary>
    public class AppSettingsModel
    {
        #region Costanti di validazione

        /// <summary>
        /// Livello compressione FLAC minimo
        /// </summary>
        public const int FLAC_COMPRESSION_MIN = 0;

        /// <summary>
        /// Livello compressione FLAC massimo
        /// </summary>
        public const int FLAC_COMPRESSION_MAX = 12;

        /// <summary>
        /// Bitrate Opus minimo in kbps
        /// </summary>
        public const int OPUS_BITRATE_MIN = 64;

        /// <summary>
        /// Bitrate Opus massimo in kbps
        /// </summary>
        public const int OPUS_BITRATE_MAX = 768;

        /// <summary>
        /// Temi validi
        /// </summary>
        public static readonly string[] VALID_THEMES = { "dark", "nord", "dos-blue", "matrix", "cyberpunk", "solarized-dark", "solarized-light", "cybergum", "everforest" };

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore con valori di default
        /// </summary>
        public AppSettingsModel()
        {
            this.Tools = new ToolsConfig();
            this.Flac = new FlacConfig();
            this.Opus = new OpusConfig();
            this.Ui = new UiConfig();
            this.EncodingProfiles = new List<EncodingProfile>();
            this.Advanced = new AdvancedConfig();
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Configurazione percorsi tool esterni
        /// </summary>
        public ToolsConfig Tools { get; set; }

        /// <summary>
        /// Configurazione compressione FLAC
        /// </summary>
        public FlacConfig Flac { get; set; }

        /// <summary>
        /// Configurazione codec Opus
        /// </summary>
        public OpusConfig Opus { get; set; }

        /// <summary>
        /// Configurazione interfaccia utente
        /// </summary>
        public UiConfig Ui { get; set; }

        /// <summary>
        /// Lista profili di encoding video
        /// </summary>
        public List<EncodingProfile> EncodingProfiles { get; set; }

        /// <summary>
        /// Configurazione avanzata parametri algoritmici
        /// </summary>
        public AdvancedConfig Advanced { get; set; }

        #endregion
    }
}
