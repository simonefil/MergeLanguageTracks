using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace MergeLanguageTracks
{
    /// <summary>
    /// Individua o scarica l'eseguibile ffmpeg.
    /// Controlla prima la cartella tools, poi il PATH di sistema, e scarica come ultima risorsa.
    /// </summary>
    public class FfmpegProvider
    {
        #region Variabili di classe

        /// <summary>
        /// Cartella dove sono memorizzati/scaricati i tool.
        /// </summary>
        private string _toolsFolder;

        /// <summary>
        /// Percorso risolto di ffmpeg.
        /// </summary>
        private string _ffmpegPath;

        /// <summary>
        /// URL download Windows x64 per ffmpeg release essentials.
        /// </summary>
        private const string WINDOWS_X64_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        /// <summary>
        /// URL download Linux x64 per ffmpeg build statica.
        /// </summary>
        private const string LINUX_X64_URL = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz";

        /// <summary>
        /// URL download Linux arm64 per ffmpeg build statica.
        /// </summary>
        private const string LINUX_ARM64_URL = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-arm64-static.tar.xz";

        /// <summary>
        /// URL download macOS per ffmpeg (universal binary x64/arm64).
        /// </summary>
        private const string MACOS_FFMPEG_URL = "https://evermeet.cx/ffmpeg/getrelease/ffmpeg/zip";

        #endregion

        #region Proprieta

        /// <summary>
        /// Ottiene il percorso risolto dell'eseguibile ffmpeg.
        /// </summary>
        public string FfmpegPath { get { return this._ffmpegPath; } }

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="toolsFolder">La cartella dove il binario ffmpeg deve essere individuato o scaricato.</param>
        public FfmpegProvider(string toolsFolder)
        {
            this._toolsFolder = toolsFolder;
            this._ffmpegPath = "";
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Individua ffmpeg, scaricandolo se necessario.
        /// </summary>
        /// <returns>True se ffmpeg e' stato trovato o scaricato con successo.</returns>
        public bool Resolve()
        {
            bool resolved = false;
            string exeExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            string ffmpegName = "ffmpeg" + exeExt;

            // Controlla prima la cartella tools
            string toolsFfmpeg = Path.Combine(this._toolsFolder, ffmpegName);

            if (File.Exists(toolsFfmpeg))
            {
                this._ffmpegPath = toolsFfmpeg;
                resolved = true;
            }
            else
            {
                // Controlla il PATH di sistema
                string pathFfmpeg = FindInPath(ffmpegName);

                if (pathFfmpeg.Length > 0)
                {
                    this._ffmpegPath = pathFfmpeg;
                    resolved = true;
                }
                else
                {
                    // Scarica ffmpeg per la piattaforma corrente
                    resolved = this.DownloadForCurrentPlatform(toolsFfmpeg);
                }
            }

            return resolved;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Determina la piattaforma corrente e avvia il download appropriato.
        /// </summary>
        /// <param name="ffmpegDest">Percorso destinazione per ffmpeg.</param>
        /// <returns>True se download ed estrazione hanno avuto successo.</returns>
        private bool DownloadForCurrentPlatform(string ffmpegDest)
        {
            bool success = false;

            // Determina OS e architettura
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isArm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            bool isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;

            // Log piattaforma rilevata
            string archName = isArm64 ? "arm64" : (isX64 ? "x64" : RuntimeInformation.OSArchitecture.ToString());
            string osName = isWindows ? "Windows" : (isLinux ? "Linux" : (isMacOS ? "macOS" : "Unknown"));
            ConsoleHelper.WriteDarkGray("  Piattaforma rilevata: " + osName + " " + archName);

            // Assicura che la cartella tools esista
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
                // macOS usa universal binary, funziona sia su x64 che arm64
                success = this.DownloadMacOS(ffmpegDest);
            }
            else
            {
                ConsoleHelper.WriteRed("  Piattaforma non supportata: " + osName + " " + archName);
                ConsoleHelper.WriteYellow("  Piattaforme supportate: Windows x64, Linux x64, Linux arm64, macOS x64, macOS arm64");
                ConsoleHelper.WriteYellow("  Installa ffmpeg manualmente e assicurati che sia nel PATH.");
            }

            return success;
        }

        /// <summary>
        /// Cerca nel PATH di sistema il nome eseguibile specificato.
        /// </summary>
        /// <param name="executableName">Il nome dell'eseguibile da trovare.</param>
        /// <returns>Il percorso completo se trovato, o stringa vuota.</returns>
        private static string FindInPath(string executableName)
        {
            string result = "";
            string pathEnv = Environment.GetEnvironmentVariable("PATH");

            if (pathEnv == null)
            {
                return result;
            }

            // Dividi PATH per il separatore specifico della piattaforma
            char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            string[] paths = pathEnv.Split(separator);

            for (int i = 0; i < paths.Length; i++)
            {
                string candidate = Path.Combine(paths[i], executableName);
                if (File.Exists(candidate))
                {
                    result = candidate;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Scarica ed estrae ffmpeg su Windows dall'archivio zip gyan.dev.
        /// </summary>
        /// <param name="ffmpegDest">Percorso destinazione per ffmpeg.exe.</param>
        /// <returns>True se completato con successo.</returns>
        private bool DownloadWindows(string ffmpegDest)
        {
            bool success = false;
            string zipPath = Path.Combine(this._toolsFolder, "ffmpeg.zip");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;

            try
            {
                ConsoleHelper.WriteYellow("\n  Download ffmpeg per Windows x64...");
                ConsoleHelper.WriteDarkGray("  URL: " + WINDOWS_X64_URL);

                // Scarica il file zip
                webClient = new WebClient();
                webClient.DownloadFile(WINDOWS_X64_URL, zipPath);

                ConsoleHelper.WriteDarkGray("  Estrazione in corso...");

                // Pulisci directory estrazione precedente se esiste
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                // Estrai lo zip
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Trova ffmpeg.exe nei file estratti
                string foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg.exe");

                if (foundFfmpeg.Length > 0)
                {
                    // Copia nella root della cartella tools
                    File.Copy(foundFfmpeg, ffmpegDest, true);

                    this._ffmpegPath = ffmpegDest;
                    success = true;

                    ConsoleHelper.WriteGreen("  ffmpeg scaricato in: " + this._toolsFolder);
                }
                else
                {
                    ConsoleHelper.WriteRed("  Impossibile trovare ffmpeg.exe nell'archivio");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.WriteYellow("  Scaricalo manualmente da https://www.gyan.dev/ffmpeg/builds/");
            }
            finally
            {
                // Dispose webclient
                if (webClient != null)
                {
                    webClient.Dispose();
                    webClient = null;
                }

                // Pulisci file temporanei
                CleanupTempFiles(zipPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Scarica ed estrae ffmpeg su Linux.
        /// </summary>
        /// <param name="ffmpegDest">Percorso destinazione per ffmpeg.</param>
        /// <param name="downloadUrl">URL di download per l'architettura specifica.</param>
        /// <param name="archName">Nome architettura per logging.</param>
        /// <returns>True se completato con successo.</returns>
        private bool DownloadLinux(string ffmpegDest, string downloadUrl, string archName)
        {
            bool success = false;
            string tarPath = Path.Combine(this._toolsFolder, "ffmpeg.tar.xz");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;

            try
            {
                ConsoleHelper.WriteYellow("\n  Download ffmpeg per Linux " + archName + "...");
                ConsoleHelper.WriteDarkGray("  URL: " + downloadUrl);

                // Scarica il file tar.xz
                webClient = new WebClient();
                webClient.DownloadFile(downloadUrl, tarPath);

                ConsoleHelper.WriteDarkGray("  Estrazione in corso...");

                // Pulisci directory estrazione precedente
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                // Usa tar per estrarre
                int tarExitCode = RunCommand("tar", "xf \"" + tarPath + "\" -C \"" + extractPath + "\"");

                if (tarExitCode != 0)
                {
                    ConsoleHelper.WriteRed("  Errore durante l'estrazione (tar exit code: " + tarExitCode + ")");
                    ConsoleHelper.WriteYellow("  Assicurati che tar e xz-utils siano installati");
                    return false;
                }

                // Trova ffmpeg estratto
                string foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg");

                if (foundFfmpeg.Length > 0)
                {
                    File.Copy(foundFfmpeg, ffmpegDest, true);

                    // Rendi eseguibile
                    RunCommand("chmod", "+x \"" + ffmpegDest + "\"");

                    this._ffmpegPath = ffmpegDest;
                    success = true;

                    ConsoleHelper.WriteGreen("  ffmpeg scaricato in: " + this._toolsFolder);
                }
                else
                {
                    ConsoleHelper.WriteRed("  Impossibile trovare ffmpeg nell'archivio");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.WriteYellow("  Scaricalo manualmente da https://johnvansickle.com/ffmpeg/");
                ConsoleHelper.WriteYellow("  Oppure installa con: sudo apt install ffmpeg");
            }
            finally
            {
                // Dispose webclient
                if (webClient != null)
                {
                    webClient.Dispose();
                    webClient = null;
                }

                // Pulisci file temporanei
                CleanupTempFiles(tarPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Scarica ffmpeg su macOS.
        /// </summary>
        /// <param name="ffmpegDest">Percorso destinazione per ffmpeg.</param>
        /// <returns>True se completato con successo.</returns>
        private bool DownloadMacOS(string ffmpegDest)
        {
            bool success = false;
            string ffmpegZipPath = Path.Combine(this._toolsFolder, "ffmpeg.zip");
            string extractPath = Path.Combine(this._toolsFolder, "ffmpeg_temp");
            WebClient webClient = null;

            try
            {
                ConsoleHelper.WriteYellow("\n  Download ffmpeg per macOS (universal binary)...");

                // Assicura directory temp esista
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                webClient = new WebClient();

                // Scarica ffmpeg
                ConsoleHelper.WriteDarkGray("  Download ffmpeg da: " + MACOS_FFMPEG_URL);
                webClient.DownloadFile(MACOS_FFMPEG_URL, ffmpegZipPath);

                ConsoleHelper.WriteDarkGray("  Estrazione in corso...");

                // Estrai ffmpeg
                ZipFile.ExtractToDirectory(ffmpegZipPath, extractPath);
                string foundFfmpeg = FindFileRecursive(extractPath, "ffmpeg");

                if (foundFfmpeg.Length > 0)
                {
                    File.Copy(foundFfmpeg, ffmpegDest, true);

                    // Rendi eseguibile
                    RunCommand("chmod", "+x \"" + ffmpegDest + "\"");

                    // Rimuovi attributo quarantine (Gatekeeper) se presente
                    RunCommand("xattr", "-d com.apple.quarantine \"" + ffmpegDest + "\"");

                    this._ffmpegPath = ffmpegDest;
                    success = true;

                    ConsoleHelper.WriteGreen("  ffmpeg scaricato in: " + this._toolsFolder);
                }
                else
                {
                    ConsoleHelper.WriteRed("  Impossibile trovare ffmpeg nell'archivio");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Impossibile scaricare ffmpeg: " + ex.Message);
                ConsoleHelper.WriteYellow("  Scaricalo manualmente da https://evermeet.cx/ffmpeg/");
                ConsoleHelper.WriteYellow("  Oppure installa con: brew install ffmpeg");
            }
            finally
            {
                // Dispose webclient
                if (webClient != null)
                {
                    webClient.Dispose();
                    webClient = null;
                }

                // Pulisci file temporanei
                CleanupTempFiles(ffmpegZipPath, extractPath);
            }

            return success;
        }

        /// <summary>
        /// Esegue un comando shell e restituisce l'exit code.
        /// </summary>
        /// <param name="command">Il comando da eseguire.</param>
        /// <param name="arguments">Gli argomenti del comando.</param>
        /// <returns>L'exit code del processo.</returns>
        private static int RunCommand(string command, string arguments)
        {
            int exitCode = -1;
            Process proc = null;

            try
            {
                proc = new Process();
                proc.StartInfo.FileName = command;
                proc.StartInfo.Arguments = arguments;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();
                proc.WaitForExit();
                exitCode = proc.ExitCode;
            }
            catch
            {
                // Ignora errori, ritorna -1
            }
            finally
            {
                if (proc != null)
                {
                    proc.Dispose();
                    proc = null;
                }
            }

            return exitCode;
        }

        /// <summary>
        /// Pulisce file e directory temporanei.
        /// </summary>
        /// <param name="filePath">Percorso file da eliminare (puo' essere null).</param>
        /// <param name="directoryPath">Percorso directory da eliminare (puo' essere null).</param>
        private static void CleanupTempFiles(string filePath, string directoryPath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }
            if (directoryPath != null && Directory.Exists(directoryPath))
            {
                try { Directory.Delete(directoryPath, true); } catch { }
            }
        }

        /// <summary>
        /// Cerca ricorsivamente in un albero di directory un file con il nome dato.
        /// </summary>
        /// <param name="directory">La directory root da cercare.</param>
        /// <param name="fileName">Il nome file da cercare.</param>
        /// <returns>Il percorso completo del file trovato, o stringa vuota.</returns>
        private static string FindFileRecursive(string directory, string fileName)
        {
            string result = "";

            // Controlla file nella directory corrente
            string[] files = Directory.GetFiles(directory);
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
                string[] subdirs = Directory.GetDirectories(directory);
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
    }
}
