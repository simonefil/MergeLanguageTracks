using RemuxForge.Core.Models;
using System;
using System.IO;

namespace RemuxForge.Core.Infrastructure
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
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
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
            if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
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
