namespace RemuxForge.Core
{
    /// <summary>
    /// Informazioni di una singola traccia (audio, video o sottotitoli) di un container MKV
    /// </summary>
    public class TrackInfo
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public TrackInfo()
        {
            this.Id = 0;
            this.Type = "";
            this.Codec = "";
            this.Language = "";
            this.LanguageIetf = "";
            this.Name = "";
            this.DefaultDurationNs = 0;
            this.Channels = 0;
            this.BitsPerSample = 0;
            this.SamplingFrequency = 0;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Identificatore della traccia all'interno del container MKV.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tipo della traccia: "audio", "video" o "subtitles".
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Codec utilizzato per la traccia, come riportato da mkvmerge.
        /// </summary>
        public string Codec { get; set; }

        /// <summary>
        /// Codice lingua ISO 639-2 della traccia.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Tag lingua IETF/BCP 47 della traccia, se disponibile.
        /// </summary>
        public string LanguageIetf { get; set; }

        /// <summary>
        /// Nome visualizzato della traccia, se impostato.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Durata predefinita in nanosecondi per frame della traccia video
        /// </summary>
        public long DefaultDurationNs { get; set; }

        /// <summary>
        /// Numero di canali audio della traccia
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Bit per campione audio (es. 16, 24, 32)
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        /// Frequenza di campionamento audio in Hz (es. 44100, 48000, 96000)
        /// </summary>
        public int SamplingFrequency { get; set; }

        #endregion
    }
}
