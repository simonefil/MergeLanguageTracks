using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace RemuxForge.Core.Analysis.Deep
{
    /// <summary>
    /// Estrae e confronta envelope audio locali per DeepAnalysis
    /// </summary>
    public class DeepAudioEnvelopeService
    {
        #region Delegati

        /// <summary>
        /// Registra il tempo speso in estrazione envelope audio
        /// </summary>
        /// <param name="elapsedMs">Tempo estrazione in millisecondi</param>
        public delegate void AudioEnvelopeExtractRecorded(long elapsedMs);

        #endregion

        #region Variabili di classe

        private readonly string _ffmpegPath;
        private readonly AudioEnvelopeExtractRecorded _recordExtract;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso ffmpeg</param>
        /// <param name="recordExtract">Callback opzionale di metrica estrazione</param>
        public DeepAudioEnvelopeService(string ffmpegPath, AudioEnvelopeExtractRecorded recordExtract)
        {
            this._ffmpegPath = ffmpegPath;
            this._recordExtract = recordExtract;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Estrae envelope RMS/abs mono a bassa frequenza da una traccia audio specifica
        /// </summary>
        public double[] Extract(string filePath, double startSec, double durationSec, int windowMs, int audioStreamIndex)
        {
            List<double> result = new List<double>();
            ProcessBinaryResult processResult;
            Stopwatch stopwatch = new Stopwatch();
            int sampleRate = 8000;
            int windowSamples = (sampleRate * windowMs) / 1000;
            int usableBytes;
            int sample;
            int samplesInWindow = 0;
            double sumAbs = 0.0;
            List<string> args = new List<string>();

            if (windowSamples < 1)
            {
                windowSamples = 1;
            }

            try
            {
                stopwatch.Start();
                args.Add("-nostdin");
                args.Add("-hide_banner");
                args.Add("-v");
                args.Add("error");
                args.Add("-ss");
                args.Add(startSec.ToString("F3", CultureInfo.InvariantCulture));
                args.Add("-t");
                args.Add(durationSec.ToString("F3", CultureInfo.InvariantCulture));
                args.Add("-i");
                args.Add(filePath);
                args.Add("-map");
                args.Add("0:a:" + audioStreamIndex.ToString(CultureInfo.InvariantCulture));
                args.Add("-vn");
                args.Add("-sn");
                args.Add("-dn");
                args.Add("-ac");
                args.Add("1");
                args.Add("-ar");
                args.Add(sampleRate.ToString(CultureInfo.InvariantCulture));
                args.Add("-f");
                args.Add("s16le");
                args.Add("-");

                processResult = ProcessRunner.RunBinaryStdout(this._ffmpegPath, args.ToArray(), (buffer, bytesRead) =>
                {
                    usableBytes = bytesRead - (bytesRead % 2);
                    for (int i = 0; i < usableBytes; i += 2)
                    {
                        sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                        sumAbs += Math.Abs(sample) / 32768.0;
                        samplesInWindow++;

                        if (samplesInWindow >= windowSamples)
                        {
                            result.Add(sumAbs / samplesInWindow);
                            sumAbs = 0.0;
                            samplesInWindow = 0;
                        }
                    }
                });

                if (samplesInWindow > 0)
                {
                    result.Add(sumAbs / samplesInWindow);
                }

                stopwatch.Stop();
                if (processResult.ExitCode != 0)
                {
                    result.Clear();
                }
            }
            catch (Exception ex)
            {
                if (stopwatch.IsRunning) { stopwatch.Stop(); }
                ConsoleHelper.Write(LogSection.Deep, LogLevel.Warning, "    Refine audio locale fallito: " + ex.Message);
                result.Clear();
            }
            finally
            {
                this._recordExtract?.Invoke(stopwatch.ElapsedMilliseconds);
            }

            if (result.Count < 20)
            {
                return null;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Confronta due finestre envelope audio con indici separati
        /// </summary>
        public double ScoreWindow(double[] sourceEnvelope, double[] languageEnvelope, int sourceStartIndex, int languageStartIndex, int count)
        {
            double result = 0.0;
            double sourceMean = 0.0;
            double languageMean = 0.0;
            double sourceValue;
            double languageValue;
            double numerator = 0.0;
            double sourceNorm = 0.0;
            double languageNorm = 0.0;
            for (int i = 0; i < count; i++)
            {
                sourceMean += sourceEnvelope[sourceStartIndex + i];
                languageMean += languageEnvelope[languageStartIndex + i];
            }

            sourceMean = sourceMean / count;
            languageMean = languageMean / count;

            for (int i = 0; i < count; i++)
            {
                sourceValue = sourceEnvelope[sourceStartIndex + i] - sourceMean;
                languageValue = languageEnvelope[languageStartIndex + i] - languageMean;
                numerator += sourceValue * languageValue;
                sourceNorm += sourceValue * sourceValue;
                languageNorm += languageValue * languageValue;
            }

            if (sourceNorm <= 0.0000001 || languageNorm <= 0.0000001)
            {
                return result;
            }

            result = numerator / Math.Sqrt(sourceNorm * languageNorm);
            result = (result + 1.0) / 2.0;
            if (result < 0.0) { result = 0.0; }
            if (result > 1.0) { result = 1.0; }

            return result;
        }

        #endregion
    }
}
