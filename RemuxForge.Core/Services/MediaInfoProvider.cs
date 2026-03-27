using System.IO;
using System.Runtime.InteropServices;

namespace RemuxForge.Core
{
    /// <summary>
    /// Individua l'eseguibile mediainfo nelle posizioni note del sistema
    /// </summary>
    public class MediaInfoProvider : ToolProviderBase
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public MediaInfoProvider()
        {
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Individua mediainfo nel sistema
        /// Ordine: AppSettings → posizioni note → PATH
        /// </summary>
        /// <returns>True se mediainfo e' stato trovato</returns>
        public bool Resolve()
        {
            return this.Resolve(true);
        }

        /// <summary>
        /// Individua mediainfo nel sistema
        /// Ordine: AppSettings → posizioni note → PATH
        /// </summary>
        /// <param name="autoSave">Se true, salva il percorso trovato in AppSettings</param>
        /// <returns>True se mediainfo e' stato trovato</returns>
        public bool Resolve(bool autoSave)
        {
            bool resolved = false;
            string miName = "mediainfo" + GetExecutableExtension();
            string found = "";

            // Controlla percorso salvato in AppSettings
            if (AppSettingsService.Instance.Settings.Tools.MediaInfoPath.Length > 0 && File.Exists(AppSettingsService.Instance.Settings.Tools.MediaInfoPath))
            {
                this._resolvedPath = AppSettingsService.Instance.Settings.Tools.MediaInfoPath;
                resolved = true;
            }

            // Controlla posizioni note del sistema (solo Linux/macOS, su Windows la GUI non e' la CLI)
            if (!resolved && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                found = SearchInPaths(miName, this.GetWellKnownPaths());
                if (found.Length > 0)
                {
                    this._resolvedPath = found;
                    resolved = true;
                }
            }

            // Controlla il PATH di sistema
            if (!resolved)
            {
                found = FindInSystemPath(miName);
                if (found.Length > 0)
                {
                    this._resolvedPath = found;
                    resolved = true;
                }
            }

            // Salva percorso trovato in AppSettings per le prossime volte
            if (autoSave && resolved && this._resolvedPath != AppSettingsService.Instance.Settings.Tools.MediaInfoPath)
            {
                AppSettingsService.Instance.Settings.Tools.MediaInfoPath = this._resolvedPath;
                AppSettingsService.Instance.Save();
            }

            return resolved;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Restituisce le posizioni di installazione note per mediainfo per ogni OS
        /// </summary>
        /// <returns>Array di percorsi di ricerca</returns>
        private string[] GetWellKnownPaths()
        {
            string[] paths = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                paths = new string[]
                {
                    @"C:\Program Files\MediaInfo",
                    @"C:\Program Files (x86)\MediaInfo"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                paths = new string[] { "/usr/bin", "/usr/local/bin" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                paths = new string[] { "/usr/local/bin", "/opt/homebrew/bin" };
            }
            else
            {
                paths = new string[0];
            }

            return paths;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Percorso risolto dell'eseguibile mediainfo
        /// </summary>
        public string MediaInfoPath { get { return this._resolvedPath; } }

        #endregion
    }
}
