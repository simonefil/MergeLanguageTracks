using RemuxForge.Core.Configuration;
using System.IO;

namespace RemuxForge.Core.Tools
{
    /// <summary>
    /// Centro unico per la risoluzione dei binari esterni
    /// </summary>
    public class ToolPathResolverService
    {
        #region Variabili di classe

        /// <summary>
        /// Cartella config interna usata per il download di ffmpeg
        /// </summary>
        private string _configFolder;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="configFolder">Cartella config interna dell'applicazione</param>
        public ToolPathResolverService(string configFolder)
        {
            this._configFolder = configFolder;
            if (this._configFolder == null || this._configFolder.Length == 0)
            {
                this._configFolder = AppSettingsService.Instance.ConfigFolder;
            }
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Risolve il path di mkvmerge
        /// </summary>
        /// <param name="autoSave">True per salvare il risultato in AppSettings quando valido</param>
        /// <returns>Path risolto o stringa vuota</returns>
        public string ResolveMkvMergePath(bool autoSave)
        {
            string result = "";
            MkvMergeProvider provider = new MkvMergeProvider();
            if (provider.Resolve(autoSave))
            {
                result = provider.MkvMergePath;
            }

            return result;
        }

        /// <summary>
        /// Risolve il path di ffmpeg
        /// </summary>
        /// <param name="autoSave">True per salvare il risultato in AppSettings quando valido</param>
        /// <param name="allowDownload">True per tentare il download automatico</param>
        /// <returns>Path risolto o stringa vuota</returns>
        public string ResolveFfmpegPath(bool autoSave, bool allowDownload)
        {
            string result = "";
            FfmpegProvider provider = new FfmpegProvider(this._configFolder);
            if (provider.Resolve(autoSave, allowDownload))
            {
                result = provider.FfmpegPath;
            }

            return result;
        }

        /// <summary>
        /// Risolve il path di mediainfo
        /// </summary>
        /// <param name="autoSave">True per salvare il risultato in AppSettings quando valido</param>
        /// <returns>Path risolto o stringa vuota</returns>
        public string ResolveMediaInfoPath(bool autoSave)
        {
            string result = "";
            MediaInfoProvider provider = new MediaInfoProvider();
            if (provider.Resolve(autoSave))
            {
                result = provider.MediaInfoPath;
            }

            return result;
        }

        /// <summary>
        /// Indica se un path mediainfo punta a un eseguibile CLI valido
        /// </summary>
        /// <param name="path">Percorso da verificare</param>
        /// <returns>True se il path e' un eseguibile CLI</returns>
        public bool IsMediaInfoCliPath(string path)
        {
            return MediaInfoProvider.IsCliExecutablePath(path);
        }

        /// <summary>
        /// Risolve mkvextract partendo da mkvmerge
        /// </summary>
        /// <param name="mkvMergePath">Percorso mkvmerge</param>
        /// <param name="autoSave">True per tentare auto-resolve di mkvmerge quando vuoto</param>
        /// <returns>Path mkvextract o stringa vuota</returns>
        public string ResolveMkvExtractPath(string mkvMergePath, bool autoSave)
        {
            string result = "";
            string mkvPath = mkvMergePath;
            if (mkvPath.Length == 0 && autoSave)
            {
                mkvPath = this.ResolveMkvMergePath(autoSave);
            }

            if (mkvPath.Length > 0)
            {
                string folder = Path.GetDirectoryName(mkvPath);
                if (folder.Length > 0)
                {
                    string candidate = Path.Combine(folder, "mkvextract");
                    string windowsCandidate = Path.Combine(folder, "mkvextract.exe");
                    if (File.Exists(candidate))
                    {
                        result = candidate;
                    }
                    else if (File.Exists(windowsCandidate))
                    {
                        result = windowsCandidate;
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
