using RemuxForge.Core.Media.Mkv;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;

namespace RemuxForge.Core.Pipeline
{
    /// <summary>
    /// Costruzione preview comando mkvmerge per record analizzati
    /// </summary>
    public class PipelineMergePreviewBuilder
    {
        #region Variabili di classe

        private PipelineTrackMapper _trackMapper;
        private PipelineOutputManager _outputManager;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="trackMapper">Mapper tracce pipeline</param>
        /// <param name="outputManager">Gestore output pipeline</param>
        public PipelineMergePreviewBuilder(PipelineTrackMapper trackMapper, PipelineOutputManager outputManager)
        {
            this._trackMapper = trackMapper;
            this._outputManager = outputManager;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Costruisce anteprima e argomenti merge per un record
        /// </summary>
        /// <param name="record">Record in elaborazione</param>
        /// <param name="options">Opzioni operative</param>
        /// <param name="mkvService">Servizio mkvmerge</param>
        /// <param name="fileInfoProvider">Provider metadata file</param>
        /// <param name="needsMerge">True se serve merge</param>
        /// <param name="needsRemux">True se serve remux</param>
        /// <param name="filterSourceAudio">True se filtrare audio sorgente</param>
        /// <param name="filterSourceSubs">True se filtrare sottotitoli sorgente</param>
        /// <param name="codecPatterns">Pattern codec lingua</param>
        /// <param name="sourceAudioCodecPatterns">Pattern codec audio sorgente</param>
        public void Build(FileProcessingRecord record, Options options, MkvToolsService mkvService, Func<string, MkvFileInfo> fileInfoProvider, bool needsMerge, bool needsRemux, bool filterSourceAudio, bool filterSourceSubs, string[] codecPatterns, string[] sourceAudioCodecPatterns)
        {
            int effectiveAudioDelay = record.SyncOffsetMs + options.AudioDelay + record.ManualAudioDelayMs;
            int effectiveSubDelay = record.SyncOffsetMs + options.SubtitleDelay + record.ManualSubDelayMs;
            string stretchFactor = record.StretchFactor;
            MkvFileInfo sourceInfo;
            List<TrackInfo> sourceTracks;
            List<TrackInfo> langTracks = null;
            List<int> sourceAudioIds = new List<int>();
            List<int> sourceSubIds = new List<int>();
            List<TrackInfo> audioTracks = new List<TrackInfo>();
            List<TrackInfo> subtitleTracks = new List<TrackInfo>();
            string outputPath;
            List<string> mergeArgs;
            bool hasWork;
            sourceInfo = fileInfoProvider(record.SourceFilePath);
            sourceTracks = (sourceInfo != null) ? sourceInfo.Tracks : null;

            if (needsMerge && record.LangFilePath.Length > 0)
            {
                MkvFileInfo langInfo = fileInfoProvider(record.LangFilePath);
                langTracks = (langInfo != null) ? langInfo.Tracks : null;
            }

            if (sourceTracks != null)
            {
                if (filterSourceAudio)
                {
                    sourceAudioIds = mkvService.GetSourceTrackIds(sourceTracks, "audio", options.KeepSourceAudioLangs, sourceAudioCodecPatterns);
                }
                if (filterSourceSubs)
                {
                    sourceSubIds = mkvService.GetSourceTrackIds(sourceTracks, "subtitles", options.KeepSourceSubtitleLangs, null);
                }

                if (needsMerge && langTracks != null)
                {
                    this._trackMapper.CollectLanguageTracks(record, langTracks, mkvService, options, codecPatterns, out audioTracks, out subtitleTracks);
                }

                hasWork = needsMerge ? (audioTracks.Count > 0 || subtitleTracks.Count > 0) : needsRemux;

                record.KeptSourceAudioIds = sourceAudioIds;
                record.KeptSourceSubIds = sourceSubIds;
                record.ImportedAudioTracks = audioTracks;
                record.ImportedSubTracks = subtitleTracks;
                record.DisplayAudioFormat = options.AudioFormat;

                if (hasWork)
                {
                    outputPath = this._outputManager.ComputeFinalOutputPath(record.SourceFilePath, options);

                    MergeRequest mergeReq = new MergeRequest();
                    mergeReq.SourceFile = record.SourceFilePath;
                    mergeReq.LanguageFile = needsMerge ? record.LangFilePath : "";
                    mergeReq.OutputFile = outputPath;
                    mergeReq.SourceAudioIds = sourceAudioIds;
                    mergeReq.SourceAudioTracks = this._trackMapper.FilterTracksByIds(sourceTracks, sourceAudioIds);
                    mergeReq.SourceSubIds = sourceSubIds;
                    mergeReq.LangAudioTracks = audioTracks;
                    mergeReq.LangSubTracks = subtitleTracks;
                    mergeReq.AudioDelayMs = effectiveAudioDelay;
                    mergeReq.SubDelayMs = effectiveSubDelay;
                    mergeReq.FilterSourceAudio = filterSourceAudio;
                    mergeReq.FilterSourceSubs = filterSourceSubs;
                    mergeReq.StretchFactor = stretchFactor;
                    mergeReq.AudioFormat = options.AudioFormat;
                    mergeReq.AudioRenameScope = options.AudioRenameScope;
                    mergeReq.SourceTitle = (sourceInfo != null) ? sourceInfo.ContainerTitle : "";
                    mergeReq.ConvertedSourceTracks = new Dictionary<int, string>();
                    mergeReq.ConvertedLangTracks = new Dictionary<int, string>();
                    mergeArgs = mkvService.BuildMergeArguments(mergeReq);

                    record.MergeCommand = mkvService.FormatMergeCommand(mergeArgs);
                }
            }
        }

        #endregion
    }
}
