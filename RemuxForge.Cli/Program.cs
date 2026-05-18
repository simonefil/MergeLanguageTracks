using RemuxForge.Core.Configuration;
using RemuxForge.Core.Infrastructure;
using RemuxForge.Core.Models;
using RemuxForge.Core.Pipeline;
using RemuxForge.Core.Splitting;
using System;
using System.Collections.Generic;

namespace RemuxForge.Cli
{
    /// <summary>
    /// Entry point applicativo della CLI RemuxForge
    /// </summary>
    internal class Program
    {
        #region Entry point

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">Argomenti riga di comando</param>
        /// <returns>Codice uscita: 0 successo, 1 errore</returns>
        static int Main(string[] args)
        {
            bool done = false;
            int exitCode = 0;
            Options opts = null;
            OptionsValidationResult validation;
            ConsoleHelper.SetRuntimeMode(LogRuntimeMode.Cli);

            // Abilita log su file se variabile d'ambiente impostata
            string logFilePath = Environment.GetEnvironmentVariable("REMUXFORGE_LOG_FILE");
            if (!string.IsNullOrEmpty(logFilePath))
            {
                ConsoleHelper.EnableFileLog(logFilePath);
            }

            // Inizializza AppSettings e cartella .remux-forge prima di tutto
            AppSettingsService.Instance.Initialize();
            ProcessingPipeline pipeline = null;
            List<FileProcessingRecord> records;
            ProcessingStats stats;
            // Nessun argomento: mostra help CLI
            if (args.Length == 0)
            {
                PrintHelp();
                done = true;
            }

            // Parsing argomenti
            if (!done)
            {
                opts = Options.Parse(args);
                if (opts.ErrorMessage.Length > 0)
                {
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Error, "Errore: " + opts.ErrorMessage);
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Debug, "Usa -h per vedere tutte le opzioni.");
                    exitCode = 1;
                    done = true;
                }
            }

            // Help
            if (!done && opts.Help)
            {
                PrintHelp();
                done = true;
            }

            if (!done)
            {
                validation = OptionsValidator.Validate(opts, true, false);
                if (!validation.IsValid)
                {
                    for (int i = 0; i < validation.Errors.Count; i++)
                    {
                        ConsoleHelper.Write(LogSection.Config, LogLevel.Error, "Errore: " + validation.Errors[i]);
                    }
                    for (int i = 0; i < validation.Warnings.Count; i++)
                    {
                        ConsoleHelper.Write(LogSection.Config, LogLevel.Info, validation.Warnings[i]);
                    }

                    exitCode = 1;
                    done = true;
                }
            }

            // Inizializza pipeline
            if (!done && opts.Mode == Options.MODE_SPLIT)
            {
                try
                {
                    MkvSplitPipeline splitPipeline = new MkvSplitPipeline();
                    exitCode = splitPipeline.Execute(opts);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.Write(LogSection.Split, LogLevel.Error, "Errore split: " + ex.Message);
                    exitCode = 1;
                }
                done = true;
            }

            // Inizializza pipeline
            if (!done)
            {
                pipeline = new ProcessingPipeline();

                // Collega log pipeline alla console
                pipeline.OnLogMessage += (section, level, text) =>
                {
                    // Componi testo con prefisso sezione se non General
                    string displayText = section != LogSection.General ? ConsoleHelper.FormatSectionPrefix(section) + text : text;
                    ConsoleColor color = ConsoleHelper.MapLevelToColor(level);
                    ConsoleColor original = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.WriteLine(displayText);
                    Console.ForegroundColor = original;
                };

                if (!pipeline.Initialize(opts))
                {
                    exitCode = 1;
                    done = true;
                }
            }

