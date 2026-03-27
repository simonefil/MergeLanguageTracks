using System;
using System.IO;
using System.Text;

namespace RemuxForge.Core
{
    /// <summary>
    /// Logging centralizzato con supporto multi-sink (callback UI, file, console fallback)
    /// </summary>
    public static class ConsoleHelper
    {
        #region Variabili statiche

        /// <summary>
        /// Callback per redirect output verso UI (CLI, TUI, WebUI)
        /// </summary>
        private static Action<LogSection, LogLevel, string> s_logCallback;

        /// <summary>
        /// Percorso file di log su disco (vuoto = disabilitato)
        /// </summary>
        private static string s_logFilePath = "";

        /// <summary>
        /// Lock per accesso thread-safe al file di log
        /// </summary>
        private static object s_logFileLock = new object();

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Scrive un messaggio di log con sezione e livello specificati
        /// </summary>
        /// <param name="section">Sezione operativa del messaggio</param>
        /// <param name="level">Livello di severita' del messaggio</param>
        /// <param name="text">Testo del messaggio</param>
        public static void Write(LogSection section, LogLevel level, string text)
        {
            // Sink 1: callback UI (se registrato)
            if (s_logCallback != null)
            {
                s_logCallback(section, level, text);
            }
            else
            {
                // Fallback console diretta (nessun callback attivo)
                WriteToConsole(level, text);
            }

            // Sink 2: file log (se abilitato)
            if (s_logFilePath.Length > 0)
            {
                WriteToFile(section, level, text);
            }
        }

        /// <summary>
        /// Imposta il callback per il redirect dell'output verso UI
        /// </summary>
        /// <param name="callback">Callback che riceve sezione, livello e testo</param>
        public static void SetLogCallback(Action<LogSection, LogLevel, string> callback)
        {
            s_logCallback = callback;
        }

        /// <summary>
        /// Rimuove il callback di redirect e ripristina l'output su console
        /// </summary>
        public static void ClearLogCallback()
        {
            s_logCallback = null;
        }

        /// <summary>
        /// Abilita il log su file
        /// </summary>
        /// <param name="filePath">Percorso del file di log</param>
        public static void EnableFileLog(string filePath)
        {
            s_logFilePath = filePath;
        }

        /// <summary>
        /// Ricrea il file di log (reset per nuovo scan)
        /// </summary>
        public static void ResetFileLog()
        {
            if (s_logFilePath.Length == 0)
            {
                return;
            }

            lock (s_logFileLock)
            {
                try
                {
                    File.WriteAllText(s_logFilePath, "");
                }
                catch
                {
                    // Impossibile resettare il file di log, continua senza
                }
            }
        }

        /// <summary>
        /// Restituisce il prefisso testo per una sezione
        /// </summary>
        /// <param name="section">Sezione operativa</param>
        /// <returns>Prefisso formattato o stringa vuota per General</returns>
        public static string FormatSectionPrefix(LogSection section)
        {
            string result = "";

            if (section == LogSection.General) { result = ""; }
            else if (section == LogSection.Config) { result = "[CONFIG] "; }
            else if (section == LogSection.Speed) { result = "[SPEED] "; }
            else if (section == LogSection.Deep) { result = "[DEEP] "; }
            else if (section == LogSection.FrameSync) { result = "[FRAME-SYNC] "; }
            else if (section == LogSection.Conv) { result = "[CONV] "; }
            else if (section == LogSection.Encode) { result = "[ENC] "; }
            else if (section == LogSection.Merge) { result = "[MERGE] "; }
            else if (section == LogSection.Ffmpeg) { result = "[FFMPEG] "; }
            else if (section == LogSection.Report) { result = "[REPORT] "; }

            return result;
        }

        /// <summary>
        /// Mappa un livello di log al colore console corrispondente
        /// </summary>
        /// <param name="level">Livello di log</param>
        /// <returns>Colore console</returns>
        public static ConsoleColor MapLevelToColor(LogLevel level)
        {
            ConsoleColor color = ConsoleColor.Gray;

            if (level == LogLevel.Error) { color = ConsoleColor.Red; }
            else if (level == LogLevel.Warning) { color = ConsoleColor.Yellow; }
            else if (level == LogLevel.Notice) { color = ConsoleColor.DarkYellow; }
            else if (level == LogLevel.Success) { color = ConsoleColor.Green; }
            else if (level == LogLevel.Phase) { color = ConsoleColor.Cyan; }
            else if (level == LogLevel.Header) { color = ConsoleColor.White; }
            else if (level == LogLevel.Info) { color = ConsoleColor.DarkCyan; }
            else if (level == LogLevel.Text) { color = ConsoleColor.Gray; }
            else if (level == LogLevel.Debug) { color = ConsoleColor.DarkGray; }

            return color;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Scrive un messaggio sulla console con il colore appropriato al livello
        /// </summary>
        /// <param name="level">Livello di log</param>
        /// <param name="text">Testo del messaggio</param>
        private static void WriteToConsole(LogLevel level, string text)
        {
            ConsoleColor color = MapLevelToColor(level);
            ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = original;
        }

        /// <summary>
        /// Appende un messaggio formattato al file di log
        /// </summary>
        /// <param name="section">Sezione operativa</param>
        /// <param name="level">Livello di log</param>
        /// <param name="text">Testo del messaggio</param>
        private static void WriteToFile(LogSection section, LogLevel level, string text)
        {
            lock (s_logFileLock)
            {
                try
                {
                    // Formato: yyyy-MM-dd HH:mm:ss [SECTION] [LEVEL] testo
                    StringBuilder sb = new StringBuilder(256);
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.Append(" [");
                    sb.Append(FormatSectionTag(section));
                    sb.Append("] [");
                    sb.Append(level.ToString().ToUpper());
                    sb.Append("] ");
                    sb.Append(text);
                    sb.Append(Environment.NewLine);

                    File.AppendAllText(s_logFilePath, sb.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // Errore scrittura file log, continua senza
                }
            }
        }

        /// <summary>
        /// Restituisce il tag sezione per il file di log
        /// </summary>
        /// <param name="section">Sezione operativa</param>
        /// <returns>Tag sezione senza parentesi quadre</returns>
        private static string FormatSectionTag(LogSection section)
        {
            string result = "GENERAL";

            if (section == LogSection.Config) { result = "CONFIG"; }
            else if (section == LogSection.Speed) { result = "SPEED"; }
            else if (section == LogSection.Deep) { result = "DEEP"; }
            else if (section == LogSection.FrameSync) { result = "FRAME-SYNC"; }
            else if (section == LogSection.Conv) { result = "CONV"; }
            else if (section == LogSection.Encode) { result = "ENC"; }
            else if (section == LogSection.Merge) { result = "MERGE"; }
            else if (section == LogSection.Ffmpeg) { result = "FFMPEG"; }
            else if (section == LogSection.Report) { result = "REPORT"; }

            return result;
        }

        #endregion
    }
}
