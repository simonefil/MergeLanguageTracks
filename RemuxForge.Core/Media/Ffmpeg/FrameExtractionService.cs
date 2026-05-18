using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RemuxForge.Core.Media.Ffmpeg
{
    /// <summary>
    /// Estrae frame grayscale e timestamp reali tramite ffmpeg
    /// </summary>
    public class FrameExtractionService
    {
        #region Variabili statiche

        /// <summary>
        /// Regex per parsing pts_time dalle righe showinfo nello stderr ffmpeg
        /// </summary>
        private static readonly Regex s_ptsTimeRegex = new Regex(@"pts_time:(\-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

        #endregion

        #region Variabili di classe

        /// <summary>
        /// Percorso ffmpeg
        /// </summary>
        private string _ffmpegPath;

        /// <summary>
        /// Configurazione VideoSync
        /// </summary>
        private VideoSyncConfig _videoSyncConfig;

        /// <summary>
        /// Configurazione ffmpeg
        /// </summary>
        private FfmpegConfig _ffmpegConfig;

        /// <summary>
        /// Sezione log
        /// </summary>
        private LogSection _logSection;

        /// <summary>
        /// True dopo il primo fallback software per evitare log ripetuti.
        /// </summary>
        private static bool s_reportedHwAccelFallback;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FrameExtractionService(string ffmpegPath, VideoSyncConfig videoSyncConfig, FfmpegConfig ffmpegConfig, LogSection logSection)
        {
            this._ffmpegPath = ffmpegPath;
            this._videoSyncConfig = videoSyncConfig;
            this._ffmpegConfig = ffmpegConfig;
            this._logSection = logSection;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Estrae frame di un segmento video come byte array grayscale
        /// </summary>
        public void ExtractSegment(string filePath, int startMs, double durationSec, double targetFps, bool geometryCropToFourThree, out List<byte[]> frames, out double[] timestampsMs)
        {
            frames = new List<byte[]>();
            timestampsMs = new double[0];
            ProcessBinaryResult processResult;
            double startSec;
            double endSec;
            string startFormatted;
            string endFormatted;
            string resolution;
            string filterChain;
            int frameSize = this._videoSyncConfig.FrameWidth * this._videoSyncConfig.FrameHeight;
            List<byte[]> extractedFrames = frames;
            List<string> args = new List<string>();
            byte[] frameData;
            int totalRead = 0;
            string stderrText;
            MatchCollection ptsMatches;
            List<double> tsList = new List<double>();
            double ptsSec;
            int minCount;
            bool useFpsFilter;
            int maxAttempts;
            int timeoutMs;
            try
            {
                startSec = startMs / 1000.0;
                endSec = startSec + durationSec;
                startFormatted = startSec.ToString("F3", CultureInfo.InvariantCulture);
                endFormatted = endSec.ToString("F3", CultureInfo.InvariantCulture);
                resolution = this._videoSyncConfig.FrameWidth + ":" + this._videoSyncConfig.FrameHeight;
                useFpsFilter = targetFps > 0.0;
                maxAttempts = this._ffmpegConfig.HardwareAcceleration ? 2 : 1;
                timeoutMs = this._ffmpegConfig.FrameExtractionTimeoutMs;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    bool useHardwareAcceleration = this._ffmpegConfig.HardwareAcceleration && attempt == 0;
                    args.Clear();
                    extractedFrames.Clear();
                    tsList.Clear();
                    totalRead = 0;

                    args.Add("-nostdin");
                    args.Add("-hide_banner");
                    if (useHardwareAcceleration)
                    {
                        args.Add("-hwaccel");
                        args.Add("auto");
                    }
                    args.Add("-ss");
                    args.Add(startFormatted);
                    args.Add("-i");
                    args.Add(filePath);
                    args.Add("-copyts");
                    args.Add("-to");
                    args.Add(endFormatted);
                    args.Add("-fps_mode");
                    args.Add(useFpsFilter ? "vfr" : "passthrough");

                    filterChain = this.BuildFilterChain(targetFps, geometryCropToFourThree, useFpsFilter, resolution);
                    args.Add("-vf");
                    args.Add(filterChain);
                    args.Add("-f");
                    args.Add("rawvideo");
                    args.Add("-");

                    frameData = new byte[frameSize];
                    processResult = ProcessRunner.RunBinaryStdout(this._ffmpegPath, args.ToArray(), (buffer, bytesRead) =>
                    {
                        int offset = 0;
                        while (offset < bytesRead)
                        {
                            int copyCount = Math.Min(frameSize - totalRead, bytesRead - offset);
                            Array.Copy(buffer, offset, frameData, totalRead, copyCount);
                            totalRead += copyCount;
                            offset += copyCount;

                            if (totalRead == frameSize)
                            {
                                extractedFrames.Add(frameData);
                                frameData = new byte[frameSize];
                                totalRead = 0;
                            }
                        }
                    }, timeoutMs);

                    stderrText = processResult.Stderr;
                    ptsMatches = s_ptsTimeRegex.Matches(stderrText);
                    for (int i = 0; i < ptsMatches.Count; i++)
                    {
                        if (double.TryParse(ptsMatches[i].Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out ptsSec))
                        {
                            tsList.Add(ptsSec * 1000.0);
                        }
                    }

                    minCount = Math.Min(extractedFrames.Count, tsList.Count);
                    if (minCount < extractedFrames.Count)
                    {
                        extractedFrames.RemoveRange(minCount, extractedFrames.Count - minCount);
                    }
                    if (minCount < tsList.Count)
                    {
                        tsList.RemoveRange(minCount, tsList.Count - minCount);
                    }

                    if (extractedFrames.Count > 0 || !useHardwareAcceleration)
                    {
                        if (extractedFrames.Count == 0 && processResult.ExitCode != 0)
                        {
                            ConsoleHelper.Write(this._logSection, LogLevel.Warning, "  Estrazione frame fallita: " + this.GetLastErrorLine(processResult.Stderr));
                        }

                        break;
                    }

                    if (!s_reportedHwAccelFallback)
                    {
                        ConsoleHelper.Write(this._logSection, LogLevel.Notice, "  HWAccel non utilizzabile per questa estrazione, retry software (" + this.GetLastErrorLine(processResult.Stderr) + ")");
                        s_reportedHwAccelFallback = true;
                    }
                }

                timestampsMs = tsList.ToArray();
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write(this._logSection, LogLevel.Warning, "  Errore ExtractSegment: " + ex.Message);
            }
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Estrae l'ultima riga utile da un messaggio stderr
        /// </summary>
        /// <param name="text">Testo stderr</param>
        /// <returns>Ultima riga utile, o messaggio generico</returns>
        private string GetLastErrorLine(string text)
        {
            string result = "nessun dettaglio";
            string[] lines;

            if (!string.IsNullOrEmpty(text))
            {
                lines = text.Replace("\r", "").Split('\n');
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].Trim().Length > 0)
                    {
                        result = lines[i].Trim();
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Costruisce filter chain ffmpeg per normalizzare frame
        /// </summary>
        private string BuildFilterChain(double targetFps, bool geometryCropToFourThree, bool useFpsFilter, string resolution)
        {
            string filterChain = "";
            if (useFpsFilter)
            {
                filterChain = "fps=fps=" + targetFps.ToString("F6", CultureInfo.InvariantCulture);
            }
            if (geometryCropToFourThree)
            {
                if (filterChain.Length > 0)
                {
                    filterChain = filterChain + ",crop=ih*4/3:ih";
                }
                else
                {
                    filterChain = "crop=ih*4/3:ih";
                }
            }
            if (filterChain.Length > 0)
            {
                filterChain = filterChain + ",scale=w='trunc(iw*sar/2)*2':h=ih:flags=fast_bilinear,setsar=1,scale=" + resolution + ":flags=fast_bilinear,format=gray";
            }
            else
            {
                filterChain = "scale=w='trunc(iw*sar/2)*2':h=ih:flags=fast_bilinear,setsar=1,scale=" + resolution + ":flags=fast_bilinear,format=gray";
            }

            filterChain = filterChain + ",showinfo";
            return filterChain;
        }

        #endregion
    }
}