            // Scan ed elaborazione
            if (!done)
            {
                // Banner
                ConsoleHelper.Write(LogSection.General, LogLevel.Phase, "\n========================================");
                ConsoleHelper.Write(LogSection.General, LogLevel.Phase, "  RemuxForge");
                ConsoleHelper.Write(LogSection.General, LogLevel.Phase, "========================================\n");

                // Configurazione
                PrintConfiguration(opts, pipeline.CodecPatterns);

                // Scan file
                records = pipeline.ScanFiles();

                // Analizza e processa ogni file
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].Status == FileStatus.Pending)
                    {
                        pipeline.AnalyzeFile(records[i]);

                        if (records[i].Status == FileStatus.Analyzed)
                        {
                            pipeline.ProcessFile(records[i]);
                        }
                    }
                }

                // Report e riepilogo
                stats = ComputeStats(records);
                PrintDetailedReport(records, opts.DryRun);
                PrintSummary(stats);
            }

            return exitCode;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Calcola statistiche di elaborazione dai record
        /// </summary>
        /// <param name="records">Lista record elaborazione</param>
        /// <returns>Statistiche calcolate</returns>
        private static ProcessingStats ComputeStats(List<FileProcessingRecord> records)
        {
            ProcessingStats stats = new ProcessingStats();

            for (int i = 0; i < records.Count; i++)
            {
                FileProcessingRecord r = records[i];

                // File elaborati con successo
                if (r.Success)
                {
                    stats.Processed++;
                }
                // File saltati per mancanza ID episodio
                else if (string.Equals(r.SkipReason, "No episode ID"))
                {
                    stats.Skipped++;
                }
                // File senza corrispondenza lingua
                else if (string.Equals(r.SkipReason, "No match"))
                {
                    stats.NoMatch++;
                }
                // File senza tracce corrispondenti
                else if (string.Equals(r.SkipReason, "No matching tracks"))
                {
                    stats.NoTracks++;
                }
                // File con sync fallito (frame-sync o speed correction)
                else if (r.ErrorMessage.IndexOf("sync", StringComparison.OrdinalIgnoreCase) >= 0 || r.ErrorMessage.IndexOf("Speed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    stats.SyncFailed++;
                }
                // Errori generici (merge fallito, encoding fallito, etc.)
                else if (r.Status == FileStatus.Error)
                {
                    stats.Errors++;
                }
            }

            return stats;
        }

        /// <summary>
        /// Stampa il testo di aiuto completo
        /// </summary>
        private static void PrintHelp()
        {
            string helpText = @"
RemuxForge v" + Utils.GetVersion() + @"

USAGE: RemuxForge --mode remux|split [OPTIONS]

Modalita' remux: unisce tracce audio e sottotitoli da file MKV in lingue diverse.
Modalita' split: divide file MKV in segmenti/episodi.
Supporta sincronizzazione automatica tramite confronto visivo frame.

OPZIONI COMUNI:
  --mode remux|split             Obbligatorio. Modalita' operativa
  -r,   --recursive              Cerca ricorsivamente nelle sottocartelle
  -nr,  --no-recursive           Disabilita ricerca ricorsiva
  -ext, --extensions <list>      Estensioni file da cercare. Default: mkv
  -n,   --dry-run                Mostra cosa verrebbe fatto senza eseguire
  -h,   --help                   Mostra questo messaggio

REMUX - PARAMETRI OBBLIGATORI:
  -s,   --source <path>          Cartella con i file MKV sorgente
  -t,   --target-language <code> Codice/i lingua ISO 639-2 (es: ita oppure eng,ita)

REMUX - SORGENTE LINGUA:
  -l,   --language <path>        Cartella con i file MKV nella lingua da importare
                                 Se omesso, usa la cartella sorgente

REMUX - OUTPUT (scegliere destination oppure overwrite):
  -d,   --destination <path>     Cartella di output
  -o,   --overwrite              Sovrascrive i file sorgente

REMUX - SYNC:
  -fs,  --framesync              Abilita sync tramite confronto visivo frame
        --framesync-diagnostics  Scrive JSON diagnostici frame-sync in .remux-forge/framesync-diagnostics
  -da,  --deep-analysis          Analisi completa per file con edit diversi (lento)
        --deep-analysis-diagnostics
                                 Scrive JSON diagnostici deep-analysis in .remux-forge/deepanalysis-diagnostics
        --speed-correction <m>   Correzione velocita': off, auto, manual (default: off)
        --stretch-factor <f>     Stretch manuale per mkvmerge --sync (es: 25000/24000)
        --no-speed-correction    Alias per --speed-correction off
  -ad,  --audio-delay <ms>       Delay manuale audio in ms (sommato a sync auto)
  -sd,  --subtitle-delay <ms>    Delay manuale sottotitoli in ms
        --audio-source-fill-threshold-ms <ms>
                                 Soglia per riempire audio importato con segmenti source
        --audio-source-fill-language <code>
                                 Lingua audio source da usare per i segmenti di riempimento
        --audio-source-fill-modes <list>
                                 Modalita': start,end,insert-silence
                                 Richiede --audio-format e --audio-scope lang|all

  NOTA: In auto la correzione velocita' usa MediaInfo e non viene applicata
        su VFR. Per VFR usare --speed-correction manual --stretch-factor.
REMUX - FILTRO TRACCE:
  -ac,  --audio-codec <codec>    Importa solo audio con codec specifico (es: E-AC-3 oppure DTS,E-AC-3)
  -so,  --sub-only               Importa solo sottotitoli (ignora audio)
  -ao,  --audio-only             Importa solo audio (ignora sottotitoli)
  -ksa, --keep-source-audio      Lingue audio da mantenere nel sorgente (es: eng,jpn)
  -ksac,--keep-source-audio-codec Codec audio da mantenere nel sorgente (es: DTS,E-AC-3)
  -kss, --keep-source-subs       Lingue sub da mantenere nel sorgente

REMUX - MATCHING:
  -m,   --match-pattern <regex>  Pattern per matching episodi (default: S(\d+)E(\d+))

REMUX - AUDIO:
        --audio-format <fmt>     Formato finale audio: flac, lpcm, aac, opus.
                                 Se impostato senza --audio-scope, default: all
        --audio-scope <scope>    Tracce da processare: disabled, lang, all
                                 disabled evita conversioni generiche
        --audio-24-to-16         Converte 24bit -> 16bit con soxr/shibata (flac/lpcm)
        --audio-peak-normalize   Peak normalization globale multicanale
        --audio-peak-target-db   Target peak in dB (default: -1.0)
        --audio-rename-scope <scope>
                                 Rinomina audio finale: disabled, lang, all

  NOTA: Deep analysis con 0 operazioni usa solo delay mkvmerge. Se invece produce
        cut/insert su audio importato, serve --audio-format per ricodificare le
        tracce toccate. --audio-source-fill richiede anche scope lang o all.

REMUX - ENCODING:
  -ep,  --encoding-profile <name> Profilo encoding video post-merge (definito in appsettings.json)
                                  Codec supportati: libx264, libx265, libsvtav1
                                  L'encoding avviene in-place sul file risultato

SPLIT - PARAMETRI OBBLIGATORI:
  -s,   --source <path>          File MKV oppure cartella MKV
                                 Se e' una cartella, esegue batch con lo stesso pattern
  Scegliere una sola modalita' di taglio tra quelle sotto:
        --pattern ""5,5,5""        Raggruppa capitoli in segmenti
        --ranges ""T1-T2,...""     Range espliciti. T accetta HH:MM:SS.mmm, secondi, f<frame>, END
        --split-at ""T1,T2""       Split ai punti indicati
        --trim-start T            Scarta prima di T
        --trim-end T              Scarta dopo T
        --chapters-each           Un segmento per capitolo

SPLIT - INPUT E OUTPUT:
        --source-raw <file>       PTS da file alternativo, solo single file
        --output-dir <path>       Opzionale. Cartella output split
                                 Se omesso, scrive accanto al file input
        --output-template <tpl>   Opzionale. Template nomi output
                                 Keyword: {source_name}, {n}, {n+213:03d}, {start}, {end}, {chapter_name}
        --snap off|before|after|nearest
                                 Snap start a keyframe (default: off)
        --force                   Sovrascrive output split esistenti

OPZIONI TOOL:
  -mkv,   --mkvmerge-path <path> Percorso mkvmerge (default: cerca in PATH)

CODEC AUDIO (per -ac):
  Dolby:
    AC-3        Dolby Digital (DD, AC3)
    E-AC-3      Dolby Digital Plus (DD+, EAC3, include Atmos lossy)
    TrueHD      Dolby TrueHD (include Atmos lossless)
    MLP         Meridian Lossless Packing (base di TrueHD)

  DTS:
    DTS         DTS Core / Digital Surround
    DTS-HD MA   DTS-HD Master Audio (lossless)
    DTS-HD HR   DTS-HD High Resolution
    DTS-ES      DTS Extended Surround
    DTS:X       DTS:X (object-based, estensione di DTS-HD MA)

  Lossless:
    FLAC        Free Lossless Audio Codec
    PCM         Audio non compresso (LPCM, WAV)
    ALAC        Apple Lossless

  Lossy:
    AAC         Advanced Audio Coding (LC, HE-AAC, HE-AACv2)
    MP3         MPEG Audio Layer 3
    MP2         MPEG Audio Layer 2
    Opus        Opus (WebM, alta qualita' a basso bitrate)
    Vorbis      Ogg Vorbis

  IMPORTANTE: il matching e' ESATTO, non parziale!
        -ac ""DTS""      -> matcha SOLO DTS core, NON DTS-HD MA
        -ac ""DTS-HD""   -> matcha DTS-HD MA e DTS-HD HR
        -ac ""DTS-HDMA"" -> matcha SOLO DTS-HD Master Audio
        -ac ""ATMOS""    -> matcha TrueHD e E-AC-3 (entrambi possono avere Atmos)

  Alias comuni accettati:
        EAC3, DDP, DD+ -> E-AC-3
        AC3, DD        -> AC-3
        DTSX           -> DTS:X
        LPCM, WAV      -> PCM

ESEMPI REMUX:
  # Unisci tracce italiane con frame-sync
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -d ""D:\Out"" -fs

  # Deep analysis per file con scene diverse
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -d ""D:\Out"" -da

  # Dry run (mostra cosa farebbe senza eseguire)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -d ""D:\Out"" -fs -n

  # Solo audio E-AC-3 italiano
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ac ""E-AC-3"" -d ""D:\Out"" -fs

  # Importa audio DTS o E-AC-3 italiano
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ac ""DTS,E-AC-3"" -d ""D:\Out"" -fs

  # Solo sottotitoli (no audio)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -so -d ""D:\Out"" -fs

  # Sovrascrive i file sorgente (no cartella destinazione)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -o -fs

  # Mantieni solo eng/jpn audio e eng sub dal sorgente
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ksa eng,jpn -kss eng -d ""D:\Out""

  # Mantieni solo tracce DTS dal sorgente (qualsiasi lingua)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ksac DTS -d ""D:\Out""

  # Mantieni solo eng con codec DTS dal sorgente
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ksa eng -ksac DTS -d ""D:\Out""

  # Pattern custom (1x01 invece di S01E01)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -m ""(\d+)x(\d+)"" -d ""D:\Out""

  # Cerca anche file MP4 e AVI oltre a MKV
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ext mkv,mp4,avi -d ""D:\Out"" -fs

  # Singola sorgente: applica delay 960ms alle tracce ita, mantieni jpn+eng audio e eng+jpn sub
  RemuxForge --mode remux -s ""D:\Serie"" -t ita -ksa jpn,eng -kss eng,jpn -ad 960 -sd 960 -o

  # Processa audio lang in FLAC
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita --audio-format flac --audio-scope lang -d ""D:\Out""

  # Processa tutte le tracce audio in Opus (bitrate configurabile in .remux-forge/appsettings.json)
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita --audio-format opus --audio-scope all -ksa eng -d ""D:\Out"" -fs

  # Audio source fill: usa audio source ita per riempire gap start/end oltre 100ms
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita --audio-format flac --audio-scope lang --audio-source-fill-threshold-ms 100 --audio-source-fill-language ita --audio-source-fill-modes start,end -d ""D:\Out"" -da

  # Merge + encoding video con profilo definito in appsettings.json
  RemuxForge --mode remux -s ""D:\EN"" -l ""D:\IT"" -t ita -ep ""libx265_CRF28"" -d ""D:\Out""

ESEMPI SPLIT:
  # Divide un file in segmenti usando gruppi capitolo e naming automatico
  RemuxForge --mode split -s ""D:\Movie\source.mkv"" --pattern ""5,5,5"" --output-dir ""D:\Out""

  # Batch da cartella: stesso pattern su tutti gli MKV trovati
  RemuxForge --mode split -s ""D:\Season"" --pattern ""5,5,5,6"" --output-dir ""D:\Out""

  # Template con numero episodio offsettato
  RemuxForge --mode split -s ""D:\Season"" --pattern ""5,5,5,6"" --output-template ""Bleach.S12E{n+213:03d}.mkv""

  # Trim senza output-dir: crea {source_name}_trimmed.mkv accanto all'input
  RemuxForge --mode split -s ""D:\Movie\source.mkv"" --trim-start 00:01:00 --trim-end 00:24:00

CODICI LINGUA (ISO 639-2):
  Comuni: ita, eng, jpn, ger/deu, fra/fre, spa, por, rus, chi/zho, kor
  Altri:  ara, hin, pol, tur, nld/dut, swe, nor, dan, fin, hun, ces/cze
  Speciali: und (undefined), mul (multiple), zxx (no language)

REQUISITI:
  - MKVToolNix (mkvmerge) nel PATH
  - ffmpeg per frame-sync (download automatico su Windows/Linux; su macOS installare con Homebrew o configurare il path)

NOTE:
  Correzione velocita' (stretch): default off. In auto rileva differenze FPS
  solo su sorgenti CFR confermate da MediaInfo. Su VFR non applica stretch
  automatico; usare modalita' manuale con --stretch-factor. Corregge tramite
  mkvmerge --sync senza ricodifica e richiede ffmpeg per la verifica.

  Frame-sync: rileva i tagli scena nei frame grayscale 320x240 e li confronta
  tra sorgente e lingua per trovare il delay. Verifica a 9 punti distribuiti
  nel video con retry adattivo. Copre offset fino a +-60 secondi.

  Entrambe le funzionalita' richiedono ffmpeg. Il download automatico e' disponibile su Windows/Linux; su macOS usare Homebrew o path manuale.
";
            Console.WriteLine(helpText);
        }

        /// <summary>
        /// Stampa il riepilogo configurazione corrente
        /// </summary>
        /// <param name="opts">Opzioni validate</param>
        /// <param name="codecPatterns">Pattern codec risolti o null</param>
        private static void PrintConfiguration(Options opts, string[] codecPatterns)
        {
            ConsoleHelper.Write(LogSection.Config, LogLevel.Info, "Configurazione:");
            ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Cartella sorgente:   " + opts.SourceFolder);
            if (string.Equals(opts.SourceFolder, opts.LanguageFolder, StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Phase, "  Modalita':           Singola sorgente (lingua = sorgente)");
            }
            else
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Cartella lingua:     " + opts.LanguageFolder);
            }
            ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Lingua target:       " + string.Join(", ", opts.TargetLanguage));
            ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Pattern matching:    " + opts.MatchPattern);
            ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Estensioni file:     " + string.Join(", ", opts.FileExtensions));
            if (opts.Overwrite)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Modalita' output:    Overwrite (sovrascrive sorgente)");
            }
            else
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Modalita' output:    Destination");
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Cartella output:     " + opts.DestinationFolder);
            }

            // Mostra configurazione sync
            ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Speed correction:   " + opts.SpeedCorrectionMode);
            if (opts.ManualStretchFactor.Length > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Stretch manuale:    " + opts.ManualStretchFactor);
            }

            if (opts.DeepAnalysis)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Success, "  Deep analysis:       ATTIVO");
                if (opts.AudioDelay != 0 || opts.SubtitleDelay != 0)
                {
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Notice, "  Offset manuale:      Audio " + Utils.FormatDelay(opts.AudioDelay) + ", Sub " + Utils.FormatDelay(opts.SubtitleDelay) + " (sommato a deep analysis)");
                }
            }
            else if (opts.FrameSync)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Success, "  Frame-sync:          ATTIVO");
                if (opts.AudioDelay != 0 || opts.SubtitleDelay != 0)
                {
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Notice, "  Offset manuale:      Audio " + Utils.FormatDelay(opts.AudioDelay) + ", Sub " + Utils.FormatDelay(opts.SubtitleDelay) + " (sommato a frame-sync)");
                }
            }
            else
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Delay audio:         " + Utils.FormatDelay(opts.AudioDelay));
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Delay sottotitoli:   " + Utils.FormatDelay(opts.SubtitleDelay));
            }
            if (opts.AudioSourceFillThresholdMs > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Audio source fill:   >" + opts.AudioSourceFillThresholdMs + "ms da " + opts.AudioSourceFillLanguage + " (" + FormatAudioSourceFillModes(opts) + ")");
            }
            if (opts.AudioFormat.Length > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Formato audio:       " + opts.AudioFormat.ToUpperInvariant() + " (" + opts.AudioProcessingScope + ")");
                if (opts.AudioDownsample24To16)
                {
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  24bit -> 16bit:      SI");
                }
                if (opts.AudioPeakNormalize)
                {
                    ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Normalizzazione:     " + opts.AudioPeakTargetDb.ToString(System.Globalization.CultureInfo.InvariantCulture) + " dB");
                }
            }
            if (opts.AudioRenameScope != "disabled")
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Rinomina audio:      " + opts.AudioRenameScope);
            }

            // Mostra flag filtro
            if (opts.SubOnly)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Phase, "  Solo sottotitoli:    SI (audio ignorato)");
            }
            if (opts.AudioOnly)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Phase, "  Solo audio:          SI (sottotitoli ignorati)");
            }
            if (opts.AudioCodec.Count > 0 && codecPatterns != null)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Success, "  Codec selezionato: " + string.Join(", ", opts.AudioCodec) + " -> matcha: " + string.Join(", ", codecPatterns));
            }

            // Mostra filtri tracce sorgente
            if (opts.KeepSourceAudioLangs.Count > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Mantieni audio src:  " + string.Join(", ", opts.KeepSourceAudioLangs));
            }
            if (opts.KeepSourceAudioCodec.Count > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Codec audio src:     " + string.Join(", ", opts.KeepSourceAudioCodec));
            }
            if (opts.KeepSourceSubtitleLangs.Count > 0)
            {
                ConsoleHelper.Write(LogSection.Config, LogLevel.Text, "  Mantieni sub src:    " + string.Join(", ", opts.KeepSourceSubtitleLangs));
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Formatta le modalita' audio source fill attive
        /// </summary>
        /// <param name="opts">Opzioni correnti</param>
        /// <returns>Lista modalita' in formato leggibile</returns>
        private static string FormatAudioSourceFillModes(Options opts)
        {
            List<string> modes = new List<string>();
            if (opts.AudioSourceFillStart) { modes.Add(Options.AUDIO_SOURCE_FILL_START); }
            if (opts.AudioSourceFillEnd) { modes.Add(Options.AUDIO_SOURCE_FILL_END); }
            if (opts.AudioSourceFillInsertSilence) { modes.Add(Options.AUDIO_SOURCE_FILL_INSERT_SILENCE); }
            return string.Join(",", modes);
        }

        /// <summary>
        /// Stampa il report dettagliato con tabelle
        /// </summary>
        /// <param name="records">Lista record elaborazione</param>
        /// <param name="isDryRun">Se in modalita' dry run</param>
        private static void PrintDetailedReport(List<FileProcessingRecord> records, bool isDryRun)
        {
            // Filtra solo i record elaborati con successo (o dry run)
            List<FileProcessingRecord> validRecords = new List<FileProcessingRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Success || (isDryRun && records[i].LangFileName.Length > 0))
                {
                    validRecords.Add(records[i]);
                }
            }

            if (validRecords.Count > 0)
            {
                ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "\n========================================");
                ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "  Report Dettagliato");
                ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "========================================\n");

                // Tabella 1: Source Files
                ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "SOURCE FILES:");
                ConsoleHelper.Write(LogSection.Report, LogLevel.Text, "  " + Utils.PadRight("Episode", 12) + Utils.PadRight("Audio", 20) + Utils.PadRight("Subtitles", 20) + Utils.PadRight("Size", 12));
                ConsoleHelper.Write(LogSection.Report, LogLevel.Debug, "  " + new string('-', 64));

                for (int i = 0; i < validRecords.Count; i++)
                {
                    FileProcessingRecord r = validRecords[i];
                    string line = "  " + Utils.PadRight(r.EpisodeId, 12) + Utils.PadRight(Utils.FormatLangs(r.SourceAudioLangs), 20) + Utils.PadRight(Utils.FormatLangs(r.SourceSubLangs), 20) + Utils.PadRight(Utils.FormatSize(r.SourceSize), 12);
                    ConsoleHelper.Write(LogSection.Report, LogLevel.Text, line);
                }

                Console.WriteLine();

                // Tabella 2: Language Files
                ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "LANGUAGE FILES:");
                ConsoleHelper.Write(LogSection.Report, LogLevel.Text, "  " + Utils.PadRight("Episode", 12) + Utils.PadRight("Audio", 20) + Utils.PadRight("Subtitles", 20) + Utils.PadRight("Size", 12));
                ConsoleHelper.Write(LogSection.Report, LogLevel.Debug, "  " + new string('-', 64));

                for (int i = 0; i < validRecords.Count; i++)
                {
                    FileProcessingRecord r = validRecords[i];
                    string line = "  " + Utils.PadRight(r.EpisodeId, 12) + Utils.PadRight(Utils.FormatLangs(r.LangAudioLangs), 20) + Utils.PadRight(Utils.FormatLangs(r.LangSubLangs), 20) + Utils.PadRight(Utils.FormatSize(r.LangSize), 12);
                    ConsoleHelper.Write(LogSection.Report, LogLevel.Text, line);
                }

                Console.WriteLine();

                // Tabella 3: Result Files
                ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "RESULT FILES:");
                ConsoleHelper.Write(LogSection.Report, LogLevel.Text, "  " + Utils.PadRight("Episode", 12) + Utils.PadRight("Audio", 15) + Utils.PadRight("Subtitles", 15) + Utils.PadRight("Size", 10) + Utils.PadRight("Delay", 12) + Utils.PadRight("FrmSync", 10) + Utils.PadRight("FSConf", 8) + Utils.PadRight("Deep", 10) + Utils.PadRight("Speed", 10) + Utils.PadRight("Merge", 10));
                ConsoleHelper.Write(LogSection.Report, LogLevel.Debug, "  " + new string('-', 112));

                for (int i = 0; i < validRecords.Count; i++)
                {
                    FileProcessingRecord r = validRecords[i];
                    string sizeStr = isDryRun ? "N/A" : Utils.FormatSize(r.ResultSize);
                    string delayStr = Utils.FormatDelay(r.AudioDelayApplied);
                    string frameSyncStr = r.FrameSyncTimeMs > 0 ? r.FrameSyncTimeMs + "ms" : "-";
                    string frameSyncConfidenceStr = r.FrameSyncResult != null ? r.FrameSyncResult.Confidence.ToString("P0", System.Globalization.CultureInfo.InvariantCulture) : "-";
                    string deepStr = r.DeepAnalysisApplied && r.DeepAnalysisMap != null ? r.DeepAnalysisMap.Operations.Count + " ops" : "-";
                    string speedStr = r.SpeedCorrectionTimeMs > 0 ? r.SpeedCorrectionTimeMs + "ms" : "-";
                    string mergeStr = r.MergeTimeMs > 0 ? r.MergeTimeMs + "ms" : (isDryRun ? "N/A" : "-");

                    string line = "  " + Utils.PadRight(r.EpisodeId, 12) + Utils.PadRight(Utils.FormatLangs(r.ResultAudioLangs), 15) + Utils.PadRight(Utils.FormatLangs(r.ResultSubLangs), 15) + Utils.PadRight(sizeStr, 10) + Utils.PadRight(delayStr, 12) + Utils.PadRight(frameSyncStr, 10) + Utils.PadRight(frameSyncConfidenceStr, 8) + Utils.PadRight(deepStr, 10) + Utils.PadRight(speedStr, 10) + Utils.PadRight(mergeStr, 10);
                    ConsoleHelper.Write(LogSection.Report, LogLevel.Text, line);
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Stampa il riepilogo elaborazione finale
        /// </summary>
        /// <param name="stats">Statistiche elaborazione</param>
        private static void PrintSummary(ProcessingStats stats)
        {
            ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "\n========================================");
            ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "  Riepilogo");
            ConsoleHelper.Write(LogSection.Report, LogLevel.Phase, "========================================");
            ConsoleHelper.Write(LogSection.Report, LogLevel.Success, "  Elaborati:     " + stats.Processed);
            ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "  Saltati:       " + stats.Skipped);
            ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "  Senza match:   " + stats.NoMatch);
            ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "  Senza tracce:  " + stats.NoTracks);

            if (stats.SyncFailed > 0)
            {
                ConsoleHelper.Write(LogSection.Report, LogLevel.Info, "  Sync falliti:  " + stats.SyncFailed);
            }

            if (stats.Errors > 0)
            {
                ConsoleHelper.Write(LogSection.Report, LogLevel.Error, "  Errori:        " + stats.Errors);
            }
            else
            {
                ConsoleHelper.Write(LogSection.Report, LogLevel.Success, "  Errori:        " + stats.Errors);
            }

            Console.WriteLine();
        }

        #endregion
    }
}
