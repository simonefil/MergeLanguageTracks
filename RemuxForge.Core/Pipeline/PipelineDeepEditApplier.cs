using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System.Collections.Generic;
using System.IO;

namespace RemuxForge.Core.Pipeline
{
    /// <summary>
    /// Applicazione EditMap deep-analysis alle tracce importate
    /// </summary>
    public class PipelineDeepEditApplier
    {
        #region Metodi pubblici

        /// <summary>
        /// Applica l'EditMap DeepAnalysis alle tracce audio e sottotitoli importate
        /// </summary>
        /// <param name="record">Record in elaborazione</param>
        /// <param name="audioTracks">Tracce audio lingua</param>
        /// <param name="subtitleTracks">Tracce sottotitolo lingua</param>
        /// <param name="convertedLangTracks">Tracce audio lingua gia' convertite</param>
        /// <param name="processedLangSubTracks">Tracce sottotitolo processate</param>
        /// <param name="audioEditMap">EditMap da applicare alle tracce audio, null per usare quella del record</param>
        /// <param name="options">Opzioni operative</param>
        /// <param name="ffmpegPath">Percorso ffmpeg</param>
        /// <returns>True se l'applicazione e' completata</returns>
        public bool Apply(FileProcessingRecord record, List<TrackInfo> audioTracks, List<TrackInfo> subtitleTracks, Dictionary<int, string> convertedLangTracks, Dictionary<int, string> processedLangSubTracks, EditMap audioEditMap, Options options, string ffmpegPath)
        {
            string tempFolder;
            TrackSplitService splitService;
            string splitLabel;
            string processedFile;
            EditMap effectiveAudioEditMap = audioEditMap != null ? audioEditMap : record.DeepAnalysisMap;
            if (!record.DeepAnalysisApplied || record.DeepAnalysisMap == null || record.DeepAnalysisMap.Operations.Count == 0)
            {
                return false;
            }

            if (ffmpegPath.Length == 0)
            {
                this.FailApply(record, "Deep analysis richiede ffmpeg per applicare tagli/insert alle tracce importate");
                return false;
            }

            ConsoleHelper.Write(LogSection.Deep, LogLevel.Phase, "  Applicazione taglia-cuci alle tracce lang...");
            ConsoleHelper.Progress(LogSection.Deep, 92, "Deep: taglia-cuci");

            tempFolder = AppSettingsService.Instance.GetTempFolder();
            splitService = new TrackSplitService(ffmpegPath, tempFolder, options.MkvMergePath);
            splitLabel = Path.GetFileNameWithoutExtension(record.LangFilePath);

            for (int a = 0; a < audioTracks.Count; a++)
            {
                if (effectiveAudioEditMap == null || effectiveAudioEditMap.Operations.Count == 0)
                {
                    break;
                }

                string audioInput = record.LangFilePath;
                int audioTrackId = audioTracks[a].Id;

                if (convertedLangTracks.ContainsKey(audioTrackId))
                {
                    audioInput = convertedLangTracks[audioTrackId];
                    processedFile = splitService.ApplyEditMap(audioInput, 0, "audio", options.ConvertFormat, audioTracks[a].Channels, audioTracks[a].SamplingFrequency, effectiveAudioEditMap, splitLabel);
                }
                else
                {
                    processedFile = splitService.ApplyEditMap(audioInput, audioTrackId, "audio", audioTracks[a].Codec, audioTracks[a].Channels, audioTracks[a].SamplingFrequency, effectiveAudioEditMap, splitLabel);
                }

                if (processedFile.Length > 0)
                {
                    if (convertedLangTracks.ContainsKey(audioTrackId))
                    {
                        FileHelper.DeleteTempFile(convertedLangTracks[audioTrackId]);
                    }
                    convertedLangTracks[audioTrackId] = processedFile;
                }
                else
                {
                    this.FailApply(record, "Deep analysis fallita sulla traccia audio lang " + audioTrackId);
                    return false;
                }
            }

            for (int s = 0; s < subtitleTracks.Count; s++)
            {
                processedFile = splitService.ApplyEditMap(record.LangFilePath, subtitleTracks[s].Id, "subtitles", subtitleTracks[s].Codec, 0, 0, record.DeepAnalysisMap, splitLabel);

                if (processedFile.Length > 0)
                {
                    processedLangSubTracks[subtitleTracks[s].Id] = processedFile;
                }
                else
                {
                    this.FailApply(record, "Deep analysis fallita sulla traccia sottotitoli lang " + subtitleTracks[s].Id);
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Registra un fallimento hard dell'applicazione edit map
        /// </summary>
        /// <param name="record">Record in elaborazione</param>
        /// <param name="message">Messaggio errore</param>
        private void FailApply(FileProcessingRecord record, string message)
        {
            ConsoleHelper.Write(LogSection.Deep, LogLevel.Error, "  " + message);
            record.ErrorMessage = message;
            record.Status = FileStatus.Error;
        }

        #endregion
    }
}
