namespace MergeLanguageTracks
{
    public class TrackInfo
    {
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

        #endregion

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
        }

        #endregion
    }
}
