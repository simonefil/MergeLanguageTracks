using RemuxForge.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace RemuxForge.Core.Configuration
{
    /// <summary>
    /// Valida le regole funzionali condivise delle opzioni CLI/WebUI
    /// </summary>
    public static class OptionsValidator
    {
        #region Metodi pubblici

        /// <summary>
        /// Valida le opzioni
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="requireSourceFolder">True se la sorgente e' obbligatoria</param>
        /// <param name="validateFolderExists">True se validare l'esistenza delle cartelle</param>
        /// <returns>Risultato validazione</returns>
        public static OptionsValidationResult Validate(Options options, bool requireSourceFolder, bool validateFolderExists)
        {
            OptionsValidationResult result = new OptionsValidationResult();
            bool needsMerge;
            bool needsFilter;
            bool needsRemux;
            bool needsEncode;
            if (options == null)
            {
                result.AddError("Configurazione non valida");
                return result;
            }

            if (options.Mode != Options.MODE_REMUX && options.Mode != Options.MODE_SPLIT)
            {
                result.AddError("Parametro obbligatorio mancante o non valido: --mode remux|split");
                return result;
            }

            if (options.Mode == Options.MODE_SPLIT)
            {
                ValidateSplitOptions(options, requireSourceFolder, validateFolderExists, result);
                return result;
            }

            needsMerge = options.TargetLanguage.Count > 0;
            needsFilter = options.KeepSourceAudioLangs.Count > 0 || options.KeepSourceAudioCodec.Count > 0 || options.KeepSourceSubtitleLangs.Count > 0;
            needsRemux = needsMerge || needsFilter || options.AudioFormat.Length > 0 || options.AudioRenameScope != "disabled";
            needsEncode = options.EncodingProfileName.Length > 0;

            if (options.FrameSync && options.DeepAnalysis)
            {
                result.AddError("Frame-sync e deep analysis sono mutuamente esclusivi");
            }

            if (options.SubOnly && options.AudioOnly)
            {
                result.AddError("Solo sottotitoli e solo audio non possono essere attivi insieme");
            }

            if (options.Overwrite && options.DestinationFolder.Length > 0)
            {
                result.AddError("Overwrite e destination non possono essere usati insieme");
            }

            ValidateSpeedCorrection(options, result);
            ValidateAudioProcessing(options, needsMerge, result);
            ValidateAudioSourceFill(options, needsMerge, result);
            ValidateAnalysisCrop(options, result);
            ValidateRegex(options.MatchPattern, result);
            ValidateExtensions(options, result);
            ValidateLanguages(options, needsMerge, result);
            ValidateCodecs(options, result);
            ValidateFolders(options, requireSourceFolder, validateFolderExists, needsMerge, result);

            if (requireSourceFolder && !needsRemux && !needsEncode)
            {
                result.AddError("Nessuna operazione configurata. Specificare almeno una tra: lingua target, filtri tracce, processing audio, profilo encoding");
            }

            if (requireSourceFolder && !options.Overwrite && options.DestinationFolder.Length == 0 && !(needsEncode && !needsRemux))
            {
                result.AddError("Specificare destination oppure overwrite");
            }

            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Valida modalita' e parametro manuale della speed correction
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateSpeedCorrection(Options options, OptionsValidationResult result)
        {
            if (options.SpeedCorrectionMode != Options.SPEED_CORRECTION_OFF &&
                options.SpeedCorrectionMode != Options.SPEED_CORRECTION_AUTO &&
                options.SpeedCorrectionMode != Options.SPEED_CORRECTION_MANUAL)
            {
                result.AddError("Modalita' speed-correction non valida: " + options.SpeedCorrectionMode);
                return;
            }

            if (options.SpeedCorrectionMode == Options.SPEED_CORRECTION_MANUAL)
            {
                // In manuale lo stretch deve essere esplicito: non si tenta inferenza automatica su VFR
                if (options.ManualStretchFactor.Trim().Length == 0)
                {
                    result.AddError("La modalita' speed-correction manual richiede uno stretch factor");
                }
                else if (!IsValidStretchFactor(options.ManualStretchFactor))
                {
                    result.AddError("Stretch factor manuale non valido: " + options.ManualStretchFactor);
                }
            }
        }

        /// <summary>
        /// Valida opzioni audio source fill
        /// </summary>
        private static void ValidateAudioSourceFill(Options options, bool needsMerge, OptionsValidationResult result)
        {
            bool anyMode = options.AudioSourceFillStart || options.AudioSourceFillEnd || options.AudioSourceFillInsertSilence;
            bool active = anyMode || options.AudioSourceFillThresholdMs > 0 || options.AudioSourceFillLanguage.Length > 0;

            if (options.AudioSourceFillThresholdMs < 0)
            {
                result.AddError("audio-source-fill-threshold-ms non puo' essere negativo");
            }

            if (active && !needsMerge)
            {
                result.AddError("audio-source-fill richiede una lingua target da importare");
            }

            if (active && (options.AudioFormat.Length == 0 || options.AudioProcessingScope == "disabled"))
            {
                result.AddError("audio-source-fill richiede formato audio e scope audio attivo");
            }

            if (active && options.AudioSourceFillThresholdMs <= 0)
            {
                result.AddError("audio-source-fill-threshold-ms deve essere maggiore di zero quando audio-source-fill e' attivo");
            }

            if (active && options.AudioSourceFillLanguage.Length == 0)
            {
                result.AddError("audio-source-fill-language e' obbligatorio quando audio-source-fill e' attivo");
            }

            if (active && !anyMode)
            {
                result.AddError("audio-source-fill-modes richiede almeno una modalita': start, end, insert-silence");
            }

            if (options.AudioSourceFillInsertSilence && !options.DeepAnalysis)
            {
                result.AddError("audio-source-fill mode insert-silence richiede --deep-analysis");
            }

            if (options.AudioSourceFillLanguage.Length > 0)
            {
                ValidateLanguage("audio-source-fill-language", options.AudioSourceFillLanguage, result);
            }
        }

        /// <summary>
        /// Valida opzioni del processing audio
        /// </summary>
        private static void ValidateAudioProcessing(Options options, bool needsMerge, OptionsValidationResult result)
        {
            if (!IsValidAudioFormat(options.AudioFormat))
            {
                result.AddError("Formato audio non valido: " + options.AudioFormat + ". Valori validi: flac, lpcm, aac, opus");
            }

            if (!IsValidScope(options.AudioProcessingScope))
            {
                result.AddError("Scope audio non valido: " + options.AudioProcessingScope + ". Valori validi: disabled, lang, all");
            }

            if (!IsValidScope(options.AudioRenameScope))
            {
                result.AddError("Scope rinomina audio non valido: " + options.AudioRenameScope + ". Valori validi: disabled, lang, all");
            }

            if (options.AudioFormat.Length == 0 && options.AudioProcessingScope != "disabled")
            {
                result.AddError("audio-scope richiede audio-format");
            }

            if (options.AudioProcessingScope != "disabled" && options.AudioFormat.Length == 0)
            {
                result.AddError("audio-format e' obbligatorio quando audio-scope non e' disabled");
            }

            if ((options.AudioPeakNormalize || options.AudioDownsample24To16) && (options.AudioFormat.Length == 0 || options.AudioProcessingScope == "disabled"))
            {
                result.AddError("normalizzazione e 24->16 richiedono formato audio e scope audio attivo");
            }

            if (options.AudioDownsample24To16 && options.AudioFormat != "flac" && options.AudioFormat != "lpcm")
            {
                result.AddError("24->16 e' ammesso solo per flac e lpcm");
            }

            if (options.AudioPeakTargetDb > 0.0)
            {
                result.AddError("audio-peak-target-db deve essere minore o uguale a 0");
            }

            if (options.AudioPeakTargetDb < -60.0)
            {
                result.AddError("audio-peak-target-db non puo' essere minore di -60");
            }
        }

        /// <summary>
        /// Verifica se un formato audio e' valido
        /// </summary>
        private static bool IsValidAudioFormat(string value)
        {
            return value == "" || value == "flac" || value == "lpcm" || value == "aac" || value == "opus";
        }

        /// <summary>
        /// Verifica se uno scope audio e' valido
        /// </summary>
        private static bool IsValidScope(string value)
        {
            return value == "disabled" || value == "lang" || value == "all";
        }

        /// <summary>
        /// Valida i crop manuali usati solo dal matching visuale
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateAnalysisCrop(Options options, OptionsValidationResult result)
        {
            int left;
            int right;
            int top;
            int bottom;

            if (!Options.TryParseAnalysisCropPx(options.AnalysisCropSourcePx, out left, out right, out top, out bottom))
            {
                result.AddError("analysis-crop-source-px non valido: usare L:R:T:B con interi >= 0");
            }

            if (!Options.TryParseAnalysisCropPx(options.AnalysisCropLanguagePx, out left, out right, out top, out bottom))
            {
                result.AddError("analysis-crop-lang-px non valido: usare L:R:T:B con interi >= 0");
            }
        }

        /// <summary>
        /// Valida la regex di matching episodio
        /// </summary>
        /// <param name="pattern">Pattern regex</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateRegex(string pattern, OptionsValidationResult result)
        {
            try
            {
                _ = new Regex(pattern);
            }
            catch (Exception ex)
            {
                result.AddError("Pattern match non valido: " + ex.Message);
            }
        }

        /// <summary>
        /// Valida che sia configurata almeno una estensione video
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateExtensions(Options options, OptionsValidationResult result)
        {
            if (options.FileExtensions.Count == 0)
            {
                result.AddError("Indicare almeno una estensione file");
            }
        }

        /// <summary>
        /// Valida lingue target e filtri lingua
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="needsMerge">True se e' richiesto merge da language</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateLanguages(Options options, bool needsMerge, OptionsValidationResult result)
        {
            if (needsMerge)
            {
                for (int i = 0; i < options.TargetLanguage.Count; i++)
                {
                    // Le lingue target sono obbligatorie solo quando il merge e' effettivamente richiesto
                    ValidateLanguage("Lingua target", options.TargetLanguage[i], result);
                }
            }

            for (int i = 0; i < options.KeepSourceAudioLangs.Count; i++)
            {
                ValidateLanguage("keep-source-audio", options.KeepSourceAudioLangs[i], result);
            }

            for (int i = 0; i < options.KeepSourceSubtitleLangs.Count; i++)
            {
                ValidateLanguage("keep-source-subs", options.KeepSourceSubtitleLangs[i], result);
            }
        }

        /// <summary>
        /// Valida una singola lingua ISO 639
        /// </summary>
        /// <param name="label">Etichetta da usare negli errori</param>
        /// <param name="language">Codice lingua</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateLanguage(string label, string language, OptionsValidationResult result)
        {
            List<string> suggestions;
            if (language == null || !Regex.IsMatch(language.ToLowerInvariant(), @"^[a-z]{2,3}$"))
            {
                result.AddError(label + " non valida: " + language);
                return;
            }

            if (!LanguageValidator.IsValid(language))
            {
                // Le suggestion restano warning per non nascondere l'errore principale
                result.AddError(label + " non riconosciuta: " + language);
                suggestions = LanguageValidator.GetSimilar(language, 3);
                if (suggestions.Count > 0)
                {
                    result.AddWarning("Forse intendevi: " + string.Join(", ", suggestions) + "?");
                }
            }
        }

        /// <summary>
        /// Valida codec audio richiesti dall'utente
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateCodecs(Options options, OptionsValidationResult result)
        {
            for (int i = 0; i < options.AudioCodec.Count; i++)
            {
                if (CodecMapping.GetCodecPatterns(options.AudioCodec[i]) == null)
                {
                    result.AddError("Codec audio non riconosciuto: " + options.AudioCodec[i] + ". Validi: " + CodecMapping.GetAllCodecNames());
                }
            }

            for (int i = 0; i < options.KeepSourceAudioCodec.Count; i++)
            {
                if (CodecMapping.GetCodecPatterns(options.KeepSourceAudioCodec[i]) == null)
                {
                    result.AddError("Codec keep-source-audio-codec non riconosciuto: " + options.KeepSourceAudioCodec[i]);
                }
            }
        }

        /// <summary>
        /// Valida presenza ed esistenza delle cartelle operative
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="requireSourceFolder">True se source e' obbligatoria</param>
        /// <param name="validateFolderExists">True se controllare esistenza su disco</param>
        /// <param name="needsMerge">True se la cartella language serve al merge</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateFolders(Options options, bool requireSourceFolder, bool validateFolderExists, bool needsMerge, OptionsValidationResult result)
        {
            if (requireSourceFolder && options.SourceFolder.Length == 0)
            {
                result.AddError("Parametro obbligatorio mancante: source");
            }

            if (validateFolderExists && options.SourceFolder.Length > 0 && !Directory.Exists(options.SourceFolder))
            {
                result.AddError("Cartella sorgente non trovata: " + options.SourceFolder);
            }

            if (validateFolderExists && needsMerge && options.LanguageFolder.Length > 0 && !Directory.Exists(options.LanguageFolder))
            {
                result.AddError("Cartella lingua non trovata: " + options.LanguageFolder);
            }
        }

        /// <summary>
        /// Valida opzioni della modalita' split
        /// </summary>
        /// <param name="options">Opzioni da validare</param>
        /// <param name="requireSourceFolder">True se source e' obbligatorio</param>
        /// <param name="validateFolderExists">True se controllare esistenza su disco</param>
        /// <param name="result">Risultato validazione da aggiornare</param>
        private static void ValidateSplitOptions(Options options, bool requireSourceFolder, bool validateFolderExists, OptionsValidationResult result)
        {
            int modes = 0;
            bool sourceIsFolder = false;
            bool sourceIsFile = false;

            ValidateExtensions(options, result);

            if (options.Split == null)
            {
                result.AddError("Configurazione split non valida");
                return;
            }

            if (requireSourceFolder && options.Split.SourcePath.Length == 0)
            {
                result.AddError("Parametro obbligatorio mancante: source");
            }

            if (options.Split.SourcePath.Length > 0)
            {
                sourceIsFile = File.Exists(options.Split.SourcePath);
                sourceIsFolder = Directory.Exists(options.Split.SourcePath);

                if (validateFolderExists && !sourceIsFile && !sourceIsFolder)
                {
                    result.AddError("Sorgente split non trovata: " + options.Split.SourcePath);
                }
            }

            if (options.Split.Pattern.Length > 0) { modes++; }
            if (options.Split.Ranges.Length > 0) { modes++; }
            if (options.Split.SplitAt.Length > 0) { modes++; }
            if (options.Split.TrimStart.Length > 0 || options.Split.TrimEnd.Length > 0) { modes++; }
            if (options.Split.ChaptersEach) { modes++; }

            if (modes == 0)
            {
                result.AddError("Nessuna modalita' split configurata. Specificare pattern, ranges, split-at, trim-start/trim-end oppure chapters-each");
            }
            else if (modes > 1)
            {
                result.AddError("Le modalita' split sono mutuamente esclusive");
            }

            if (sourceIsFolder && options.Split.SourceRaw.Length > 0)
            {
                result.AddError("source-raw e' disponibile solo per split single file");
            }
        }

        /// <summary>
        /// Valida uno stretch factor manuale in forma decimale o frazione
        /// </summary>
        /// <param name="value">Valore testuale</param>
        /// <returns>True se il valore e' positivo e parsabile</returns>
        private static bool IsValidStretchFactor(string value)
        {
            bool result = false;
            string trimmed = value != null ? value.Trim() : "";
            string[] parts;
            double numerator;
            double denominator;
            double parsed;
            if (trimmed.Contains("/"))
            {
                // Supporta forma frazionaria tipo 24000/25025 per casi PAL/NTSC precisi
                parts = trimmed.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out numerator) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out denominator) &&
                    numerator > 0.0 &&
                    denominator > 0.0)
                {
                    result = true;
                }
            }
            else if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed > 0.0)
            {
                result = true;
            }

            return result;
        }

        #endregion
    }

    /// <summary>
    /// Risultato della validazione opzioni
    /// </summary>
    public class OptionsValidationResult
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public OptionsValidationResult()
        {
            this.Errors = new List<string>();
            this.Warnings = new List<string>();
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Aggiunge un errore
        /// </summary>
        public void AddError(string text)
        {
            this.Errors.Add(text);
        }

        /// <summary>
        /// Aggiunge un warning
        /// </summary>
        public void AddWarning(string text)
        {
            this.Warnings.Add(text);
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// True se non ci sono errori
        /// </summary>
        public bool IsValid
        {
            get { return this.Errors.Count == 0; }
        }

        /// <summary>
        /// Errori di validazione
        /// </summary>
        public List<string> Errors { get; private set; }

        /// <summary>
        /// Warning di validazione
        /// </summary>
        public List<string> Warnings { get; private set; }

        /// <summary>
        /// Messaggio errori aggregato
        /// </summary>
        public string ErrorMessage
        {
            get { return string.Join("\n", this.Errors); }
        }

        #endregion
    }
}
