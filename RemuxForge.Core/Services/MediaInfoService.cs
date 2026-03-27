using System;
using System.IO;

namespace RemuxForge.Core
{
    /// <summary>
    /// Servizio per esecuzione mediainfo CLI e generazione report
    /// </summary>
    public class MediaInfoService
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso eseguibile mediainfo
        /// </summary>
        private string _mediaInfoPath;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="mediaInfoPath">Percorso eseguibile mediainfo</param>
        public MediaInfoService(string mediaInfoPath)
        {
            this._mediaInfoPath = mediaInfoPath;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Verifica che mediainfo sia accessibile e funzionante
        /// </summary>
        /// <returns>True se mediainfo e' funzionante</returns>
        public bool Verify()
        {
            bool result = false;

            try
            {
                // Esegue mediainfo --Version per confermare esistenza
                string output = this.RunProcess("--Version");
                result = (output.Length > 0);
            }
            catch
            {
                // mediainfo non trovato o non eseguibile
                ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "mediainfo non accessibile");
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Genera il report standard di mediainfo per un file
        /// </summary>
        /// <param name="filePath">Percorso del file da analizzare</param>
        /// <returns>Report testuale o stringa vuota in caso di errore</returns>
        public string GetReport(string filePath)
        {
            string result = "";

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                result = this.RunProcess(filePath);
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "Errore esecuzione mediainfo: " + ex.Message);
                result = "Errore esecuzione mediainfo: " + ex.Message;
            }

            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Esegue mediainfo con gli argomenti dati e restituisce stdout
        /// </summary>
        /// <param name="arguments">Argomenti da passare a mediainfo</param>
        /// <returns>Output stdout del processo</returns>
        private string RunProcess(params string[] arguments)
        {
            ProcessResult result = ProcessRunner.Run(this._mediaInfoPath, arguments);

            return result.Stdout;
        }

        #endregion
    }
}
