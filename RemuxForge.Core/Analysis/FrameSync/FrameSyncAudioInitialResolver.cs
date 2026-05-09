using RemuxForge.Core.Audio;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Diagnostics;
using System.Globalization;

namespace RemuxForge.Core.Analysis.FrameSync
{
    /// <summary>
    /// Risolve e verifica candidati initial tramite fingerprint audio globale
    /// </summary>
    public class FrameSyncAudioInitialResolver
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso eseguibile ffmpeg
        /// </summary>
        private readonly string _ffmpegPath;

        /// <summary>
        /// Configurazione FrameSync
        /// </summary>
        private readonly FrameSyncConfig _frameSyncConfig;

        /// <summary>
        /// Configurazione ffmpeg
        /// </summary>
        private readonly FfmpegConfig _ffmpegConfig;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public FrameSyncAudioInitialResolver(string ffmpegPath, FrameSyncConfig frameSyncConfig, FfmpegConfig ffmpegConfig)
        {
            this._ffmpegPath = ffmpegPath;
            this._frameSyncConfig = frameSyncConfig;
            this._ffmpegConfig = ffmpegConfig;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Usa la fingerprint audio globale per proporre un delay iniziale quando il video non conclude
        /// </summary>
        public int FindInitialDelay(string sourceFile, string languageFile, bool applyCandidate, int visualOffsetMs, FrameSyncInitialResult initialResult, FrameSyncTimingInfo timing, out AudioGlobalFingerprintResult audioGlobalResult)
        {
            int result = int.MinValue;
            Stopwatch stopwatch = new Stopwatch();
            AudioGlobalFingerprintService service;
            FrameSyncCandidate candidate;
            ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Notice, "  Fingerprint audio globale: ricerca candidato su traccia intera...");

            stopwatch.Start();
            service = new AudioGlobalFingerprintService(this._ffmpegPath, this._frameSyncConfig, this._ffmpegConfig);
            audioGlobalResult = service.FindOffset(sourceFile, languageFile);
            stopwatch.Stop();

            timing.AudioGlobalMs += stopwatch.ElapsedMilliseconds;

            if (audioGlobalResult.Success)
            {
                if (applyCandidate)
                {
                    result = audioGlobalResult.OffsetMs;
                }

                candidate = new FrameSyncCandidate();
                candidate.OffsetMs = audioGlobalResult.OffsetMs;
                candidate.Source = FrameSyncCandidate.AUDIO_GLOBAL;
                candidate.VoteCount = audioGlobalResult.CandidateCount;
                candidate.MatchedCuts = 0;
                candidate.TemporalScore = audioGlobalResult.SilenceScore;
                candidate.VisualScore = 0.0;
                candidate.CombinedScore = audioGlobalResult.Score;
                candidate.SecondBestScore = audioGlobalResult.Score - audioGlobalResult.Margin;
                candidate.Margin = audioGlobalResult.Margin;

                initialResult.Candidates.Add(candidate);

                if (applyCandidate)
                {
                    initialResult.BestCandidate = candidate;
                    initialResult.Success = true;
                    initialResult.Ambiguous = false;
                    initialResult.FailureReason = "";
                }

                ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Notice, "  Audio globale propone offset=" + audioGlobalResult.OffsetMs + "ms, score=" + audioGlobalResult.Score.ToString("F3", CultureInfo.InvariantCulture) + ", margin=" + audioGlobalResult.Margin.ToString("F3", CultureInfo.InvariantCulture) + ", coverage=" + audioGlobalResult.Coverage.ToString("F2", CultureInfo.InvariantCulture));
                if (!applyCandidate && visualOffsetMs != int.MinValue)
                {
                    int delta = Math.Abs(audioGlobalResult.OffsetMs - visualOffsetMs);
                    ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Notice, "  Audio globale vs video iniziale: video=" + visualOffsetMs + "ms, audio=" + audioGlobalResult.OffsetMs + "ms, delta=" + delta + "ms");
                }
            }
            else
            {
                ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Warning, "  Audio globale non conclusivo: " + audioGlobalResult.FailureReason + ", score=" + audioGlobalResult.Score.ToString("F3", CultureInfo.InvariantCulture) + ", margin=" + audioGlobalResult.Margin.ToString("F3", CultureInfo.InvariantCulture));
            }

            return result;
        }

        /// <summary>
        /// Verifica un initial visuale debole contro l'audio globale
        /// </summary>
        public bool VerifyVisualInitial(string sourceFile, string languageFile, int visualOffsetMs, double frameIntervalMs, FrameSyncInitialResult initialResult, FrameSyncTimingInfo timing, out AudioGlobalFingerprintResult audioGlobalResult)
        {
            bool result = false;
            int delta;
            int confirmToleranceMs;
            int rejectToleranceMs;
            this.FindInitialDelay(sourceFile, languageFile, false, visualOffsetMs, initialResult, timing, out audioGlobalResult);

            if (audioGlobalResult == null || !audioGlobalResult.Success)
            {
                return result;
            }

            delta = Math.Abs(audioGlobalResult.OffsetMs - visualOffsetMs);
            confirmToleranceMs = (int)Math.Round(frameIntervalMs * this._frameSyncConfig.AudioGlobalConfirmToleranceFrames);
            rejectToleranceMs = (int)Math.Round(frameIntervalMs * this._frameSyncConfig.AudioGlobalRejectToleranceFrames);
            audioGlobalResult.VideoOffsetMs = visualOffsetMs;
            audioGlobalResult.AudioVideoDeltaMs = delta;

            if (delta <= confirmToleranceMs)
            {
                audioGlobalResult.ConfirmedVideoInitial = true;
                ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Notice, "  Audio globale conferma initial video debole: delta=" + delta + "ms");
            }
            else if (delta > rejectToleranceMs)
            {
                audioGlobalResult.RejectedVideoInitial = true;
                initialResult.FailureReason = "Audio globale divergente da initial video debole";
                ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Warning, "  Audio globale boccia initial video debole: video=" + visualOffsetMs + "ms, audio=" + audioGlobalResult.OffsetMs + "ms, delta=" + delta + "ms");
                return false;
            }
            else
            {
                ConsoleHelper.Write(LogSection.FrameSync, LogLevel.Notice, "  Audio globale non decide initial video debole: delta=" + delta + "ms");
            }

            return result;
        }

        #endregion
    }
}
