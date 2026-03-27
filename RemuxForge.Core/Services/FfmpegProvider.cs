using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace RemuxForge.Core
{
    /// <summary>
    /// Individua o scarica l'eseguibile ffmpeg
    /// </summary>
    public class FfmpegProvider : ToolProviderBase
    {
        #region Costanti

        /// <summary>
        /// URL download Windows x64
        /// </summary>
        private const string WINDOWS_X64_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        /// <summary>
        /// URL download Linux x64
        /// </summary>
        private const string LINUX_X64_URL = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz";

        /// <summary>
        /// URL download Linux arm64
        /// </summary>
        private const string LINUX_ARM64_URL = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-arm64-static.tar.xz";

        /// <summary>
        /// URL download macOS universal binary
        /// </summary>
        private const string MACOS_FFMPEG_URL = "https://evermeet.cx/ffmpeg/getrelease/ffmpeg/zip";

        #endregion

        #region Variabili di classe

        /// <summary>
        /// Cartella dei tool scaricati
        /// </summary>
        private string _toolsFolder;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="toolsFolder">Cartella di destinazione dei tool</param>
        public FfmpegProvider(string toolsFolder)
        {
            this._toolsFolder = toolsFolder;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Individua ffmpeg, scaricandolo se necessario
        /// Ordine: AppSettings → cartella tools → posizioni note → PATH → download
        /// </summary>
        /// <returns>True se ffmpeg e' stato trovato o scaricato</returns>
        public bool Resolve()
        {
            return this.Resolve(true, true);
        }

        /// <summary>
        /// Individua ffmpeg nel sistema
        /// Ordine: AppSettings → cartella tools → posizioni note → PATH → download (opzionale)
        /// </summary>
        /// <param name="autoSave">Se true, salva il percorso trovato in AppSettings</param>
        /// <param name="allowDownload">Se true, tenta il download se non trovato localmente</param>
        /// <returns>True se ffmpeg e' stato trovato o scaricato</returns>
        public bool Resolve(bool autoSave, bool allowDownload)
        {
            bool resolved = false;
            string ffmpegName = "ffmpeg" + GetExecutableExtension();
            string toolsFfmpeg = Path.Combine(this._toolsFolder, ffmpegName);
            string found = "";

            // Controlla percorso salvato in AppSettings
            if (AppSettingsService.Instance.Settings.Tools.FfmpegPath.Length > 0 && File.Exists(AppSettingsService.Instance.Settings.Tools.FfmpegPath))
            {
                this._resolvedPath = AppSettingsService.Instance.Settings.Tools.FfmpegPath;
                resolved = true;
            }

            // Controlla la cartella tools
            if (!resolved && File.Exists(toolsFfmpeg))
            {
                this._resolvedPath = toolsFfmpeg;
                resolved = true;
            }

            // Controlla posizioni note del sistema
            if (!resolved)
            {
                found = SearchInPaths(ffmpegName, this.GetWellKnownPaths());
                if (found.Length > 0)
                {
                    this._resolvedPath = found;
                    resolved = true;
                }
            }

            // Controlla il PATH di sistema
            if (!resolved)
            {
                found = FindInSystemPath(ffmpegName);
                if (found.Length > 0)
                {
                    this._resolvedPath = found;
                    resolved = true;
                }
            }

            // Scarica per la piattaforma corrente
            if (!resolved && allowDownload)
            {
                resolved = this.DownloadForCurrentPlatform(toolsFfmpeg);
            }

            // Salva percorso trovato in AppSettings per le prossime volte
            if (autoSave && resolved && this._resolvedPath != AppSettingsService.Instance.Settings.Tools.FfmpegPath)
            {
                AppSettingsService.Instance.Settings.Tools.FfmpegPath = this._resolvedPath;
                AppSettingsService.Instance.Save();
            }

            return resolved;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Restituisce le posizioni di installazione note per ffmpeg per ogni OS
        /// </summary>
        /// <returns>Array di percorsi di ricerca</returns>
        private string[] GetWellKnownPaths()
        {
            string[] paths = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                paths = new string[] { "/usr/bin", "/usr/local/bin", "/snap/bin" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                paths = new string[] { "/usr/local/bin", "/opt/homebrew/bin" };
            }
            else
            {
                // Windows: ffmpeg non ha posizione standard, si affida a tools/PATH/download
                paths = new string[0];
            }

            return paths;
        }

        /// <summary>
        /// Determina la piattaforma e avvia il download appropriato
        /// </summary>
        /// <param name="ffmpegDest">Percorso di destinazione dell'eseguibile</param>
        /// <returns>True se il download e' riuscito</returns>
        private bool DownloadForCurrentPlatform(string ffmpegDest)
        {
            bool success = false;
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isArm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            bool isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;
            string archName = isArm64 ? "arm64" : (isX64 ? "x64" : RuntimeInformation.OSArchitecture.ToString());
            string osName = isWindows ? "Windows" : (isLinux ? "Linux" : (isMacOS ? "macOS" : "Unknown"));

            ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  Piattaforma rilevata: " + osName + " " + archName);

            if (!Directory.Exists(this._toolsFolder))
            {
                Directory.CreateDirectory(this._toolsFolder);
            }

            if (isWindows && isX64)
            {
                success = this.DownloadWindows(ffmpegDest);
            }
            else if (isLinux && isX64)
            {
                success = this.DownloadLinux(ffmpegDest, LINUX_X64_URL, "amd64");
            }
            else if (isLinux && isArm64)
            {
                success = this.DownloadLinux(ffmpegDest, LINUX_ARM64_URL, "arm64");
            }
            else if (isMacOS)
            {
                success = this.DownloadMacOS(ffmpegDest);
            }
            else
            {
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Error, "  Piattaforma non supportata: " + osName + " " + archName);
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Piattaforme supportate: Windows x64, Linux x64, Linux arm64, macOS x64, macOS arm64");
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Installa ffmpeg manualmente e assicurati che sia nel PATH.");
            }

            return success;
        }

        /// <summary>
        /// Scarica ed estrae ffmpeg su Windows
        /// </summary>
        /// <param name="ffmpegDest">Percorso di destinazione dell'eseguibile</param>
        /// <returns>True se il download e l'estrazione sono riusciti</returns>
        private bool DownloadWindows(string ffmpegDest)
        {
            bool success = false;
            string zipPath = Path.Combine(this._toolsFolder, "ffmpeg.zip");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;
            string foundFfmpeg = "";

            try
            {
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "\n  Download ffmpeg per Windows x64...");
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  URL: " + WINDOWS_X64_URL);

                webClient = new WebClient();
                webClient.DownloadFile(WINDOWS_X64_URL, zipPath);

                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  Estrazione in corso...");

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg.exe");

                if (foundFfmpeg.Length > 0)
                {
                    File.Copy(foundFfmpeg, ffmpegDest, true);
                    this._resolvedPath = ffmpegDest;
                    success = true;
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Success, "  ffmpeg scaricato in: " + this._toolsFolder);
                }
                else
                {
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Error, "  Impossibile trovare ffmpeg.exe nell'archivio");
                }
            }
            catch (Exception ex)
            {
                // Download o estrazione fallita
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Warning, "Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Scaricalo manualmente da https://www.gyan.dev/ffmpeg/builds/");
            }
            finally
            {
                if (webClient != null) { webClient.Dispose(); webClient = null; }
                CleanupTempFiles(zipPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Scarica ed estrae ffmpeg su Linux
        /// </summary>
        /// <param name="ffmpegDest">Percorso di destinazione dell'eseguibile</param>
        /// <param name="downloadUrl">URL di download dell'archivio</param>
        /// <param name="archName">Nome dell'architettura</param>
        /// <returns>True se il download e l'estrazione sono riusciti</returns>
        private bool DownloadLinux(string ffmpegDest, string downloadUrl, string archName)
        {
            bool success = false;
            string tarPath = Path.Combine(this._toolsFolder, "ffmpeg.tar.xz");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;
            int tarExitCode = 0;
            string foundFfmpeg = "";

            try
            {
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "\n  Download ffmpeg per Linux " + archName + "...");
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  URL: " + downloadUrl);

                webClient = new WebClient();
                webClient.DownloadFile(downloadUrl, tarPath);

                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  Estrazione in corso...");

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                tarExitCode = RunCommand("tar", new string[] { "xf", tarPath, "-C", extractPath });

                if (tarExitCode != 0)
                {
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Error, "  Errore durante l'estrazione (tar exit code: " + tarExitCode + ")");
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Assicurati che tar e xz-utils siano installati");
                }
                else
                {
                    foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg");

                    if (foundFfmpeg.Length > 0)
                    {
                        File.Copy(foundFfmpeg, ffmpegDest, true);
                        RunCommand("chmod", new string[] { "+x", ffmpegDest });
                        this._resolvedPath = ffmpegDest;
                        success = true;
                        ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Success, "  ffmpeg scaricato in: " + this._toolsFolder);
                    }
                    else
                    {
                        ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Error, "  Impossibile trovare ffmpeg nell'archivio");
                    }
                }
            }
            catch (Exception ex)
            {
                // Download o estrazione fallita
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Warning, "Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Scaricalo manualmente da https://johnvansickle.com/ffmpeg/");
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Oppure installa con: sudo apt install ffmpeg");
            }
            finally
            {
                if (webClient != null) { webClient.Dispose(); webClient = null; }
                CleanupTempFiles(tarPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Scarica ffmpeg su macOS
        /// </summary>
        /// <param name="ffmpegDest">Percorso di destinazione dell'eseguibile</param>
        /// <returns>True se il download e l'estrazione sono riusciti</returns>
        private bool DownloadMacOS(string ffmpegDest)
        {
            bool success = false;
            string ffmpegZipPath = Path.Combine(this._toolsFolder, "ffmpeg.zip");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;
            string foundFfmpeg = "";

            try
            {
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "\n  Download ffmpeg per macOS (universal binary)...");

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                webClient = new WebClient();

                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  Download ffmpeg da: " + MACOS_FFMPEG_URL);
                webClient.DownloadFile(MACOS_FFMPEG_URL, ffmpegZipPath);

                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Debug, "  Estrazione in corso...");
                ZipFile.ExtractToDirectory(ffmpegZipPath, extractPath);
                foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg");

                if (foundFfmpeg.Length > 0)
                {
                    File.Copy(foundFfmpeg, ffmpegDest, true);
                    RunCommand("chmod", new string[] { "+x", ffmpegDest });
                    RunCommand("xattr", new string[] { "-d", "com.apple.quarantine", ffmpegDest });
                    this._resolvedPath = ffmpegDest;
                    success = true;
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Success, "  ffmpeg scaricato in: " + this._toolsFolder);
                }
                else
                {
                    ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Error, "  Impossibile trovare ffmpeg nell'archivio");
                }
            }
            catch (Exception ex)
            {
                // Download o estrazione fallita
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Warning, "Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Scaricalo manualmente da https://evermeet.cx/ffmpeg/");
                ConsoleHelper.Write(LogSection.Ffmpeg, LogLevel.Info, "  Oppure installa con: brew install ffmpeg");
            }
            finally
            {
                if (webClient != null) { webClient.Dispose(); webClient = null; }
                CleanupTempFiles(ffmpegZipPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Esegue un comando shell e restituisce l'exit code
        /// </summary>
        /// <param name="command">Comando da eseguire</param>
        /// <param name="args">Argomenti del comando</param>
        /// <returns>Exit code del processo, -1 in caso di errore</returns>
        private static int RunCommand(string command, string[] args)
        {
            ProcessResult result = ProcessRunner.Run(command, args);

            return result.ExitCode;
        }

        /// <summary>
        /// Pulisce file e directory temporanei
        /// </summary>
        /// <param name="filePath">Percorso del file temporaneo da eliminare</param>
        /// <param name="directoryPath">Percorso della directory temporanea da eliminare</param>
        private static void CleanupTempFiles(string filePath, string directoryPath)
        {
            // Errori cleanup ignorati, file temporanei non critici
            FileHelper.DeleteTempFile(filePath);
            FileHelper.DeleteTempDirectory(directoryPath);
        }

        /// <summary>
        /// Cerca ricorsivamente un file per nome in un albero di directory
        /// </summary>
        /// <param name="directory">Directory di partenza della ricerca</param>
        /// <param name="fileName">Nome del file da cercare</param>
        /// <returns>Percorso completo del file, stringa vuota se non trovato</returns>
        private static string FindFileRecursive(string directory, string fileName)
        {
            string result = "";
            string[] files = Directory.GetFiles(directory);
            string[] subdirs = null;

            // Controlla file nella directory corrente
            for (int i = 0; i < files.Length; i++)
            {
                if (Path.GetFileName(files[i]).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    result = files[i];
                    break;
                }
            }

            // Se non trovato, cerca nelle sottodirectory
            if (result.Length == 0)
            {
                subdirs = Directory.GetDirectories(directory);
                for (int i = 0; i < subdirs.Length; i++)
                {
                    result = FindFileRecursive(subdirs[i], fileName);
                    if (result.Length > 0)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Percorso risolto dell'eseguibile ffmpeg
        /// </summary>
        public string FfmpegPath { get { return this._resolvedPath; } }

        #endregion
    }
}
