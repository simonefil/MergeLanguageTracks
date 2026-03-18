using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace MergeLanguageTracks.Core
{
    /// <summary>
    /// Servizio per la conversione di tracce audio lossless tramite ffmpeg
    /// </summary>
    public class AudioConversionService
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso eseguibile ffmpeg
        /// </summary>
        private string _ffmpegPath;

        /// <summary>
        /// Cartella per file temporanei convertiti
        /// </summary>
        private string _tempFolder;

        /// <summary>
        /// Formato target di conversione (flac o opus)
        /// </summary>
        private string _format;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso eseguibile ffmpeg</param>
        /// <param name="tempFolder">Cartella per file temporanei</param>
        /// <param name="format">Formato target: "flac" o "opus"</param>
        public AudioConversionService(string ffmpegPath, string tempFolder, string format)
        {
            this._ffmpegPath = ffmpegPath;
            this._tempFolder = tempFolder;
            this._format = format;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Converte una singola traccia audio da un file MKV
        /// </summary>
        /// <param name="inputFile">Percorso file MKV sorgente</param>
        /// <param name="trackId">ID della traccia audio da estrarre e convertire</param>
        /// <param name="channels">Numero di canali della traccia</param>
        /// <param name="label">Etichetta per il nome file temporaneo</param>
        /// <returns>Percorso del file convertito, stringa vuota se errore</returns>
        public string ConvertTrack(string inputFile, int trackId, int channels, string label)
        {
            string result = "";
            string extension = "";
            string outputFile = "";
            string codecArgs = "";
            int bitrate = 0;
            int exitCode = -1;
            string processOutput = "";

            // Determina estensione e argomenti codec in base al formato
            if (string.Equals(this._format, "flac", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".flac";
                codecArgs = "-c:a flac -compression_level " + AppSettings.FlacCompressionLevel.ToString();
            }
            else if (string.Equals(this._format, "opus", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".ogg";
                bitrate = AppSettings.GetOpusBitrateForChannels(channels);
                codecArgs = "-c:a libopus -b:a " + bitrate.ToString() + "k";
            }

            // Genera nome file temporaneo univoco
            outputFile = Path.Combine(this._tempFolder, label + "_t" + trackId.ToString() + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);

            ConsoleHelper.WriteDarkYellow("  [CONV] Conversione traccia " + trackId + " (" + channels + "ch) -> " + this._format.ToUpper());
            if (string.Equals(this._format, "opus", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteDarkGray("  [CONV] Bitrate: " + bitrate + "kbps");
            }

            // Esegui ffmpeg
            exitCode = this.RunFfmpeg(inputFile, trackId, codecArgs, outputFile, out processOutput);

            if (exitCode == 0 && File.Exists(outputFile))
            {
                // Conversione riuscita
                FileInfo fi = new FileInfo(outputFile);
                ConsoleHelper.WriteGreen("  [CONV] Traccia " + trackId + " convertita (" + Utils.FormatSize(fi.Length) + ")");
                result = outputFile;
            }
            else
            {
                // Conversione fallita
                ConsoleHelper.WriteRed("  [CONV] Errore conversione traccia " + trackId + " (exit code: " + exitCode + ")");
                if (processOutput.Length > 0)
                {
                    // Mostra ultime righe di errore
                    string[] lines = processOutput.Split('\n');
                    int startLine = (lines.Length > 3) ? lines.Length - 3 : 0;
                    for (int i = startLine; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].Trim();
                        if (trimmed.Length > 0)
                        {
                            ConsoleHelper.WriteDarkGray("  [CONV] " + trimmed);
                        }
                    }
                }

                // Pulizia file parziale
                if (File.Exists(outputFile))
                {
                    try { File.Delete(outputFile); } catch { }
                }
            }

            return result;
        }

        /// <summary>
        /// Elimina un file convertito temporaneo
        /// </summary>
        /// <param name="filePath">Percorso del file da eliminare</param>
        public static void DeleteTempFile(string filePath)
        {
            if (filePath.Length > 0 && File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Esegue ffmpeg per la conversione di una traccia
        /// </summary>
        /// <param name="inputFile">File di input</param>
        /// <param name="trackId">ID traccia da estrarre</param>
        /// <param name="codecArgs">Argomenti codec (es. "-c:a flac -compression_level 12")</param>
        /// <param name="outputFile">File di output</param>
        /// <param name="processOutput">Output combinato stdout+stderr</param>
        /// <returns>Exit code del processo</returns>
        private int RunFfmpeg(string inputFile, int trackId, string codecArgs, string outputFile, out string processOutput)
        {
            int exitCode = -1;
            Process proc = null;
            StringBuilder sb = new StringBuilder();
            string stdout = "";
            string stderr = "";

            // Costruisci argomenti: -i input -map 0:trackId codecArgs -y output
            string[] codecParts = codecArgs.Split(' ');

            try
            {
                proc = new Process();
                proc.StartInfo.FileName = this._ffmpegPath;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = Encoding.UTF8;

                // Argomenti via ArgumentList per encoding corretto
                proc.StartInfo.ArgumentList.Add("-i");
                proc.StartInfo.ArgumentList.Add(inputFile);
                proc.StartInfo.ArgumentList.Add("-map");
                proc.StartInfo.ArgumentList.Add("0:" + trackId.ToString());

                // Aggiungi argomenti codec
                for (int i = 0; i < codecParts.Length; i++)
                {
                    proc.StartInfo.ArgumentList.Add(codecParts[i]);
                }

                // Sovrascrivi senza conferma
                proc.StartInfo.ArgumentList.Add("-y");
                proc.StartInfo.ArgumentList.Add(outputFile);

                proc.Start();

                // Legge stdout e stderr in parallelo per prevenire deadlock
                Thread convergence = new Thread(() => { stdout = proc.StandardOutput.ReadToEnd(); });
                convergence.Start();
                stderr = proc.StandardError.ReadToEnd();
                convergence.Join();

                proc.WaitForExit();
                exitCode = proc.ExitCode;

                sb.Append(stdout);
                if (stderr.Length > 0)
                {
                    sb.Append(stderr);
                }
            }
            catch (Exception ex)
            {
                sb.Append("Eccezione durante l'esecuzione di ffmpeg: " + ex.Message);
            }
            finally
            {
                if (proc != null) { proc.Dispose(); proc = null; }
            }

            processOutput = sb.ToString();

            return exitCode;
        }

        #endregion
    }
}
