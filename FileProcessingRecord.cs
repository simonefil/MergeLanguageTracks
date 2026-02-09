using System;
using System.Collections.Generic;
using System.Text;

namespace MergeLanguageTracks
{
    /// <summary>
    /// Record dei dati di elaborazione per un singolo file.
    /// </summary>
    public class FileProcessingRecord
    {
        #region Proprieta

        /// <summary>
        /// Identificatore episodio estratto dal pattern.
        /// </summary>
        public string EpisodeId { get; set; }

        /// <summary>
        /// Nome file sorgente.
        /// </summary>
        public string SourceFileName { get; set; }

        /// <summary>
        /// Dimensione file sorgente in bytes.
        /// </summary>
        public long SourceSize { get; set; }

        /// <summary>
        /// Lingue tracce audio nel file sorgente.
        /// </summary>
        public List<string> SourceAudioLangs { get; set; }

        /// <summary>
        /// Lingue tracce sottotitoli nel file sorgente.
        /// </summary>
        public List<string> SourceSubLangs { get; set; }

        /// <summary>
        /// Nome file lingua.
        /// </summary>
        public string LangFileName { get; set; }

        /// <summary>
        /// Dimensione file lingua in bytes.
        /// </summary>
        public long LangSize { get; set; }

        /// <summary>
        /// Lingue tracce audio nel file lingua.
        /// </summary>
        public List<string> LangAudioLangs { get; set; }

        /// <summary>
        /// Lingue tracce sottotitoli nel file lingua.
        /// </summary>
        public List<string> LangSubLangs { get; set; }

        /// <summary>
        /// Nome file risultato.
        /// </summary>
        public string ResultFileName { get; set; }

        /// <summary>
        /// Dimensione file risultato in bytes.
        /// </summary>
        public long ResultSize { get; set; }

        /// <summary>
        /// Lingue tracce audio nel file risultato.
        /// </summary>
        public List<string> ResultAudioLangs { get; set; }

        /// <summary>
        /// Lingue tracce sottotitoli nel file risultato.
        /// </summary>
        public List<string> ResultSubLangs { get; set; }

        /// <summary>
        /// Delay audio applicato in millisecondi.
        /// </summary>
        public int AudioDelayApplied { get; set; }

        /// <summary>
        /// Delay sottotitoli applicato in millisecondi.
        /// </summary>
        public int SubDelayApplied { get; set; }

        /// <summary>
        /// Tempo di esecuzione FFmpeg in millisecondi.
        /// </summary>
        public long FfmpegTimeMs { get; set; }

        /// <summary>
        /// Tempo di esecuzione calcolo AutoSync in millisecondi.
        /// </summary>
        public long AutoSyncTimeMs { get; set; }

        /// <summary>
        /// Tempo di esecuzione merge mkvmerge in millisecondi.
        /// </summary>
        public long MergeTimeMs { get; set; }

        /// <summary>
        /// Indica se l'elaborazione e' stata completata con successo.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Motivo dello skip o errore, se applicabile.
        /// </summary>
        public string SkipReason { get; set; }

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FileProcessingRecord()
        {
            this.EpisodeId = "";
            this.SourceFileName = "";
            this.SourceSize = 0;
            this.SourceAudioLangs = new List<string>();
            this.SourceSubLangs = new List<string>();
            this.LangFileName = "";
            this.LangSize = 0;
            this.LangAudioLangs = new List<string>();
            this.LangSubLangs = new List<string>();
            this.ResultFileName = "";
            this.ResultSize = 0;
            this.ResultAudioLangs = new List<string>();
            this.ResultSubLangs = new List<string>();
            this.AudioDelayApplied = 0;
            this.SubDelayApplied = 0;
            this.FfmpegTimeMs = 0;
            this.AutoSyncTimeMs = 0;
            this.MergeTimeMs = 0;
            this.Success = false;
            this.SkipReason = "";
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Formatta la dimensione file in formato leggibile.
        /// </summary>
        /// <param name="bytes">Dimensione in bytes.</param>
        /// <returns>Stringa formattata (es. "1.5 GB").</returns>
        public static string FormatSize(long bytes)
        {
            string result = "";

            if (bytes >= 1073741824)
            {
                result = Math.Round(bytes / 1073741824.0, 2) + " GB";
            }
            else if (bytes >= 1048576)
            {
                result = Math.Round(bytes / 1048576.0, 1) + " MB";
            }
            else if (bytes >= 1024)
            {
                result = Math.Round(bytes / 1024.0, 1) + " KB";
            }
            else
            {
                result = bytes + " B";
            }

            return result;
        }

        /// <summary>
        /// Formatta una lista di lingue come stringa.
        /// </summary>
        /// <param name="langs">Lista di codici lingua.</param>
        /// <returns>Stringa formattata (es. "eng,ita,jpn").</returns>
        public static string FormatLangs(List<string> langs)
        {
            string result = "-";

            if (langs != null && langs.Count > 0)
            {
                result = string.Join(",", langs);
            }

            return result;
        }

        #endregion
    }
}
