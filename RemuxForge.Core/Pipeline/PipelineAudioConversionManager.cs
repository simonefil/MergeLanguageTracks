using RemuxForge.Core.Audio;
using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;

namespace RemuxForge.Core.Pipeline
{
    /// <summary>
    /// Conversione tracce audio lossless durante la fase remux
    /// </summary>
    public class PipelineAudioConversionManager
    {
        #region Metodi pubblici

        /// <summary>
        /// Converte le tracce lossless selezionate secondo le opzioni audio
        /// </summary>
        /// <param name="record">Record in elaborazione</param>
        /// <param name="sourceTracks">Tracce sorgente</param>
        /// <param name="sourceAudioIds">ID audio sorgente selezionati</param>
        /// <param name="audioTracks">Tracce audio lingua</param>
        /// <param name="convertedSourceTracks">Output conversioni sorgente</param>
        /// <param name="convertedLangTracks">Output conversioni lingua</param>
        /// <param name="options">Opzioni operative</param>
        /// <param name="ffmpegPath">Percorso ffmpeg</param>
        /// <param name="filterSourceAudio">True se filtrare audio sorgente</param>
        /// <returns>True se tutte le conversioni richieste sono riuscite</returns>
        public bool ConvertLosslessTracks(FileProcessingRecord record, List<TrackInfo> sourceTracks, List<int> sourceAudioIds, List<TrackInfo> audioTracks, Dictionary<int, string> convertedSourceTracks, Dictionary<int, string> convertedLangTracks, Options options, string ffmpegPath, bool filterSourceAudio)
        {
            string episodeLabel = record.EpisodeId.Length > 0 ? record.EpisodeId : "track";
            AudioConversionService convService = new AudioConversionService(ffmpegPath, AppSettingsService.Instance.GetTempFolder(), options.ConvertFormat);
            string convertedFile;
            bool result = true;
            if (sourceTracks != null)
            {
                if (!filterSourceAudio)
                {
                    for (int i = 0; i < sourceTracks.Count; i++)
                    {
                        if (string.Equals(sourceTracks[i].Type, "audio", StringComparison.OrdinalIgnoreCase))
                        {
                            sourceAudioIds.Add(sourceTracks[i].Id);
                        }
                    }
                }

                for (int i = 0; i < sourceTracks.Count; i++)
                {
                    TrackInfo srcTrack = sourceTracks[i];
                    if (!string.Equals(srcTrack.Type, "audio", StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (!sourceAudioIds.Contains(srcTrack.Id)) { continue; }
                    if (!CodecMapping.IsConvertibleLossless(srcTrack, options.ConvertFormat)) { continue; }

                    ConsoleHelper.Write(LogSection.Conv, LogLevel.Phase, "  Sorgente traccia " + srcTrack.Id + " (" + srcTrack.Codec + " " + srcTrack.Channels + "ch)");
                    convertedFile = convService.ConvertTrack(record.SourceFilePath, srcTrack.Id, srcTrack.Channels, "src_" + episodeLabel);
                    if (convertedFile.Length > 0) { convertedSourceTracks[srcTrack.Id] = convertedFile; }
                    else
                    {
                        this.FailConversion(record, "Conversione audio sorgente fallita sulla traccia " + srcTrack.Id);
                        result = false;
                        break;
                    }
                }
            }

            if (result)
            {
                for (int i = 0; i < audioTracks.Count; i++)
                {
                    TrackInfo langTrack = audioTracks[i];
                    if (!CodecMapping.IsConvertibleLossless(langTrack, options.ConvertFormat)) { continue; }

                    ConsoleHelper.Write(LogSection.Conv, LogLevel.Phase, "  Lingua traccia " + langTrack.Id + " (" + langTrack.Codec + " " + langTrack.Channels + "ch)");
                    convertedFile = convService.ConvertTrack(record.LangFilePath, langTrack.Id, langTrack.Channels, "lang_" + episodeLabel);
                    if (convertedFile.Length > 0) { convertedLangTracks[langTrack.Id] = convertedFile; }
                    else
                    {
                        this.FailConversion(record, "Conversione audio lingua fallita sulla traccia " + langTrack.Id);
                        result = false;
                        break;
                    }
                }
            }

            if (result && (convertedSourceTracks.Count > 0 || convertedLangTracks.Count > 0))
            {
                ConsoleHelper.Write(LogSection.Conv, LogLevel.Success, "  Convertite " + (convertedSourceTracks.Count + convertedLangTracks.Count) + " tracce");
            }

            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Registra errore hard di conversione audio richiesta
        /// </summary>
        /// <param name="record">Record in elaborazione</param>
        /// <param name="message">Messaggio errore</param>
        private void FailConversion(FileProcessingRecord record, string message)
        {
            ConsoleHelper.Write(LogSection.Conv, LogLevel.Error, "  " + message);
            record.ErrorMessage = message;
            record.Status = FileStatus.Error;
        }

        #endregion
    }
}
