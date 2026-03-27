namespace RemuxForge.Core
{
    /// <summary>
    /// Metodi utility per mappatura e formattazione canali audio
    /// </summary>
    public static class AudioChannelHelper
    {
        #region Metodi pubblici

        /// <summary>
        /// Restituisce il nome layout canali standard per libopus in base al numero di canali.
        /// Necessario per normalizzare layout non standard (es. 5.1 side, 7.1 side)
        /// </summary>
        /// <param name="channels">Numero di canali audio</param>
        /// <returns>Nome layout standard o stringa vuota se mono/stereo (non serve remap)</returns>
        public static string GetStandardChannelLayout(int channels)
        {
            string result = "";

            if (channels == 3) { result = "2.1"; }
            else if (channels == 4) { result = "quad"; }
            else if (channels == 6) { result = "5.1"; }
            else if (channels == 8) { result = "7.1"; }

            return result;
        }

        /// <summary>
        /// Determina il channel layout per ffmpeg dal numero di canali.
        /// Usato per generazione silenzio e operazioni che richiedono sempre un layout valido
        /// </summary>
        /// <param name="channels">Numero canali</param>
        /// <returns>Stringa channel layout (sempre un valore valido)</returns>
        public static string GetChannelLayout(int channels)
        {
            string layout = "stereo";

            if (channels <= 1) { layout = "mono"; }
            else if (channels <= 2) { layout = "stereo"; }
            else if (channels <= 6) { layout = "5.1"; }
            else { layout = "7.1"; }

            return layout;
        }

        /// <summary>
        /// Formatta il layout canali in formato numerico per display (1.0, 2.0, 5.1, 7.1)
        /// </summary>
        /// <param name="channels">Numero canali audio</param>
        /// <returns>Stringa layout o vuota se canali non validi</returns>
        public static string FormatChannels(int channels)
        {
            string result = "";

            if (channels == 1) { result = "1.0"; }
            else if (channels == 2) { result = "2.0"; }
            else if (channels == 6) { result = "5.1"; }
            else if (channels == 8) { result = "7.1"; }
            else if (channels > 0) { result = channels + ".0"; }

            return result;
        }

        #endregion
    }
}
