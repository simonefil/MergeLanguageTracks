using System;
using System.IO;

namespace RemuxForge.Core
{
    /// <summary>
    /// Metodi utility per operazioni su file
    /// </summary>
    public static class FileHelper
    {
        #region Metodi pubblici

        /// <summary>
        /// Elimina un file temporaneo in modo sicuro, loggando eventuali errori
        /// </summary>
        /// <param name="filePath">Percorso del file da eliminare</param>
        public static void DeleteTempFile(string filePath)
        {
            if (filePath != null && filePath.Length > 0 && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "Eliminazione file temporaneo fallita: " + filePath + " - " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Elimina una directory temporanea in modo sicuro, loggando eventuali errori
        /// </summary>
        /// <param name="directoryPath">Percorso della directory da eliminare</param>
        public static void DeleteTempDirectory(string directoryPath)
        {
            if (directoryPath != null && directoryPath.Length > 0 && Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.Delete(directoryPath, true);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.Write(LogSection.General, LogLevel.Warning, "Eliminazione directory temporanea fallita: " + directoryPath + " - " + ex.Message);
                }
            }
        }

        #endregion
    }
}
