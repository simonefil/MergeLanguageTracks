using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Media.Mkv;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RemuxForge.Core.Audio
{
    /// <summary>
    /// Crea tracce audio importate riempiendo porzioni mancanti con audio del source
    /// </summary>
    public class AudioSourceFillService
    {
        #region Variabili di classe

        private readonly string _ffmpegPath;
        private readonly string _tempFolder;
        private readonly MkvToolsService _mkvToolsService;

        #endregion

        #region Costruttore

        public AudioSourceFillService(string ffmpegPath, string tempFolder, MkvToolsService mkvToolsService)
        {
            this._ffmpegPath = ffmpegPath;
            this._tempFolder = tempFolder;
            this._mkvToolsService = mkvToolsService;
        }

        #endregion

        #region Metodi pubblici

        public bool Apply(FileProcessingRecord record, MkvFileInfo sourceInfo, MkvFileInfo langInfo, List<TrackInfo> langAudioTracks, Options options, int delayMs, Dictionary<int, string> convertedLangTracks, HashSet<int> audioDelayBypassedLangIds, Dictionary<int, string> processedLangAudioFormats, out int outputDelayMs, out EditMap audioEditMap)
        {
            bool result = true;
            bool anyProcessed = false;
            bool startApplied = false;
            List<EditOperation> fillInsertOperations;

            outputDelayMs = delayMs;
            audioEditMap = this.CloneEditMap(record.DeepAnalysisMap);

            if (!this.IsActive(options) || langAudioTracks == null || langAudioTracks.Count == 0)
            {
                return result;
            }

            fillInsertOperations = this.GetFillInsertOperations(record.DeepAnalysisMap, options);
            if (!this.NeedsAnyFill(sourceInfo, langInfo, langAudioTracks, options, delayMs, fillInsertOperations))
            {
                return result;
            }

            if (sourceInfo == null || sourceInfo.Tracks == null || sourceInfo.Tracks.Count == 0)
            {
                return this.Fail(record, "Audio source fill richiesto ma le tracce source non sono disponibili");
            }

            ConsoleHelper.Write(LogSection.Conv, LogLevel.Phase, "  Audio source fill: soglia " + options.AudioSourceFillThresholdMs + "ms, sorgente " + options.AudioSourceFillLanguage);

            for (int i = 0; i < langAudioTracks.Count; i++)
            {
                TrackInfo langTrack = langAudioTracks[i];
                TrackInfo sourceTrack = this.SelectSourceTrack(sourceInfo.Tracks, options.AudioSourceFillLanguage, langTrack);
                if (sourceTrack == null)
                {
                    return this.Fail(record, "Audio source fill fallito: nessuna traccia audio source in lingua " + options.AudioSourceFillLanguage + " per la traccia lang " + langTrack.Id);
                }

                AudioSourceFillPlan plan = this.BuildPlan(sourceInfo, langInfo, sourceTrack, langTrack, options, delayMs, fillInsertOperations);
                if (!plan.HasWork)
                {
                    continue;
                }

                string outputFormat = this.ResolveOutputFormat(langTrack, options);
                string outputFile = this.BuildFilledTrack(record.SourceFilePath, record.LangFilePath, sourceTrack, langTrack, plan, outputFormat, record.EpisodeId.Length > 0 ? record.EpisodeId : "track");
                if (outputFile.Length == 0)
                {
                    return this.Fail(record, "Audio source fill fallito: impossibile creare traccia riempita per lang track " + langTrack.Id + " usando source track " + sourceTrack.Id);
                }

                if (convertedLangTracks.ContainsKey(langTrack.Id))
                {
                    FileHelper.DeleteTempFile(convertedLangTracks[langTrack.Id]);
                }
                convertedLangTracks[langTrack.Id] = outputFile;
                processedLangAudioFormats[langTrack.Id] = outputFormat;
                if (plan.StartFillMs > 0)
                {
                    audioDelayBypassedLangIds.Add(langTrack.Id);
                    startApplied = true;
                }

                anyProcessed = true;
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Success, "  Audio source fill traccia lang " + langTrack.Id + " -> " + outputFormat.ToUpperInvariant() + " (" + Path.GetFileName(outputFile) + ")");
            }

            if (anyProcessed && startApplied)
            {
                outputDelayMs = 0;
            }

            if (anyProcessed && fillInsertOperations.Count > 0 && audioEditMap != null)
            {
                this.RemoveFilledInsertOperations(audioEditMap, fillInsertOperations);
            }

            return result;
        }

        #endregion

        #region Metodi privati

        private bool IsActive(Options options)
        {
            return options.AudioSourceFillThresholdMs > 0 &&
                options.AudioSourceFillLanguage.Length > 0 &&
                (options.AudioSourceFillStart || options.AudioSourceFillEnd || options.AudioSourceFillInsertSilence);
        }

        private bool NeedsAnyFill(MkvFileInfo sourceInfo, MkvFileInfo langInfo, List<TrackInfo> langAudioTracks, Options options, int delayMs, List<EditOperation> fillInsertOperations)
        {
            if (options.AudioSourceFillStart && delayMs > options.AudioSourceFillThresholdMs)
            {
                return true;
            }

            if (options.AudioSourceFillInsertSilence && fillInsertOperations.Count > 0)
            {
                return true;
            }

            if (options.AudioSourceFillEnd && sourceInfo != null && langInfo != null)
            {
                for (int i = 0; i < langAudioTracks.Count; i++)
                {
                    TrackInfo langTrack = langAudioTracks[i];
                    TrackInfo sourceTrack = this.SelectSourceTrack(sourceInfo.Tracks, options.AudioSourceFillLanguage, langTrack);
                    if (sourceTrack != null)
                    {
                        int sourceDurationMs = this.ResolveSourceReferenceDurationMs(sourceInfo, sourceTrack);
                        int langDurationMs = this.ResolveTrackDurationMs(langInfo, langTrack);
                        if (sourceDurationMs > 0 && langDurationMs > 0 && sourceDurationMs - (langDurationMs + delayMs) > options.AudioSourceFillThresholdMs)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private AudioSourceFillPlan BuildPlan(MkvFileInfo sourceInfo, MkvFileInfo langInfo, TrackInfo sourceTrack, TrackInfo langTrack, Options options, int delayMs, List<EditOperation> fillInsertOperations)
        {
            AudioSourceFillPlan result = new AudioSourceFillPlan();
            int sourceDurationMs = this.ResolveSourceReferenceDurationMs(sourceInfo, sourceTrack);
            int langDurationMs = this.ResolveTrackDurationMs(langInfo, langTrack);

            if (options.AudioSourceFillStart && delayMs > options.AudioSourceFillThresholdMs)
            {
                result.StartFillMs = delayMs;
            }

            if (options.AudioSourceFillEnd && sourceDurationMs > 0 && langDurationMs > 0)
            {
                int endFillMs = sourceDurationMs - (langDurationMs + delayMs);
                if (endFillMs > options.AudioSourceFillThresholdMs)
                {
                    result.EndFillMs = endFillMs;
                    result.SourceDurationMs = sourceDurationMs;
                }
            }

            if (options.AudioSourceFillInsertSilence)
            {
                for (int i = 0; i < fillInsertOperations.Count; i++)
                {
                    result.InsertOperations.Add(fillInsertOperations[i]);
                }
            }

            return result;
        }

        private List<EditOperation> GetFillInsertOperations(EditMap editMap, Options options)
        {
            List<EditOperation> result = new List<EditOperation>();
            if (!options.AudioSourceFillInsertSilence || editMap == null || editMap.Operations == null)
            {
                return result;
            }

            for (int i = 0; i < editMap.Operations.Count; i++)
            {
                EditOperation operation = editMap.Operations[i];
                if (string.Equals(operation.Type, EditOperation.INSERT_SILENCE, StringComparison.Ordinal) && operation.DurationMs > options.AudioSourceFillThresholdMs)
                {
                    result.Add(operation);
                }
            }

            return result;
        }

        private void RemoveFilledInsertOperations(EditMap editMap, List<EditOperation> filledOperations)
        {
            for (int i = editMap.Operations.Count - 1; i >= 0; i--)
            {
                for (int f = 0; f < filledOperations.Count; f++)
                {
                    if (this.IsSameOperation(editMap.Operations[i], filledOperations[f]))
                    {
                        editMap.Operations.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private bool IsSameOperation(EditOperation left, EditOperation right)
        {
            return left != null && right != null &&
                string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
                left.LangTimestampMs == right.LangTimestampMs &&
                left.SourceTimestampMs == right.SourceTimestampMs &&
                left.DurationMs == right.DurationMs;
        }

        private EditMap CloneEditMap(EditMap source)
        {
            EditMap result = null;
            if (source != null)
            {
                result = new EditMap();
                result.InitialDelayMs = source.InitialDelayMs;
                result.StretchFactor = source.StretchFactor;
                result.AnalysisTimeMs = source.AnalysisTimeMs;
                result.BaselineMse = source.BaselineMse;
                result.Diagnostics = source.Diagnostics;
                for (int i = 0; i < source.Operations.Count; i++)
                {
                    EditOperation operation = new EditOperation();
                    operation.Type = source.Operations[i].Type;
                    operation.LangTimestampMs = source.Operations[i].LangTimestampMs;
                    operation.SourceTimestampMs = source.Operations[i].SourceTimestampMs;
                    operation.DurationMs = source.Operations[i].DurationMs;
                    result.Operations.Add(operation);
                }
            }

            return result;
        }

        private TrackInfo SelectSourceTrack(List<TrackInfo> sourceTracks, string sourceLanguage, TrackInfo langTrack)
        {
            TrackInfo result = null;

            for (int i = 0; i < sourceTracks.Count; i++)
            {
                TrackInfo candidate = sourceTracks[i];
                if (!string.Equals(candidate.Type, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!this._mkvToolsService.IsLanguageMatch(candidate, sourceLanguage))
                {
                    continue;
                }
                if (result == null || this.IsBetterSourceTrack(candidate, result, langTrack))
                {
                    result = candidate;
                }
            }

            return result;
        }

        private bool IsBetterSourceTrack(TrackInfo candidate, TrackInfo current, TrackInfo langTrack)
        {
            bool candidateSameChannels = candidate.Channels == langTrack.Channels;
            bool currentSameChannels = current.Channels == langTrack.Channels;

            if (candidateSameChannels != currentSameChannels)
            {
                return candidateSameChannels;
            }

            return candidate.Bitrate > current.Bitrate;
        }

        private int ResolveSourceReferenceDurationMs(MkvFileInfo sourceInfo, TrackInfo sourceTrack)
        {
            int result = this.ResolveTrackDurationMs(sourceInfo, sourceTrack);
            if (result <= 0)
            {
                result = this.ResolveVideoDurationMs(sourceInfo);
            }

            return result;
        }

        private int ResolveTrackDurationMs(MkvFileInfo fileInfo, TrackInfo track)
        {
            if (track != null && track.TrackDurationNs > 0)
            {
                return (int)Math.Round(track.TrackDurationNs / 1000000.0);
            }

            return this.ResolveVideoDurationMs(fileInfo);
        }

        private int ResolveVideoDurationMs(MkvFileInfo fileInfo)
        {
            if (fileInfo != null && fileInfo.Tracks != null)
            {
                for (int i = 0; i < fileInfo.Tracks.Count; i++)
                {
                    TrackInfo track = fileInfo.Tracks[i];
                    if (string.Equals(track.Type, "video", StringComparison.OrdinalIgnoreCase) && track.TrackDurationNs > 0)
                    {
                        return (int)Math.Round(track.TrackDurationNs / 1000000.0);
                    }
                }
            }

            return fileInfo != null && fileInfo.ContainerDurationNs > 0 ? (int)Math.Round(fileInfo.ContainerDurationNs / 1000000.0) : 0;
        }

        private string ResolveOutputFormat(TrackInfo langTrack, Options options)
        {
            if (options.ConvertFormat.Length > 0)
            {
                return options.ConvertFormat;
            }

            return CodecMapping.IsLosslessCodec(langTrack.Codec) ? "flac" : "opus";
        }

        private string BuildFilledTrack(string sourceFile, string languageFile, TrackInfo sourceTrack, TrackInfo langTrack, AudioSourceFillPlan plan, string outputFormat, string episodeLabel)
        {
            string extension = string.Equals(outputFormat, "flac", StringComparison.OrdinalIgnoreCase) ? ".flac" : ".ogg";
            string outputFile = Path.Combine(this._tempFolder, "source_fill_" + episodeLabel + "_t" + langTrack.Id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
            List<string> args = new List<string>();
            string filter = this.BuildFilter(sourceTrack, langTrack, plan, outputFormat);
            ProcessResult processResult;

            args.Add("-y");
            args.Add("-i");
            args.Add(sourceFile);
            args.Add("-i");
            args.Add(languageFile);
            args.Add("-filter_complex");
            args.Add(filter);
            args.Add("-map");
            args.Add("[outa]");
            this.AddCodecArgs(args, langTrack, outputFormat);
            args.Add(outputFile);

            processResult = ProcessRunner.Run(this._ffmpegPath, args.ToArray());
            if (processResult.ExitCode == 0 && File.Exists(outputFile))
            {
                return outputFile;
            }

            FileHelper.DeleteTempFile(outputFile);
            if (processResult.Stderr.Length > 0)
            {
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Debug, "  ffmpeg: " + this.LastErrorLine(processResult.Stderr));
            }
            return "";
        }

        private string BuildFilter(TrackInfo sourceTrack, TrackInfo langTrack, AudioSourceFillPlan plan, string outputFormat)
        {
            List<AudioFilterSegment> segments = new List<AudioFilterSegment>();
            int currentLangMs = 0;

            if (plan.StartFillMs > 0)
            {
                segments.Add(new AudioFilterSegment(0, sourceTrack.Id, 0, plan.StartFillMs));
            }

            plan.InsertOperations.Sort((a, b) => a.LangTimestampMs.CompareTo(b.LangTimestampMs));
            for (int i = 0; i < plan.InsertOperations.Count; i++)
            {
                EditOperation operation = plan.InsertOperations[i];
                if (operation.LangTimestampMs > currentLangMs)
                {
                    segments.Add(new AudioFilterSegment(1, langTrack.Id, currentLangMs, operation.LangTimestampMs));
                }

                segments.Add(new AudioFilterSegment(0, sourceTrack.Id, operation.SourceTimestampMs, operation.SourceTimestampMs + operation.DurationMs));
                currentLangMs = operation.LangTimestampMs;
            }

            segments.Add(new AudioFilterSegment(1, langTrack.Id, currentLangMs, -1));

            if (plan.EndFillMs > 0 && plan.SourceDurationMs > plan.EndFillMs)
            {
                segments.Add(new AudioFilterSegment(0, sourceTrack.Id, plan.SourceDurationMs - plan.EndFillMs, plan.SourceDurationMs));
            }

            return this.BuildConcatFilter(segments, langTrack, outputFormat);
        }

        private string BuildConcatFilter(List<AudioFilterSegment> segments, TrackInfo langTrack, string outputFormat)
        {
            string sampleRate = this.ResolveSampleRate(langTrack, outputFormat).ToString(CultureInfo.InvariantCulture);
            string layout = AudioChannelHelper.GetChannelLayout(langTrack.Channels);
            string sampleFmt = this.ResolveSampleFormat(langTrack, outputFormat);
            string aformat = "aformat=sample_rates=" + sampleRate + ":channel_layouts=" + layout;
            string filter = "";
            string concatInputs = "";

            if (sampleFmt.Length > 0)
            {
                aformat += ":sample_fmts=" + sampleFmt;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                AudioFilterSegment segment = segments[i];
                string label = "a" + i.ToString(CultureInfo.InvariantCulture);
                filter += "[" + segment.InputIndex + ":" + segment.TrackId + "]atrim=start=" + (segment.StartMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture);
                if (segment.EndMs > 0)
                {
                    filter += ":end=" + (segment.EndMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture);
                }
                filter += ",asetpts=PTS-STARTPTS," + aformat + "[" + label + "];";
                concatInputs += "[" + label + "]";
            }

            filter += concatInputs + "concat=n=" + segments.Count + ":v=0:a=1[outa]";
            return filter;
        }

        private int ResolveSampleRate(TrackInfo langTrack, string outputFormat)
        {
            if (string.Equals(outputFormat, "opus", StringComparison.OrdinalIgnoreCase))
            {
                return 48000;
            }

            return langTrack.SamplingFrequency > 0 ? langTrack.SamplingFrequency : 48000;
        }

        private string ResolveSampleFormat(TrackInfo langTrack, string outputFormat)
        {
            if (string.Equals(outputFormat, "opus", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return langTrack.BitsPerSample > 16 ? "s32" : "s16";
        }

        private void AddCodecArgs(List<string> args, TrackInfo langTrack, string outputFormat)
        {
            if (string.Equals(outputFormat, "flac", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("-c:a");
                args.Add("flac");
                args.Add("-compression_level");
                args.Add(AppSettingsService.Instance.Settings.Flac.CompressionLevel.ToString(CultureInfo.InvariantCulture));
                args.Add("-ar");
                args.Add(this.ResolveSampleRate(langTrack, outputFormat).ToString(CultureInfo.InvariantCulture));
                args.Add("-sample_fmt");
                args.Add(this.ResolveSampleFormat(langTrack, outputFormat));
                if (langTrack.BitsPerSample > 0)
                {
                    args.Add("-bits_per_raw_sample");
                    args.Add(langTrack.BitsPerSample.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                int bitrate = AppSettingsService.Instance.GetOpusBitrateForChannels(langTrack.Channels);
                string standardLayout = AudioChannelHelper.GetStandardChannelLayout(langTrack.Channels);
                args.Add("-c:a");
                args.Add("libopus");
                args.Add("-b:a");
                args.Add(bitrate + "k");
                if (standardLayout.Length > 0)
                {
                    args.Add("-mapping_family");
                    args.Add("1");
                }
            }
        }

        private string LastErrorLine(string text)
        {
            string[] lines = text.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return "";
        }

        private bool Fail(FileProcessingRecord record, string message)
        {
            ConsoleHelper.Write(LogSection.Conv, LogLevel.Error, "  " + message);
            record.ErrorMessage = message;
            record.Status = FileStatus.Error;
            return false;
        }

        #endregion

        #region Classi annidate

        private class AudioSourceFillPlan
        {
            public AudioSourceFillPlan()
            {
                this.InsertOperations = new List<EditOperation>();
            }

            public int StartFillMs { get; set; }

            public int EndFillMs { get; set; }

            public int SourceDurationMs { get; set; }

            public List<EditOperation> InsertOperations { get; set; }

            public bool HasWork
            {
                get { return this.StartFillMs > 0 || this.EndFillMs > 0 || this.InsertOperations.Count > 0; }
            }
        }

        private class AudioFilterSegment
        {
            public AudioFilterSegment(int inputIndex, int trackId, int startMs, int endMs)
            {
                this.InputIndex = inputIndex;
                this.TrackId = trackId;
                this.StartMs = startMs;
                this.EndMs = endMs;
            }

            public int InputIndex { get; set; }

            public int TrackId { get; set; }

            public int StartMs { get; set; }

            public int EndMs { get; set; }
        }

        #endregion
    }
}
