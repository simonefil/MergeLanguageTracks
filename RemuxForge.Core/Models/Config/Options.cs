using RemuxForge.Core.Configuration;
using System.Collections.Generic;

namespace RemuxForge.Core.Models
{
    /// <summary>
    /// Opzioni da riga di comando: parsing e validazione dei parametri CLI
    /// </summary>
    public class Options
    {
        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public Options()
        {
            this.Mode = "";
            this.Help = false;
            this.SourceFolder = "";
            this.LanguageFolder = "";
            this.TargetLanguage = new List<string>();
            this.MatchPattern = @"S(\d+)E(\d+)";
            this.Overwrite = false;
            this.DestinationFolder = "";
            this.AudioDelay = 0;
            this.SubtitleDelay = 0;
            this.AudioSourceFillThresholdMs = 0;
            this.AudioSourceFillLanguage = "";
            this.AudioSourceFillStart = false;
            this.AudioSourceFillEnd = false;
            this.AudioSourceFillInsertSilence = false;
            this.SpeedCorrectionMode = SPEED_CORRECTION_OFF;
            this.ManualStretchFactor = "";
            this.FrameSync = false;
            this.FrameSyncDiagnostics = false;
            this.DeepAnalysis = false;
            this.DeepAnalysisDiagnostics = false;
            this.AudioCodec = new List<string>();
            this.SubOnly = false;
            this.AudioOnly = false;
            this.KeepSourceAudioLangs = new List<string>();
            this.KeepSourceAudioCodec = new List<string>();
            this.KeepSourceSubtitleLangs = new List<string>();
            this.MkvMergePath = (AppSettingsService.Instance.Settings.Tools.MkvMergePath.Length > 0) ? AppSettingsService.Instance.Settings.Tools.MkvMergePath : "mkvmerge";
            this.Recursive = true;
            this.DryRun = false;
            this.FileExtensions = new List<string> { "mkv" };
            this.ConvertFormat = "";
            this.RenameAllTracks = false;
            this.ErrorMessage = "";
            this.EncodingProfileName = "";
            this.Split = new MkvSplitOptions();
        }

        #endregion

        #region Costanti

        /// <summary>
        /// Modalita' remux
        /// </summary>
        public const string MODE_REMUX = "remux";

        /// <summary>
        /// Modalita' split
        /// </summary>
        public const string MODE_SPLIT = "split";

        /// <summary>
        /// Speed correction disabilitata
        /// </summary>
        public const string SPEED_CORRECTION_OFF = "off";

        /// <summary>
        /// Speed correction automatica conservativa
        /// </summary>
        public const string SPEED_CORRECTION_AUTO = "auto";

        /// <summary>
        /// Speed correction con stretch factor manuale verificato
        /// </summary>
        public const string SPEED_CORRECTION_MANUAL = "manual";

        /// <summary>
        /// Modalita' audio source fill: inizio file
        /// </summary>
        public const string AUDIO_SOURCE_FILL_START = "start";

        /// <summary>
        /// Modalita' audio source fill: fine file
        /// </summary>
        public const string AUDIO_SOURCE_FILL_END = "end";

