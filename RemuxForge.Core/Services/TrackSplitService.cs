using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RemuxForge.Core
{
    /// <summary>
    /// Servizio che applica una EditMap alle tracce audio e sottotitoli del lang,
    /// eseguendo taglia-cuci tramite ffmpeg stream copy e concat
    /// </summary>
    public class TrackSplitService
    {
        #region Costanti

        /// <summary>
        /// Codec audio per cui ffmpeg ha encoder e puo' generare silenzi
        /// </summary>
        private static readonly HashSet<string> ENCODABLE_CODECS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AC-3", "E-AC-3", "AAC", "FLAC", "DTS", "MP3", "Opus", "Vorbis", "PCM",
            "A_AC3", "A_EAC3", "A_AAC", "A_FLAC", "A_DTS", "A_MP3", "A_OPUS", "A_VORBIS"
        };

        /// <summary>
        /// Codec audio senza encoder ffmpeg
        /// </summary>
        private static readonly HashSet<string> NON_ENCODABLE_CODECS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TrueHD", "DTS-HD Master Audio", "DTS-HD High Resolution", "DTS:X", "MLP",
            "A_TRUEHD", "A_DTS/LOSSLESS", "A_MLP"
        };

        #endregion

        #region Variabili di classe

        /// <summary>
        /// Timeout singolo comando ffmpeg in millisecondi
        /// </summary>
        private int _ffmpegTimeoutMs;

        /// <summary>
        /// Percorso eseguibile ffmpeg
        /// </summary>
        private string _ffmpegPath;

        /// <summary>
        /// Cartella per file temporanei
        /// </summary>
        private string _tempFolder;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso eseguibile ffmpeg</param>
        /// <param name="tempFolder">Cartella per file temporanei</param>
        public TrackSplitService(string ffmpegPath, string tempFolder)
        {
            this._ffmpegPath = ffmpegPath;
            this._tempFolder = tempFolder;

            TrackSplitConfig cfg = AppSettingsService.Instance.Settings.Advanced.TrackSplit;
            this._ffmpegTimeoutMs = cfg.FfmpegTimeoutMs;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Applica la EditMap a una singola traccia, producendo un file temporaneo con taglia-cuci
        /// </summary>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="trackId">ID traccia MKV</param>
        /// <param name="trackType">Tipo traccia: "audio" o "subtitles"</param>
        /// <param name="trackCodec">Codec della traccia</param>
        /// <param name="channels">Numero canali (solo audio)</param>
        /// <param name="sampleRate">Sample rate (solo audio)</param>
        /// <param name="editMap">EditMap con operazioni</param>
        /// <param name="label">Etichetta per naming file temporanei</param>
        /// <returns>Percorso file risultante, stringa vuota se errore o codec non supportato</returns>
        public string ApplyEditMap(string langFile, int trackId, string trackType, string trackCodec, int channels, int sampleRate, EditMap editMap, string label)
        {
            string result = "";
            bool isAudio = string.Equals(trackType, "audio", StringComparison.OrdinalIgnoreCase);
            bool isSub = string.Equals(trackType, "subtitles", StringComparison.OrdinalIgnoreCase);
            string inputFile = langFile;
            string stretchedFile = "";
            List<string> segmentFiles = new List<string>();
            string concatFile = "";

            // Verifica codec per audio
            if (isAudio && this.IsNonEncodableCodec(trackCodec))
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Codec " + trackCodec + " non supportato per deep analysis, traccia " + trackId + " saltata");
                return result;
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Notice, "  Traccia " + (isAudio ? "audio" : "sub") + " " + trackId + " (" + trackCodec + "): " + editMap.Operations.Count + " operazioni da applicare");

            // Se c'e' stretch e la traccia e' audio, applica stretch prima del taglia-cuci
            if (isAudio && editMap.StretchFactor.Length > 0)
            {
                stretchedFile = this.ApplyStretch(langFile, trackId, trackCodec, channels, sampleRate, editMap.StretchFactor, label);
                if (stretchedFile.Length == 0)
                {
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Errore stretch traccia " + trackId);
                    return result;
                }
                // Per il taglia-cuci successivo usiamo il file stretchato (track 0)
                inputFile = stretchedFile;
                trackId = 0;
            }

            // Calcola segmenti e silenzi
            segmentFiles = this.BuildSegments(inputFile, trackId, trackType, trackCodec, channels, sampleRate, editMap, label);

            if (segmentFiles.Count == 0)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Nessun segmento prodotto per traccia " + trackId);
                // Cleanup stretch temporaneo
                if (stretchedFile.Length > 0) { FileHelper.DeleteTempFile(stretchedFile); }
                return result;
            }

            // Concatena i segmenti
            concatFile = this.ConcatSegments(segmentFiles, isAudio, label, trackId);

            if (concatFile.Length > 0)
            {
                result = concatFile;
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Success, "  Concatenazione " + segmentFiles.Count + " parti -> output OK");
            }
            else
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Errore concatenazione traccia " + trackId);
            }

            // Cleanup segmenti temporanei (non il concat finale)
            for (int i = 0; i < segmentFiles.Count; i++)
            {
                FileHelper.DeleteTempFile(segmentFiles[i]);
            }

            // Cleanup stretch temporaneo
            if (stretchedFile.Length > 0) { FileHelper.DeleteTempFile(stretchedFile); }

            return result;
        }


        #endregion

        #region Metodi privati

        /// <summary>
        /// Verifica se il codec non e' encodabile da ffmpeg
        /// </summary>
        /// <param name="codec">Nome codec</param>
        /// <returns>True se il codec non e' supportato per encoding</returns>
        private bool IsNonEncodableCodec(string codec)
        {
            bool result = false;

            // Controlla match diretto
            if (NON_ENCODABLE_CODECS.Contains(codec))
            {
                result = true;
            }
            else
            {
                // Controlla match parziale per varianti
                string codecLower = codec.ToLowerInvariant();
                if (codecLower.Contains("truehd") || codecLower.Contains("dts-hd") || codecLower.Contains("dts:x") || codecLower.Contains("mlp") || codecLower.Contains("lossless"))
                {
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Applica stretch all'intera traccia audio tramite ffmpeg atempo
        /// </summary>
        /// <param name="langFile">Percorso file lang</param>
        /// <param name="trackId">ID traccia audio</param>
        /// <param name="trackCodec">Codec della traccia</param>
        /// <param name="channels">Numero canali</param>
        /// <param name="sampleRate">Sample rate</param>
        /// <param name="stretchFactor">Fattore stretch come stringa "num/den"</param>
        /// <param name="label">Etichetta per il file</param>
        /// <returns>Percorso file stretchato, stringa vuota se errore</returns>
        private string ApplyStretch(string langFile, int trackId, string trackCodec, int channels, int sampleRate, string stretchFactor, string label)
        {
            string outputFile = "";
            string extension = ".mka";
            double stretchRatio = 0.0;
            string codecArgs = "";
            int exitCode = -1;

            // Calcola rapporto stretch da stringa "num/den"
            string[] parts = stretchFactor.Split('/');
            if (parts.Length == 2)
            {
                double num = 0.0;
                double den = 0.0;
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out num) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out den) && den > 0.0)
                {
                    stretchRatio = num / den;
                }
            }

            if (stretchRatio <= 0.0)
            {
                return "";
            }

            // Determina codec di output per re-encode
            codecArgs = this.GetCodecArgs(trackCodec, channels, sampleRate);
            if (codecArgs.Length == 0)
            {
                return "";
            }

            outputFile = Path.Combine(this._tempFolder, label + "_stretch_t" + trackId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Stretch audio: atempo=" + stretchRatio.ToString("F6", CultureInfo.InvariantCulture));

            exitCode = this.RunFfmpegCommand(new string[]
            {
                "-nostdin", "-hide_banner", "-y",
                "-i", langFile,
                "-map", "0:" + trackId,
                "-af", "atempo=" + stretchRatio.ToString("F6", CultureInfo.InvariantCulture),
                codecArgs,
                outputFile
            });

            if (exitCode != 0 || !File.Exists(outputFile))
            {
                FileHelper.DeleteTempFile(outputFile);
                return "";
            }

            return outputFile;
        }

        /// <summary>
        /// Costruisce i segmenti (estratti + silenzi) dalle operazioni della EditMap
        /// </summary>
        /// <param name="inputFile">File di input (originale o stretchato)</param>
        /// <param name="trackId">ID traccia nel file di input</param>
        /// <param name="trackType">Tipo traccia</param>
        /// <param name="trackCodec">Codec traccia</param>
        /// <param name="channels">Numero canali</param>
        /// <param name="sampleRate">Sample rate</param>
        /// <param name="editMap">EditMap con operazioni</param>
        /// <param name="label">Etichetta per naming</param>
        /// <returns>Lista percorsi file segmento ordinati</returns>
        private List<string> BuildSegments(string inputFile, int trackId, string trackType, string trackCodec, int channels, int sampleRate, EditMap editMap, string label)
        {
            List<string> files = new List<string>();
            bool isAudio = string.Equals(trackType, "audio", StringComparison.OrdinalIgnoreCase);
            double currentStartMs = 0.0;
            string segFile = "";
            string silFile = "";
            int segIdx = 0;

            for (int i = 0; i < editMap.Operations.Count; i++)
            {
                EditOperation op = editMap.Operations[i];

                if (string.Equals(op.Type, EditOperation.INSERT_SILENCE, StringComparison.Ordinal))
                {
                    // Estrai segmento prima del punto di inserimento
                    if (op.LangTimestampMs > currentStartMs)
                    {
                        segFile = this.ExtractTrackSegment(inputFile, trackId, trackType, currentStartMs / 1000.0, op.LangTimestampMs / 1000.0, label, segIdx);
                        if (segFile.Length > 0)
                        {
                            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Segmento " + (segIdx + 1) + ": " + (currentStartMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "-" + (op.LangTimestampMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s (stream copy)");
                            files.Add(segFile);
                            segIdx++;
                        }
                    }

                    // Genera silenzio (solo audio)
                    if (isAudio)
                    {
                        silFile = this.GenerateSilence(trackCodec, channels, sampleRate, op.DurationMs, label, segIdx);
                        if (silFile.Length > 0)
                        {
                            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Silenzio " + segIdx + ": " + (op.DurationMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s");
                            files.Add(silFile);
                            segIdx++;
                        }
                    }
                    else
                    {
                        // Per sottotitoli il gap e' implicito
                        ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    [gap " + (op.DurationMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s]");
                    }

                    currentStartMs = op.LangTimestampMs;
                }
                else if (string.Equals(op.Type, EditOperation.CUT_SEGMENT, StringComparison.Ordinal))
                {
                    // Estrai segmento prima del taglio
                    if (op.LangTimestampMs > currentStartMs)
                    {
                        segFile = this.ExtractTrackSegment(inputFile, trackId, trackType, currentStartMs / 1000.0, op.LangTimestampMs / 1000.0, label, segIdx);
                        if (segFile.Length > 0)
                        {
                            ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Segmento " + (segIdx + 1) + ": " + (currentStartMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "-" + (op.LangTimestampMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s (stream copy)");
                            files.Add(segFile);
                            segIdx++;
                        }
                    }

                    // Salta il segmento tagliato
                    currentStartMs = op.LangTimestampMs + op.DurationMs;
                    ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    [cut " + (op.DurationMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s]");
                }
            }

            // Segmento finale: dal punto corrente alla fine del file
            segFile = this.ExtractTrackSegment(inputFile, trackId, trackType, currentStartMs / 1000.0, -1.0, label, segIdx);
            if (segFile.Length > 0)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Debug, "    Segmento " + (segIdx + 1) + ": " + (currentStartMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + "s-fine (stream copy)");
                files.Add(segFile);
            }

            return files;
        }

        /// <summary>
        /// Estrae un segmento di una traccia tramite ffmpeg stream copy
        /// </summary>
        /// <param name="inputFile">File di input</param>
        /// <param name="trackId">ID traccia</param>
        /// <param name="trackType">Tipo traccia</param>
        /// <param name="startSec">Inizio in secondi</param>
        /// <param name="endSec">Fine in secondi, -1 per fino alla fine</param>
        /// <param name="label">Etichetta</param>
        /// <param name="segIndex">Indice segmento</param>
        /// <returns>Percorso file segmento, stringa vuota se errore</returns>
        private string ExtractTrackSegment(string inputFile, int trackId, string trackType, double startSec, double endSec, string label, int segIndex)
        {
            string result = "";
            bool isAudio = string.Equals(trackType, "audio", StringComparison.OrdinalIgnoreCase);
            string extension = isAudio ? ".mka" : ".mks";
            // Usa indice globale ffmpeg (0:N) perche' trackId e' l'ID mkvmerge globale
            string mapArg = "0:" + trackId;
            string copyArg = isAudio ? "-c:a" : "-c:s";
            string outputFile = Path.Combine(this._tempFolder, label + "_seg" + segIndex + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
            List<string> args = new List<string>();
            int exitCode = -1;

            args.Add("-nostdin");
            args.Add("-hide_banner");
            args.Add("-y");
            args.Add("-i");
            args.Add(inputFile);
            args.Add("-ss");
            args.Add(startSec.ToString("F3", CultureInfo.InvariantCulture));
            if (endSec > 0.0)
            {
                args.Add("-to");
                args.Add(endSec.ToString("F3", CultureInfo.InvariantCulture));
            }
            args.Add("-map");
            args.Add(mapArg);
            args.Add(copyArg);
            args.Add("copy");
            args.Add(outputFile);

            exitCode = this.RunFfmpegCommand(args.ToArray());

            if (exitCode == 0 && File.Exists(outputFile))
            {
                result = outputFile;
            }
            else
            {
                FileHelper.DeleteTempFile(outputFile);
            }

            return result;
        }

        /// <summary>
        /// Genera un file di silenzio nel codec specificato
        /// </summary>
        /// <param name="trackCodec">Codec originale</param>
        /// <param name="channels">Numero canali</param>
        /// <param name="sampleRate">Sample rate</param>
        /// <param name="durationMs">Durata in millisecondi</param>
        /// <param name="label">Etichetta</param>
        /// <param name="segIndex">Indice segmento</param>
        /// <returns>Percorso file silenzio, stringa vuota se errore</returns>
        private string GenerateSilence(string trackCodec, int channels, int sampleRate, int durationMs, string label, int segIndex)
        {
            string result = "";
            string outputFile = Path.Combine(this._tempFolder, label + "_sil" + segIndex + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mka");
            string channelLayout = AudioChannelHelper.GetChannelLayout(channels);
            string codecArgs = this.GetCodecArgs(trackCodec, channels, sampleRate);
            double durationSec = durationMs / 1000.0;
            int exitCode = -1;

            if (codecArgs.Length == 0)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "  Codec non supportato per generazione silenzio: " + trackCodec);
                return result;
            }

            string anullsrcArg = "anullsrc=channel_layout=" + channelLayout + ":sample_rate=" + sampleRate;

            exitCode = this.RunFfmpegCommand(new string[]
            {
                "-nostdin", "-hide_banner", "-y",
                "-f", "lavfi",
                "-i", anullsrcArg,
                "-t", durationSec.ToString("F3", CultureInfo.InvariantCulture),
                codecArgs,
                outputFile
            });

            if (exitCode == 0 && File.Exists(outputFile))
            {
                result = outputFile;
            }
            else
            {
                FileHelper.DeleteTempFile(outputFile);
            }

            return result;
        }

        /// <summary>
        /// Concatena i segmenti tramite ffmpeg concat demuxer
        /// </summary>
        /// <param name="segmentFiles">Lista file segmento</param>
        /// <param name="isAudio">True se traccia audio</param>
        /// <param name="label">Etichetta</param>
        /// <param name="trackId">ID traccia originale</param>
        /// <returns>Percorso file concatenato, stringa vuota se errore</returns>
        private string ConcatSegments(List<string> segmentFiles, bool isAudio, string label, int trackId)
        {
            string result = "";
            string extension = isAudio ? ".mka" : ".mks";
            string copyArg = isAudio ? "-c:a" : "-c:s";
            string concatListFile = Path.Combine(this._tempFolder, label + "_concat_t" + trackId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
            string outputFile = Path.Combine(this._tempFolder, label + "_deep_t" + trackId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
            int exitCode = -1;
            StringBuilder sb = new StringBuilder();

            // Scrivi file lista per concat demuxer
            for (int i = 0; i < segmentFiles.Count; i++)
            {
                // Il concat demuxer richiede path con forward slash e single quotes
                string escapedPath = segmentFiles[i].Replace("\\", "/").Replace("'", "'\\''");
                sb.Append("file '").Append(escapedPath).Append("'\n");
            }

            try
            {
                File.WriteAllText(concatListFile, sb.ToString());
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  Errore scrittura concat list: " + ex.Message);
                return result;
            }

            exitCode = this.RunFfmpegCommand(new string[]
            {
                "-nostdin", "-hide_banner", "-y",
                "-f", "concat",
                "-safe", "0",
                "-i", concatListFile,
                copyArg, "copy",
                outputFile
            });

            // Cleanup lista
            FileHelper.DeleteTempFile(concatListFile);

            if (exitCode == 0 && File.Exists(outputFile))
            {
                result = outputFile;
            }
            else
            {
                FileHelper.DeleteTempFile(outputFile);
            }

            return result;
        }

        /// <summary>
        /// Determina gli argomenti codec ffmpeg per l'encoding
        /// </summary>
        /// <param name="trackCodec">Codec originale</param>
        /// <param name="channels">Numero canali</param>
        /// <param name="sampleRate">Sample rate</param>
        /// <returns>Stringa argomenti codec, vuota se non supportato</returns>
        private string GetCodecArgs(string trackCodec, int channels, int sampleRate)
        {
            string result = "";
            string codecLower = trackCodec.ToLowerInvariant();
            int bitrate = 0;

            if (codecLower.Contains("ac-3") || codecLower.Contains("ac3") || codecLower == "a_ac3")
            {
                bitrate = this.GetBitrateForChannels(channels, "ac3");
                result = "-c:a ac3 -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("e-ac-3") || codecLower.Contains("eac3") || codecLower == "a_eac3")
            {
                bitrate = this.GetBitrateForChannels(channels, "eac3");
                result = "-c:a eac3 -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("aac") || codecLower == "a_aac")
            {
                bitrate = this.GetBitrateForChannels(channels, "aac");
                result = "-c:a aac -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("flac") || codecLower == "a_flac")
            {
                result = "-c:a flac";
            }
            else if (codecLower.Contains("dts") || codecLower == "a_dts")
            {
                // DTS core (non DTS-HD che e' filtrato prima)
                bitrate = this.GetBitrateForChannels(channels, "dts");
                result = "-c:a dca -b:a " + bitrate + "k -strict -2";
            }
            else if (codecLower.Contains("mp3") || codecLower == "a_mp3" || codecLower == "a_mpeg/l3")
            {
                bitrate = this.GetBitrateForChannels(channels, "mp3");
                result = "-c:a libmp3lame -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("opus") || codecLower == "a_opus")
            {
                bitrate = this.GetBitrateForChannels(channels, "opus");
                result = "-c:a libopus -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("vorbis") || codecLower == "a_vorbis")
            {
                bitrate = this.GetBitrateForChannels(channels, "vorbis");
                result = "-c:a libvorbis -b:a " + bitrate + "k";
            }
            else if (codecLower.Contains("pcm") || codecLower.StartsWith("a_pcm"))
            {
                result = "-c:a pcm_s16le";
            }

            return result;
        }

        /// <summary>
        /// Determina il bitrate appropriato per codec e numero di canali
        /// </summary>
        /// <param name="channels">Numero canali</param>
        /// <param name="codec">Nome codec</param>
        /// <returns>Bitrate in kbps</returns>
        private int GetBitrateForChannels(int channels, string codec)
        {
            int bitrate = 128;

            if (codec == "ac3" || codec == "eac3")
            {
                if (channels <= 2) { bitrate = 192; }
                else if (channels <= 6) { bitrate = 640; }
                else { bitrate = 768; }
            }
            else if (codec == "aac")
            {
                if (channels <= 2) { bitrate = 128; }
                else if (channels <= 6) { bitrate = 384; }
                else { bitrate = 512; }
            }
            else if (codec == "dts")
            {
                if (channels <= 2) { bitrate = 256; }
                else if (channels <= 6) { bitrate = 768; }
                else { bitrate = 1024; }
            }
            else if (codec == "mp3")
            {
                if (channels <= 2) { bitrate = 192; }
                else { bitrate = 320; }
            }
            else if (codec == "opus")
            {
                if (channels <= 1) { bitrate = 128; }
                else if (channels <= 2) { bitrate = 256; }
                else if (channels <= 6) { bitrate = 510; }
                else { bitrate = 768; }
            }
            else if (codec == "vorbis")
            {
                if (channels <= 2) { bitrate = 192; }
                else if (channels <= 6) { bitrate = 448; }
                else { bitrate = 640; }
            }

            return bitrate;
        }

        /// <summary>
        /// Esegue un comando ffmpeg e attende il completamento
        /// </summary>
        /// <param name="args">Argomenti come array di stringhe (supporta argomenti composti con spazi)</param>
        /// <returns>Exit code del processo</returns>
        private int RunFfmpegCommand(string[] args)
        {
            // Splitta argomenti composti (es. "-c:a ac3 -b:a 640k") preservando path con spazi
            string[] splitArgs = ProcessRunner.SplitCompoundArgs(args);

            return ProcessRunner.RunDiscardOutput(this._ffmpegPath, splitArgs, this._ffmpegTimeoutMs);
        }

        #endregion
    }
}
