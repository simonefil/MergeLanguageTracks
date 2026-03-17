using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MergeLanguageTracks
{
    /// <summary>
    /// Metodi utility statici di formattazione
    /// </summary>
    public static class Utils
    {
        #region Metodi pubblici

        /// <summary>
        /// Formatta una dimensione in byte in stringa leggibile
        /// </summary>
        /// <param name="bytes">Dimensione in bytes</param>
        /// <returns>Stringa formattata</returns>
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
        /// Formatta una lista di codici lingua come stringa separata da virgola
        /// </summary>
        /// <param name="langs">Lista di codici lingua</param>
        /// <returns>Stringa formattata</returns>
        public static string FormatLangs(List<string> langs)
        {
            string result = "-";

            if (langs != null && langs.Count > 0)
            {
                result = string.Join(",", langs);
            }

            return result;
        }

        /// <summary>
        /// Formatta un delay in millisecondi con segno
        /// </summary>
        /// <param name="delayMs">Delay in millisecondi</param>
        /// <returns>Stringa formattata</returns>
        public static string FormatDelay(int delayMs)
        {
            string result = "0ms";

            if (delayMs > 0)
            {
                result = "+" + delayMs + "ms";
            }
            else if (delayMs < 0)
            {
                result = delayMs + "ms";
            }

            return result;
        }

        /// <summary>
        /// Padding a destra con troncamento se il testo supera la larghezza
        /// </summary>
        /// <param name="text">Testo da formattare</param>
        /// <param name="width">Larghezza colonna</param>
        /// <returns>Stringa con padding</returns>
        public static string PadRight(string text, int width)
        {
            string result = "";

            if (text.Length >= width)
            {
                result = text.Substring(0, width - 1) + " ";
            }
            else
            {
                result = text + new string(' ', width - text.Length);
            }

            return result;
        }

        /// <summary>
        /// Restituisce la versione dell'applicazione letta dall'assembly
        /// </summary>
        /// <returns>Stringa versione</returns>
        public static string GetVersion()
        {
            string result = "0.0";
            Assembly asm = Assembly.GetExecutingAssembly();
            AssemblyInformationalVersionAttribute attr = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyInformationalVersionAttribute));

            if (attr != null)
            {
                result = attr.InformationalVersion;
            }

            return result;
        }

        #endregion
    }
}