        /// <summary>
        /// Modalita' audio source fill: INSERT_SILENCE DeepAnalysis
        /// </summary>
        public const string AUDIO_SOURCE_FILL_INSERT_SILENCE = "insert-silence";

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Parsa gli argomenti da riga di comando in un'istanza
        /// </summary>
        /// <param name="args">Array di argomenti da riga di comando</param>
        /// <returns>Istanza Options popolata</returns>
        public static Options Parse(string[] args)
        {
            Options options = new Options();
            int i = 0;
            string key;
            bool handled;

            if (args == null)
            {
                options.ErrorMessage = "Argomenti non validi";
                return options;
            }

            if (HasHelpArgument(args))
            {
                options.Help = true;
                return options;
            }

            DetectMode(args, options);

            while (i < args.Length && options.ErrorMessage.Length == 0)
            {
                key = NormalizeKey(args[i]);
                handled = HandleCommonArgument(options, args, ref i, key);

                if (!handled)
                {
                    if (options.Mode == MODE_SPLIT)
                    {
                        handled = HandleSplitArgument(options, args, ref i, key);
                    }
                    else
                    {
                        handled = HandleRemuxArgument(options, args, ref i, key);
                    }
                }

                if (!handled && options.ErrorMessage.Length == 0)
                {
                    options.ErrorMessage = "Parametro sconosciuto: -" + key;
                }
            }

            return options;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Rileva --mode prima del parsing completo
        /// </summary>
        private static void DetectMode(string[] args, Options options)
        {
            string key;
            string mode = "";

            for (int i = 0; i < args.Length; i++)
            {
                key = NormalizeKey(args[i]);
                if (key == "mode")
                {
                    if (i + 1 >= args.Length)
                    {
                        options.ErrorMessage = "Valore mancante per --mode";
                        return;
                    }

                    mode = args[i + 1].Trim().ToLowerInvariant();
                    break;
                }
            }

            if (mode.Length == 0)
            {
                options.ErrorMessage = "Parametro obbligatorio mancante: --mode remux|split";
            }
            else if (mode != MODE_REMUX && mode != MODE_SPLIT)
            {
                options.ErrorMessage = "Modalita' non valida: " + mode + ". Valori validi: remux, split";
            }
            else
            {
                options.Mode = mode;
            }
        }

        /// <summary>
        /// Gestisce argomenti comuni a remux e split
        /// </summary>
        private static bool HandleCommonArgument(Options options, string[] args, ref int i, string key)
        {
            bool handled = true;

            if (key == "mode")
            {
                if (RequireValue(args, i, options))
                {
                    i += 2;
                }
            }
            else if (key == "h" || key == "help" || key == "?")
            {
                options.Help = true;
                i++;
            }
            else if (key == "n" || key == "dry-run")
            {
                options.DryRun = true;
                options.Split.DryRun = true;
                i++;
            }
            else if (key == "r" || key == "recursive")
            {
                options.Recursive = true;
                i++;
            }
            else if (key == "nr" || key == "no-recursive")
            {
                options.Recursive = false;
                i++;
            }
            else if (key == "ext" || key == "extensions")
            {
                if (RequireValue(args, i, options))
                {
                    options.FileExtensions.Clear();
                    ParseExtensions(args[i + 1], options.FileExtensions);
                    i += 2;
                }
            }
            else
            {
                handled = false;
            }

            return handled;
        }

        /// <summary>
        /// Gestisce argomenti remux
        /// </summary>
        private static bool HandleRemuxArgument(Options options, string[] args, ref int i, string key)
        {
            bool handled = true;
            string value;
            int delay;
            string cfLower;

            if (key == "fs" || key == "framesync")
            {
                options.FrameSync = true;
                i++;
            }
            else if (key == "framesync-diagnostics")
            {
                options.FrameSyncDiagnostics = true;
                i++;
            }
            else if (key == "deep-analysis-diagnostics")
            {
                options.DeepAnalysisDiagnostics = true;
                i++;
            }
            else if (key == "da" || key == "deep-analysis")
            {
                options.DeepAnalysis = true;
                i++;
            }
            else if (key == "so" || key == "sub-only")
            {
                options.SubOnly = true;
                i++;
            }
            else if (key == "ao" || key == "audio-only")
            {
                options.AudioOnly = true;
                i++;
            }
            else if (key == "rt" || key == "rename-tracks")
            {
                options.RenameAllTracks = true;
                i++;
            }
            else if (key == "o" || key == "overwrite")
            {
                options.Overwrite = true;
                i++;
            }
            else if (key == "no-speed-correction")
            {
                options.SpeedCorrectionMode = SPEED_CORRECTION_OFF;
                i++;
            }
            else if (!RequireValue(args, i, options))
            {
                handled = true;
            }
            else
            {
                value = args[i + 1];
                if (key == "s" || key == "source")
                {
                    options.SourceFolder = value;
                }
                else if (key == "l" || key == "language")
                {
                    options.LanguageFolder = value;
                }
                else if (key == "t" || key == "target-language")
                {
                    ParseCsvToList(value, options.TargetLanguage);
                }
                else if (key == "m" || key == "match-pattern")
                {
                    options.MatchPattern = value;
                }
                else if (key == "d" || key == "destination")
                {
                    options.DestinationFolder = value;
                }
                else if (key == "ad" || key == "audio-delay")
                {
                    if (!int.TryParse(value, out delay))
                    {
                        options.ErrorMessage = "Valore non valido per audio-delay: " + value;
                    }
                    options.AudioDelay = delay;
                }
                else if (key == "sd" || key == "subtitle-delay")
                {
                    if (!int.TryParse(value, out delay))
                    {
                        options.ErrorMessage = "Valore non valido per subtitle-delay: " + value;
                    }
                    options.SubtitleDelay = delay;
                }
                else if (key == "audio-source-fill-threshold-ms")
                {
                    if (!int.TryParse(value, out delay))
                    {
                        options.ErrorMessage = "Valore non valido per audio-source-fill-threshold-ms: " + value;
                    }
                    options.AudioSourceFillThresholdMs = delay;
                }
                else if (key == "audio-source-fill-language")
                {
                    options.AudioSourceFillLanguage = value.Trim();
                }
                else if (key == "audio-source-fill-modes")
                {
                    ParseAudioSourceFillModes(value, options);
                }
                else if (key == "speed-correction")
                {
                    options.SpeedCorrectionMode = NormalizeSpeedCorrectionMode(value);
                    if (options.SpeedCorrectionMode.Length == 0)
                    {
                        options.ErrorMessage = "Modalita' speed-correction non valida: " + value + ". Valori validi: off, auto, manual";
                    }
                }
                else if (key == "stretch-factor")
                {
                    options.ManualStretchFactor = value.Trim();
                    options.SpeedCorrectionMode = SPEED_CORRECTION_MANUAL;
                }
                else if (key == "ac" || key == "audio-codec")
                {
                    ParseCsvToList(value, options.AudioCodec);
                }
                else if (key == "ksa" || key == "keep-source-audio")
                {
                    ParseCsvToList(value, options.KeepSourceAudioLangs);
                }
                else if (key == "ksac" || key == "keep-source-audio-codec")
                {
                    ParseCsvToList(value, options.KeepSourceAudioCodec);
                }
                else if (key == "kss" || key == "keep-source-subs")
                {
                    ParseCsvToList(value, options.KeepSourceSubtitleLangs);
                }
                else if (key == "cf" || key == "convert-format")
                {
                    cfLower = value.Trim().ToLowerInvariant();
                    if (cfLower == "flac" || cfLower == "opus")
                    {
                        options.ConvertFormat = cfLower;
                    }
                    else
                    {
                        options.ErrorMessage = "Formato conversione non valido: " + value + ". Valori validi: flac, opus";
                    }
                }
                else if (key == "mkv" || key == "mkvmerge-path")
                {
                    options.MkvMergePath = value;
                }
                else if (key == "ep" || key == "encoding-profile")
                {
                    options.EncodingProfileName = value;
                }
                else
                {
                    handled = false;
                }

                if (handled)
                {
                    i += 2;
                }
            }

            return handled;
        }

        /// <summary>
        /// Gestisce argomenti split
        /// </summary>
        private static bool HandleSplitArgument(Options options, string[] args, ref int i, string key)
        {
            bool handled = true;
            string value;

            if (key == "chapters-each")
            {
                options.Split.ChaptersEach = true;
                i++;
            }
            else if (key == "force")
            {
                options.Split.Force = true;
                i++;
            }
            else if (!RequireValue(args, i, options))
            {
                handled = true;
            }
            else
            {
                value = args[i + 1];
                if (key == "s" || key == "source")
                {
                    options.SourceFolder = value;
                    options.Split.SourcePath = value;
                }
                else if (key == "source-raw")
                {
                    options.Split.SourceRaw = value;
                }
                else if (key == "d" || key == "destination" || key == "o" || key == "output-dir")
                {
                    options.DestinationFolder = value;
                    options.Split.OutputDir = value;
                }
                else if (key == "pattern")
                {
                    options.Split.Pattern = value;
                }
                else if (key == "ranges")
                {
                    options.Split.Ranges = value;
                }
                else if (key == "split-at")
                {
                    options.Split.SplitAt = value;
                }
                else if (key == "trim-start")
                {
                    options.Split.TrimStart = value;
                }
                else if (key == "trim-end")
                {
                    options.Split.TrimEnd = value;
                }
                else if (key == "output-template")
                {
                    options.Split.OutputTemplate = value;
                }
                else if (key == "snap")
                {
                    options.Split.Snap = ParseSnapMode(value, options);
                }
                else if (key == "log")
                {
                    options.Split.Log = value;
                }
                else
                {
                    handled = false;
                }

                if (handled)
                {
                    i += 2;
                }
            }

            return handled;
        }

        /// <summary>
        /// Verifica se un argomento valore e' presente
        /// </summary>
        private static bool RequireValue(string[] args, int i, Options options)
        {
            bool result = (i + 1 < args.Length) && (!args[i + 1].StartsWith("-") || (args[i + 1].Length > 1 && char.IsDigit(args[i + 1][1])));
            if (!result)
            {
                options.ErrorMessage = "Valore mancante per -" + NormalizeKey(args[i]);
            }

            return result;
        }

        /// <summary>
        /// Normalizza una chiave CLI
        /// </summary>
        private static string NormalizeKey(string value)
        {
            return value.ToLowerInvariant().TrimStart('-');
        }

        /// <summary>
        /// True se gli argomenti chiedono help
        /// </summary>
        private static bool HasHelpArgument(string[] args)
        {
            bool result = false;
            string key;
            for (int i = 0; i < args.Length; i++)
            {
                key = NormalizeKey(args[i]);
                if (key == "h" || key == "help" || key == "?")
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Parsa una stringa CSV e aggiunge i valori trimmati non vuoti alla lista
        /// </summary>
        private static void ParseCsvToList(string value, List<string> target)
        {
            string[] parts = value.Split(',');
            string trimmed;

            for (int j = 0; j < parts.Length; j++)
            {
                trimmed = parts[j].Trim();
                if (trimmed.Length > 0)
                {
                    target.Add(trimmed);
                }
            }
        }

        /// <summary>
        /// Parsa modalita' audio source fill
        /// </summary>
        /// <param name="value">Lista CSV modalita'</param>
        /// <param name="options">Opzioni da aggiornare</param>
        private static void ParseAudioSourceFillModes(string value, Options options)
        {
            string[] parts = value.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string mode = parts[i].Trim().ToLowerInvariant();
                if (mode.Length == 0)
                {
                    continue;
                }
                if (mode == AUDIO_SOURCE_FILL_START)
                {
                    options.AudioSourceFillStart = true;
                }
                else if (mode == AUDIO_SOURCE_FILL_END)
                {
                    options.AudioSourceFillEnd = true;
                }
                else if (mode == AUDIO_SOURCE_FILL_INSERT_SILENCE)
                {
                    options.AudioSourceFillInsertSilence = true;
                }
                else
                {
                    options.ErrorMessage = "Modalita' audio-source-fill non valida: " + mode + ". Valori validi: start,end,insert-silence";
                }
            }
        }

        /// <summary>
        /// Parsa estensioni CSV rimuovendo il punto iniziale
        /// </summary>
        private static void ParseExtensions(string value, List<string> target)
        {
            string[] parts = value.Split(',');
            string trimmed;

            for (int j = 0; j < parts.Length; j++)
            {
                trimmed = parts[j].Trim().TrimStart('.');
                if (trimmed.Length > 0)
                {
                    target.Add(trimmed);
                }
            }
        }

        /// <summary>
        /// Normalizza la modalita' speed correction
        /// </summary>
        private static string NormalizeSpeedCorrectionMode(string value)
        {
            string result = "";
            string trimmed = value != null ? value.Trim().ToLowerInvariant() : "";

            if (trimmed == SPEED_CORRECTION_OFF || trimmed == "none" || trimmed == "disabled")
            {
                result = SPEED_CORRECTION_OFF;
            }
            else if (trimmed == SPEED_CORRECTION_AUTO || trimmed == "autosafe")
            {
                result = SPEED_CORRECTION_AUTO;
            }
            else if (trimmed == SPEED_CORRECTION_MANUAL)
            {
                result = SPEED_CORRECTION_MANUAL;
            }

            return result;
        }

        /// <summary>
        /// Parsa modalita' snap split
        /// </summary>
        private static MkvSplitSnapMode ParseSnapMode(string value, Options options)
        {
            MkvSplitSnapMode result = MkvSplitSnapMode.Off;
            string trimmed = value != null ? value.Trim().ToLowerInvariant() : "";

            if (trimmed == "off")
            {
                result = MkvSplitSnapMode.Off;
            }
            else if (trimmed == "before")
            {
                result = MkvSplitSnapMode.Before;
            }
            else if (trimmed == "after")
            {
                result = MkvSplitSnapMode.After;
            }
            else if (trimmed == "nearest")
            {
                result = MkvSplitSnapMode.Nearest;
            }
            else
            {
                options.ErrorMessage = "--snap deve essere uno tra: off, before, after, nearest";
            }

            return result;
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Modalita' operativa obbligatoria: remux o split
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Indica se e' stato richiesto il messaggio di aiuto (-h, --help)
        /// </summary>
        public bool Help { get; set; }

        /// <summary>
        /// Percorso della cartella sorgente contenente i file MKV (-s, --source)
        /// </summary>
        public string SourceFolder { get; set; }

        /// <summary>
        /// Percorso della cartella lingua contenente i file MKV nella lingua alternativa (-l, --language)
        /// </summary>
        public string LanguageFolder { get; set; }

        /// <summary>
        /// Lista di codici lingua ISO 639-2 da estrarre (-t, --target-language). Supporta valori multipli separati da virgola
        /// </summary>
        public List<string> TargetLanguage { get; set; }

        /// <summary>
        /// Pattern regex per il matching degli episodi (-m, --match-pattern). Default: S(\d+)E(\d+)
        /// </summary>
        public string MatchPattern { get; set; }

        /// <summary>
        /// Modalita' overwrite: sovrascrive i file sorgente (-o, --overwrite). Default: false
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Cartella di destinazione per l'output (-d, --destination)
        /// </summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Ritardo audio manuale in millisecondi (-ad, --audio-delay). Sommato all'offset frame-sync se abilitato
        /// </summary>
        public int AudioDelay { get; set; }

        /// <summary>
        /// Ritardo sottotitoli manuale in millisecondi (-sd, --subtitle-delay). Sommato all'offset frame-sync se abilitato
        /// </summary>
        public int SubtitleDelay { get; set; }

        /// <summary>
        /// Soglia oltre cui usare audio source per riempire inizio, fine o INSERT_SILENCE
        /// </summary>
        public int AudioSourceFillThresholdMs { get; set; }

        /// <summary>
        /// Lingua audio sorgente da usare per audio source fill
        /// </summary>
        public string AudioSourceFillLanguage { get; set; }

        /// <summary>
        /// Usa audio source all'inizio se il delay positivo supera la soglia
        /// </summary>
        public bool AudioSourceFillStart { get; set; }

        /// <summary>
        /// Usa audio source in coda se la traccia lang termina prima del source oltre soglia
        /// </summary>
        public bool AudioSourceFillEnd { get; set; }

        /// <summary>
        /// Usa audio source per INSERT_SILENCE DeepAnalysis oltre soglia
        /// </summary>
        public bool AudioSourceFillInsertSilence { get; set; }

        /// <summary>
        /// Modalita' speed correction: off, auto, manual
        /// </summary>
        public string SpeedCorrectionMode { get; set; }

        /// <summary>
        /// Stretch factor manuale per mkvmerge --sync
        /// </summary>
        public string ManualStretchFactor { get; set; }

        /// <summary>
        /// Indica se il raffinamento frame sync e' abilitato (-fs, --framesync)
        /// </summary>
        public bool FrameSync { get; set; }

        /// <summary>
        /// Scrive diagnostica JSON per il frame-sync (--framesync-diagnostics)
        /// </summary>
        public bool FrameSyncDiagnostics { get; set; }

        /// <summary>
        /// Indica se la deep analysis e' abilitata (-da, --deep-analysis). Mutuamente esclusiva con FrameSync
        /// </summary>
        public bool DeepAnalysis { get; set; }

        /// <summary>
        /// Scrive diagnostica JSON per la deep analysis (--deep-analysis-diagnostics)
        /// </summary>
        public bool DeepAnalysisDiagnostics { get; set; }

        /// <summary>
        /// Lista di codec audio da importare (-ac, --audio-codec). Solo le tracce con questi codec verranno importate
        /// </summary>
        public List<string> AudioCodec { get; set; }

        /// <summary>
        /// Importa solo sottotitoli, ignora tracce audio (-so, --sub-only)
        /// </summary>
        public bool SubOnly { get; set; }

        /// <summary>
        /// Importa solo tracce audio, ignora sottotitoli (-ao, --audio-only)
        /// </summary>
        public bool AudioOnly { get; set; }

        /// <summary>
        /// Lista di codici lingua da mantenere nelle tracce audio sorgente (-ksa, --keep-source-audio)
        /// </summary>
        public List<string> KeepSourceAudioLangs { get; set; }

        /// <summary>
        /// Lista di codec audio da mantenere nelle tracce sorgente (-ksac, --keep-source-audio-codec). Solo le tracce con questi codec verranno mantenute
        /// </summary>
        public List<string> KeepSourceAudioCodec { get; set; }

        /// <summary>
        /// Lista di codici lingua da mantenere nelle tracce sottotitoli sorgente (-kss, --keep-source-subs)
        /// </summary>
        public List<string> KeepSourceSubtitleLangs { get; set; }

        /// <summary>
        /// Percorso dell'eseguibile mkvmerge (-mkv, --mkvmerge-path). Default: cerca nel PATH
        /// </summary>
        public string MkvMergePath { get; set; }

        /// <summary>
        /// Formato conversione tracce lossless (-cf, --convert-format). Valori: "flac", "opus", "" = disabilitato
        /// </summary>
        public string ConvertFormat { get; set; }

        /// <summary>
        /// Forza rinomina di tutte le tracce audio (non solo quelle convertite) (-rt, --rename-tracks)
        /// </summary>
        public bool RenameAllTracks { get; set; }

        /// <summary>
        /// Indica se cercare ricorsivamente nelle sottocartelle (-r, --recursive). Default: true
        /// </summary>
        public bool Recursive { get; set; }

        /// <summary>
        /// Modalita' dry run: mostra cosa verrebbe fatto senza eseguire (-n, --dry-run)
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Lista di estensioni file da cercare (-ext, --extensions). Default: mkv
        /// </summary>
        public List<string> FileExtensions { get; set; }

        /// <summary>
        /// Messaggio di errore dal parsing. Vuoto se nessun errore
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Nome del profilo di encoding video post-merge, vuoto = disabilitato
        /// </summary>
        public string EncodingProfileName { get; set; }

        /// <summary>
        /// Opzioni specifiche della modalita' split
        /// </summary>
        public MkvSplitOptions Split { get; set; }

        #endregion
    }
}
