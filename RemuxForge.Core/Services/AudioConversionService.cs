using System;
using System.IO;

namespace RemuxForge.Core
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
                codecArgs = "-c:a flac -compression_level " + AppSettingsService.Instance.Settings.Flac.CompressionLevel.ToString();
            }
            else if (string.Equals(this._format, "opus", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".ogg";
                bitrate = AppSettingsService.Instance.GetOpusBitrateForChannels(channels);
                // Normalizza layout canali al formato standard per libopus
                string channelLayout = AudioChannelHelper.GetStandardChannelLayout(channels);
                if (channelLayout.Length > 0)
                {
                    codecArgs = "-af aformat=channel_layouts=" + channelLayout + " -c:a libopus -b:a " + bitrate.ToString() + "k -mapping_family 1";
                }
                else
                {
                    codecArgs = "-c:a libopus -b:a " + bitrate.ToString() + "k";
                }
            }

            // Formato non riconosciuto, abort
            if (extension.Length == 0)
            {
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Error, "  Formato non supportato: " + this._format);
                return result;
            }

            // Genera nome file temporaneo univoco
            outputFile = Path.Combine(this._tempFolder, label + "_t" + trackId.ToString() + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);

            ConsoleHelper.Write(LogSection.Conv, LogLevel.Notice, "  Conversione traccia " + trackId + " (" + channels + "ch) -> " + this._format.ToUpper());
            if (string.Equals(this._format, "opus", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Debug, "  Bitrate: " + bitrate + "kbps");
            }

            // Esegui ffmpeg
            exitCode = this.RunFfmpeg(inputFile, trackId, codecArgs, outputFile, out processOutput);

            if (exitCode == 0 && File.Exists(outputFile))
            {
                // Conversione riuscita
                FileInfo fi = new FileInfo(outputFile);
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Success, "  Traccia " + trackId + " convertita (" + Utils.FormatSize(fi.Length) + ")");
                result = outputFile;
            }
            else
            {
                // Conversione fallita
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Error, "  Errore conversione traccia " + trackId + " (exit code: " + exitCode + ")");
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
                            ConsoleHelper.Write(LogSection.Conv, LogLevel.Debug, "  " + trimmed);
                        }
                    }
                }

                // Pulizia file parziale
                FileHelper.DeleteTempFile(outputFile);
            }

            return result;
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
            // Costruisci argomenti: -i input -map 0:trackId codecArgs -y output
            string[] codecParts = codecArgs.Split(' ');
            string[] baseArgs = new string[] { "-i", inputFile, "-map", "0:" + trackId.ToString() };
            string[] tailArgs = new string[] { "-y", outputFile };

            // Combina base + codec + tail
            string[] allArgs = new string[baseArgs.Length + codecParts.Length + tailArgs.Length];
            baseArgs.CopyTo(allArgs, 0);
            codecParts.CopyTo(allArgs, baseArgs.Length);
            tailArgs.CopyTo(allArgs, baseArgs.Length + codecParts.Length);

            ProcessResult result = ProcessRunner.Run(this._ffmpegPath, allArgs);

            // Combina stdout + stderr
            string combined = result.Stdout;
            if (result.Stderr.Length > 0)
            {
                combined = combined + result.Stderr;
            }
            processOutput = combined;

            return result.ExitCode;
        }

        #endregion
    }
}
