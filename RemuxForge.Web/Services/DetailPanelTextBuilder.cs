using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Localization;
using RemuxForge.Core.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RemuxForge.Web.Services
{
    /// <summary>
    /// Costruisce il testo del pannello dettaglio episodio
    /// </summary>
    public class DetailPanelTextBuilder
    {
        #region Metodi pubblici

        /// <summary>
        /// Costruisce il testo dettaglio per il record selezionato
        /// </summary>
        /// <param name="record">Record selezionato</param>
        /// <param name="options">Opzioni correnti</param>
        /// <returns>Stringa con dettaglio completo</returns>
        public string Build(FileProcessingRecord record, Options options)
        {
            string result = "";
            StringBuilder sb;
            if (record == null)
            {
                return result;
            }

            sb = new StringBuilder(512);

            this.AppendHeader(sb, record);
            this.AppendSourceFile(sb, record);
            this.AppendLanguageFile(sb, record);
            this.AppendTracks(sb, record, options);
            this.AppendSync(sb, record);
            this.AppendErrors(sb, record);
            this.AppendProcessingTimes(sb, record);
            this.AppendResult(sb, record);
            this.AppendEncoding(sb, record);

            result = sb.ToString();
            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Aggiunge intestazione record
        /// </summary>
        private void AppendHeader(StringBuilder sb, FileProcessingRecord record)
        {
            sb.Append("--- ").Append(record.EpisodeId).Append(" [").Append(Utils.GetStatusText(record.Status)).Append("] ---\n\n");
        }

        /// <summary>
        /// Aggiunge informazioni file sorgente
        /// </summary>
        private void AppendSourceFile(StringBuilder sb, FileProcessingRecord record)
        {
            sb.Append(AppText.T("web.detail.sourceFile")).Append('\n');
            sb.Append("  ").Append(record.SourceFileName).Append('\n');
            sb.Append(AppText.F("web.detail.sizeLine", Utils.FormatSize(record.SourceSize))).Append('\n');
            sb.Append('\n');
        }

        /// <summary>
        /// Aggiunge informazioni file lingua
        /// </summary>
        private void AppendLanguageFile(StringBuilder sb, FileProcessingRecord record)
        {
            sb.Append(AppText.T("web.detail.languageFile")).Append('\n');
            sb.Append("  ").Append(record.LangFileName.Length > 0 ? record.LangFileName : AppText.T("web.common.none")).Append('\n');
            if (record.LangSize > 0)
            {
                sb.Append(AppText.F("web.detail.sizeLine", Utils.FormatSize(record.LangSize))).Append('\n');
            }
        }

        /// <summary>
        /// Aggiunge tracce sorgente, importate e risultato finale
        /// </summary>
        private void AppendTracks(StringBuilder sb, FileProcessingRecord record, Options options)
        {
            bool filterAudio;
            bool filterSub;
            bool hasMerge;
            bool forceImportedAudioProcessing;
            sb.Append('\n').Append(AppText.T("web.detail.sourceTracks")).Append('\n');
            sb.Append(AppText.F("web.detail.audioLine", Utils.FormatTrackList(record.SourceAudioTracks))).Append('\n');
            sb.Append(AppText.F("web.detail.subLine", Utils.FormatTrackList(record.SourceSubTracks))).Append('\n');

            hasMerge = record.LangFilePath.Length > 0;
            forceImportedAudioProcessing = this.HasForcedImportedAudioProcessing(record, options);

            if (record.KeptSourceAudioIds.Count > 0 || record.KeptSourceSubIds.Count > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.keptSourceTracks")).Append('\n');
                sb.Append(AppText.F("web.detail.audioLine", Utils.FormatTrackListByIds(record.SourceAudioTracks, record.KeptSourceAudioIds))).Append('\n');
                sb.Append(AppText.F("web.detail.subLine", Utils.FormatTrackListByIds(record.SourceSubTracks, record.KeptSourceSubIds))).Append('\n');
            }

            if (record.ImportedAudioTracks.Count > 0 || record.ImportedSubTracks.Count > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.importTracks")).Append('\n');
                sb.Append(AppText.F("web.detail.audioLine", Utils.FormatImportedTrackList(record.ImportedAudioTracks, options, forceImportedAudioProcessing))).Append('\n');
                sb.Append(AppText.F("web.detail.subLine", Utils.FormatTrackList(record.ImportedSubTracks))).Append('\n');
            }

            filterAudio = record.KeptSourceAudioIds.Count > 0;
            filterSub = record.KeptSourceSubIds.Count > 0;
            if (record.ImportedAudioTracks.Count > 0 || record.ImportedSubTracks.Count > 0 || filterAudio || filterSub)
            {
                sb.Append('\n').Append(AppText.T("web.detail.finalResult")).Append('\n');
                sb.Append(AppText.F("web.detail.audioLine", Utils.FormatResultTrackList(record.SourceAudioTracks, record.KeptSourceAudioIds, record.ImportedAudioTracks, options, filterAudio, hasMerge, forceImportedAudioProcessing))).Append('\n');
                sb.Append(AppText.F("web.detail.subLine", Utils.FormatResultTrackList(record.SourceSubTracks, record.KeptSourceSubIds, record.ImportedSubTracks, "", filterSub))).Append('\n');
            }
        }

        /// <summary>
        /// True se deep analysis o source fill possono forzare render audio sulle tracce importate
        /// </summary>
        /// <param name="record">Record selezionato</param>
        /// <param name="options">Opzioni correnti</param>
        /// <returns>True se il processing importato non e' solo conversione generica</returns>
        private bool HasForcedImportedAudioProcessing(FileProcessingRecord record, Options options)
        {
            bool deepAudioRequired;

            if (options == null || options.AudioFormat.Length == 0)
            {
                return false;
            }

            deepAudioRequired = record.DeepAnalysisApplied &&
                record.DeepAnalysisMap != null &&
                record.DeepAnalysisMap.Operations.Count > 0 &&
                !options.SubOnly;

            return deepAudioRequired || options.AudioSourceFillThresholdMs > 0;
        }

        /// <summary>
        /// Aggiunge sezione sincronizzazione
        /// </summary>
        private void AppendSync(StringBuilder sb, FileProcessingRecord record)
        {
            sb.Append('\n').Append(AppText.T("web.detail.sync")).Append('\n');
            sb.Append(AppText.F("web.detail.audioDelayLine", Utils.FormatDelay(record.AudioDelayApplied))).Append('\n');
            sb.Append(AppText.F("web.detail.subDelayLine", Utils.FormatDelay(record.SubDelayApplied))).Append('\n');
            if (record.StretchFactor.Length > 0)
            {
                sb.Append(AppText.F("web.detail.stretchLine", record.StretchFactor)).Append('\n');
            }
            if (record.SpeedCorrectionApplied)
            {
                sb.Append(AppText.T("web.detail.speedCorrectionApplied")).Append('\n');
            }
            if (record.FrameSyncResult != null)
            {
                this.AppendFrameSyncSummary(sb, record.FrameSyncResult);
            }
            if (record.DeepAnalysisApplied && record.DeepAnalysisMap != null)
            {
                this.AppendDeepAnalysisSummary(sb, record.DeepAnalysisMap);
            }
        }

        /// <summary>
        /// Aggiunge riepilogo operativo DeepAnalysis
        /// </summary>
        private void AppendDeepAnalysisSummary(StringBuilder sb, EditMap editMap)
        {
            sb.Append(AppText.T("web.detail.deepApplied")).Append('\n');
            if (editMap.StretchFactor.Length > 0)
            {
                sb.Append(AppText.F("web.detail.deepStretchLine", editMap.StretchFactor)).Append('\n');
            }

            this.AppendEditOperations(sb, editMap.Operations);
        }

        /// <summary>
        /// Aggiunge operazioni EditMap
        /// </summary>
        private void AppendEditOperations(StringBuilder sb, List<EditOperation> operations)
        {
            sb.Append(AppText.F("web.detail.editOperations", operations.Count)).Append('\n');
            if (operations.Count == 0)
            {
                sb.Append(AppText.T("web.detail.noLocalEdits")).Append('\n');
                return;
            }

            for (int i = 0; i < operations.Count; i++)
            {
                EditOperation op = operations[i];
                sb.Append("    ").Append(i + 1).Append(". ");
                sb.Append(this.FormatEditOperationType(op.Type)).Append(" @ lang ");
                sb.Append(this.FormatTimestamp(op.LangTimestampMs)).Append(", ");
                sb.Append(AppText.T("web.detail.sourceShort")).Append(' ');
                sb.Append(this.FormatTimestamp(op.SourceTimestampMs)).Append(", ");
                sb.Append(AppText.F("web.detail.durationSeconds", (op.DurationMs / 1000.0).ToString("F3", CultureInfo.InvariantCulture))).Append('\n');
            }
        }

        /// <summary>
        /// Aggiunge riepilogo operativo FrameSync
        /// </summary>
        private void AppendFrameSyncSummary(StringBuilder sb, FrameSyncResult frameSyncResult)
        {
            int accepted = 0;
            int total = 0;
            if (frameSyncResult.Points != null)
            {
                total = frameSyncResult.Points.Count;
                for (int i = 0; i < frameSyncResult.Points.Count; i++)
                {
                    if (frameSyncResult.Points[i].Accepted)
                    {
                        accepted++;
                    }
                }
            }

            sb.Append(AppText.F("web.detail.frameSyncLine", frameSyncResult.Success ? "OK" : AppText.T("web.detail.failed"))).Append('\n');
            sb.Append(AppText.F("web.detail.frameSyncOffset", this.FormatOptionalDelay(frameSyncResult.OffsetMs))).Append('\n');
            sb.Append(AppText.F("web.detail.confidenceLine", frameSyncResult.Confidence.ToString("P0", CultureInfo.InvariantCulture))).Append('\n');
            if (total > 0)
            {
                sb.Append(AppText.F("web.detail.checkpointValid", accepted, total)).Append('\n');
            }
            if (frameSyncResult.FailureReason.Length > 0)
            {
                sb.Append(AppText.F("web.detail.reasonLine", frameSyncResult.FailureReason)).Append('\n');
            }
        }

        /// <summary>
        /// Aggiunge errori e motivi skip
        /// </summary>
        private void AppendErrors(StringBuilder sb, FileProcessingRecord record)
        {
            if (record.ErrorMessage.Length > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.error")).Append('\n');
                sb.Append("  ").Append(record.ErrorMessage).Append('\n');
            }
            if (record.SkipReason.Length > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.skipped")).Append('\n');
                sb.Append("  ").Append(record.SkipReason).Append('\n');
            }
        }

        /// <summary>
        /// Aggiunge tempi elaborazione
        /// </summary>
        private void AppendProcessingTimes(StringBuilder sb, FileProcessingRecord record)
        {
            if (record.SpeedCorrectionTimeMs > 0 || record.FrameSyncTimeMs > 0 || record.DeepAnalysisTimeMs > 0 || record.MergeTimeMs > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.processingTimes")).Append('\n');
                if (record.SpeedCorrectionTimeMs > 0) { sb.Append(AppText.F("web.detail.speedTime", record.SpeedCorrectionTimeMs)).Append('\n'); }
                if (record.FrameSyncTimeMs > 0) { sb.Append("  Frame-sync: ").Append(record.FrameSyncTimeMs).Append(" ms\n"); }
                if (record.DeepAnalysisTimeMs > 0) { sb.Append("  Deep analysis: ").Append(record.DeepAnalysisTimeMs).Append(" ms\n"); }
                if (record.MergeTimeMs > 0) { sb.Append("  Merge:      ").Append(record.MergeTimeMs).Append(" ms\n"); }
            }
        }

        /// <summary>
        /// Aggiunge risultato file
        /// </summary>
        private void AppendResult(StringBuilder sb, FileProcessingRecord record)
        {
            if (record.ResultSize > 0)
            {
                sb.Append('\n').Append(AppText.T("web.detail.result")).Append('\n');
                sb.Append(AppText.F("web.detail.sizeLine", Utils.FormatSize(record.ResultSize))).Append('\n');
                if (record.ResultFilePath.Length > 0)
                {
                    sb.Append(AppText.F("web.detail.fileLine", record.ResultFilePath)).Append('\n');
                }
            }
        }

        /// <summary>
        /// Aggiunge sezione encoding
        /// </summary>
        private void AppendEncoding(StringBuilder sb, FileProcessingRecord record)
        {
            if (record.EncodingProfileName.Length == 0)
            {
                return;
            }

            sb.Append('\n').Append(AppText.T("web.detail.encoding")).Append('\n');
            sb.Append(AppText.F("web.detail.profileLine", record.EncodingProfileName)).Append('\n');
            if (record.EncodedSize > 0 && record.ResultSize > 0)
            {
                long riduzione = 100 - (record.EncodedSize * 100 / record.ResultSize);
                sb.Append(AppText.F("web.detail.sizeChangeLine", Utils.FormatSize(record.ResultSize), Utils.FormatSize(record.EncodedSize)));
                sb.Append(AppText.F("web.detail.reductionSuffix", riduzione)).Append('\n');
            }
            if (record.EncodingTimeMs > 0)
            {
                sb.Append(AppText.F("web.detail.timeLine", record.EncodingTimeMs)).Append('\n');
            }
            if (record.EncodingCommand.Length > 0)
            {
                sb.Append(AppText.T("web.detail.commandAvailable")).Append('\n');
            }
        }

        /// <summary>
        /// Formatta un delay opzionale
        /// </summary>
        private string FormatOptionalDelay(int value)
        {
            if (value == int.MinValue)
            {
                return AppText.T("web.common.naLower");
            }

            return Utils.FormatDelay(value);
        }

        /// <summary>
        /// Formatta tipo operazione edit
        /// </summary>
        private string FormatEditOperationType(string operationType)
        {
            string result = operationType;

            if (operationType == EditOperation.CUT_SEGMENT)
            {
                result = AppText.T("web.detail.editCut");
            }
            else if (operationType == EditOperation.INSERT_SILENCE)
            {
                result = AppText.T("web.detail.editInsert");
            }

            return result;
        }

        /// <summary>
        /// Formatta timestamp in minutaggio
        /// </summary>
        private string FormatTimestamp(int timestampMs)
        {
            if (timestampMs < 0) { timestampMs = 0; }

            int totalSeconds = timestampMs / 1000;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            int millis = timestampMs % 1000;

            if (hours > 0)
            {
                return hours.ToString(CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture) + "." + millis.ToString("000", CultureInfo.InvariantCulture);
            }

            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture) + "." + millis.ToString("000", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
