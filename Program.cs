using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MergeLanguageTracks
{
    internal class Program
    {
        #region Metodi privati

        /// <summary>
        /// Stampa il testo di aiuto completo in italiano, corrispondente alla versione PowerShell.
        /// </summary>
        private static void PrintHelp()
        {
            string helpText = @"
USAGE: MergeLanguageTracks [OPTIONS]

Unisce tracce audio e sottotitoli da file MKV in lingue diverse.
Supporta sincronizzazione automatica tramite audio fingerprinting.

OPZIONI OBBLIGATORIE:
  -s,  -SourceFolder <path>      Cartella con i file MKV sorgente
  -l,  -LanguageFolder <path>    Cartella con i file MKV nella lingua da importare
  -t,  -TargetLanguage <code>    Codice/i lingua ISO 639-2 (es: ita, eng  oppure: eng,ita)

OPZIONI OUTPUT:
  -d,  -DestinationFolder <path> Cartella di output (default: richiesta)
  -o,  -OutputMode <mode>        ""Destination"" (default) o ""Overwrite""

OPZIONI SYNC:
  -as, -AutoSync                 Abilita sync automatico (audio fingerprinting)
  -ad, -AudioDelay <ms>          Delay manuale audio in ms (sommato ad auto se -as)
  -sd, -SubtitleDelay <ms>       Delay manuale sottotitoli in ms
  -at, -AnalysisTime <sec>       Durata analisi audio in secondi (default: 300 = 5 min)

OPZIONI FILTRO:
  -ac, -AudioCodec <codec>       Importa solo audio con codec specifico (es: E-AC-3, DTS)
  -so, -SubOnly                  Importa solo sottotitoli (ignora audio)
  -ao, -AudioOnly                Importa solo audio (ignora sottotitoli)
  -ksa, -KeepSourceAudioLangs    Lingue audio da mantenere nel sorgente (es: eng,jpn)
  -kss, -KeepSourceSubtitleLangs Lingue sub da mantenere nel sorgente

OPZIONI MATCHING:
  -m,  -MatchPattern <regex>     Pattern per matching episodi (default: S(\d+)E(\d+))
  -r,  -Recursive                Cerca ricorsivamente nelle sottocartelle (default: true)
  -ext, -FileExtensions <list>   Estensioni file da cercare (default: mkv). Separa con virgola: mkv,mp4,avi

OPZIONI TOOL:
  -mkv,  -MkvMergePath <path>    Percorso mkvmerge (default: cerca in PATH)
  -tools, -ToolsFolder <path>    Cartella per tool scaricati (ffmpeg)

ALTRE OPZIONI:
  -DryRun, -dry, -n              Mostra cosa verrebbe fatto senza eseguire
  -h, -Help                      Mostra questo messaggio

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

ESEMPI:
  # Unisci tracce italiane con auto-sync
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -d ""D:\Out"" -as

  # Dry run (mostra cosa farebbe senza eseguire)
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -d ""D:\Out"" -as -DryRun

  # Solo audio E-AC-3 italiano
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -ac ""E-AC-3"" -d ""D:\Out"" -as

  # Solo sottotitoli (no audio)
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -so -d ""D:\Out"" -as

  # Sostituisci traccia ita esistente (rimuovi vecchia, aggiungi nuova)
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -ksa eng -d ""D:\Out"" -as

  # Mantieni solo eng/jpn audio e eng sub dal sorgente
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -ksa eng,jpn -kss eng -d ""D:\Out""

  # Pattern custom (1x01 invece di S01E01)
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -m ""(\d+)x(\d+)"" -d ""D:\Out""

  # Cerca anche file MP4 e AVI oltre a MKV
  MergeLanguageTracks -s ""D:\EN"" -l ""D:\IT"" -t ita -ext mkv,mp4,avi -d ""D:\Out"" -as

CODICI LINGUA (ISO 639-2):
  Comuni: ita, eng, jpn, ger/deu, fra/fre, spa, por, rus, chi/zho, kor
  Altri:  ara, hin, pol, tur, nld/dut, swe, nor, dan, fin, hun, ces/cze
  Speciali: und (undefined), mul (multiple), zxx (no language)

REQUISITI:
  - MKVToolNix (mkvmerge) nel PATH
  - ffmpeg per AutoSync (scaricato automaticamente se mancante)

NOTE:
  AutoSync analizza i primi 5 min di audio (configurabile con -at), rileva silenzi
  e picchi di volume, e trova l'offset ottimale. Funziona anche con lingue diverse
  perche' musica, effetti sonori e silenzi sono identici tra versioni doppiate.
  Precisione: ~1ms (ricerca in 3 fasi: 500ms -> 10ms -> 1ms)
";
            Console.WriteLine(helpText);
        }

        /// <summary>
        /// Normalizza un percorso file system risolvendolo alla forma assoluta completa
        /// </summary>
        /// <param name="path">Il percorso da normalizzare.</param>
        /// <returns>Il percorso assoluto normalizzato.</returns>
        private static string NormalizePath(string path)
        {
            string result = path;

            if (path.Length > 0)
            {
                result = Path.GetFullPath(path);
                result = result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return result;
        }

        /// <summary>
        /// Estrae l'identificatore episodio da un nome file usando il pattern regex di match.
        /// </summary>
        /// <param name="fileName">Il nome file da cui estrarre.</param>
        /// <param name="pattern">Il pattern regex con gruppi di cattura.</param>
        /// <returns>La stringa identificatore episodio, o stringa vuota se nessun match.</returns>
        private static string GetEpisodeIdentifier(string fileName, string pattern)
        {
            string result = "";

            Match match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (match.Groups.Count > 1)
                {
                    // Unisci tutti i gruppi di cattura con underscore
                    StringBuilder sb = new StringBuilder();
                    for (int g = 1; g < match.Groups.Count; g++)
                    {
                        if (g > 1)
                        {
                            sb.Append("_");
                        }
                        sb.Append(match.Groups[g].Value);
                    }
                    result = sb.ToString();
                }
                else
                {
                    // Nessun gruppo di cattura, usa il match completo come identificatore
                    result = match.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Formatta un valore di ritardo in millisecondi per la visualizzazione, includendo il prefisso segno.
        /// </summary>
        /// <param name="delayMs">Il ritardo in millisecondi.</param>
        /// <returns>Una stringa formattata come "+500ms", "-200ms" o "0ms".</returns>
        private static string FormatDelay(int delayMs)
        {
            string result = "0ms";

            if (delayMs > 0)
            {
                result = "+" + delayMs + "ms";
            }
            else if (delayMs < 0)
            {
                result = delayMs + "ms";
            }

            return result;
        }

        /// <summary>
        /// Formatta le informazioni traccia per output console leggibile.
        /// </summary>
        /// <param name="tracks">La lista di tracce da formattare.</param>
        /// <returns>Una stringa formattata multilinea.</returns>
        private static string FormatTrackInfo(List<TrackInfo> tracks)
        {
            string result = "  Nessuna";

            if (tracks.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.AppendLine();
                    }
                    string name = (tracks[i].Name.Length > 0) ? " - \"" + tracks[i].Name + "\"" : "";
                    string lang = (tracks[i].Language.Length > 0) ? "[" + tracks[i].Language + "]" : "[und]";
                    sb.Append("  Track " + tracks[i].Id + ": " + tracks[i].Codec + " " + lang + name);
                }
                result = sb.ToString();
            }

            return result;
        }

        /// <summary>
        /// Formatta una lista di ID traccia come stringa separata da virgole.
        /// </summary>
        /// <param name="trackIds">La lista di ID traccia.</param>
        /// <returns>Una stringa formattata come "1, 2, 3" o "Nessuna".</returns>
        private static string FormatTrackIdList(List<int> trackIds)
        {
            string result = "Nessuna";

            if (trackIds.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < trackIds.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(trackIds[i]);
                }
                result = sb.ToString();
            }

            return result;
        }

        /// <summary>
        /// Raccoglie ricorsivamente tutti i file video da una directory, opzionalmente cercando nelle sottocartelle.
        /// </summary>
        /// <param name="folder">La cartella root da cercare.</param>
        /// <param name="extensions">Lista di estensioni da cercare (senza punto).</param>
        /// <param name="recursive">Se includere le sottodirectory.</param>
        /// <returns>Una lista di percorsi completi ai file trovati.</returns>
        private static List<string> FindVideoFiles(string folder, List<string> extensions, bool recursive)
        {
            List<string> files = new List<string>();
            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Cerca per ogni estensione
            for (int e = 0; e < extensions.Count; e++)
            {
                string pattern = "*." + extensions[e];
                string[] found = Directory.GetFiles(folder, pattern, searchOption);
                for (int i = 0; i < found.Length; i++)
                {
                    files.Add(found[i]);
                }
            }

            return files;
        }

        /// <summary>
        /// Valida tutte le opzioni richieste ed esce con errore se qualcuna non e' valida.
        /// </summary>
        /// <param name="opts">Le opzioni parsate da validare.</param>
        /// <returns>True se tutte le validazioni passano.</returns>
        private static bool ValidateOptions(Options opts)
        {
            bool valid = true;

            // Verifica parametri obbligatori
            if (opts.SourceFolder.Length == 0 || opts.LanguageFolder.Length == 0 || opts.TargetLanguage.Count == 0)
            {
                ConsoleHelper.WriteRed("Errore: parametri obbligatori mancanti.");
                ConsoleHelper.WriteYellow("Uso: MergeLanguageTracks -s <source> -l <lang> -t <lingua> -d <dest> [-as] [-DryRun]");
                ConsoleHelper.WriteDarkGray("     Usa -h per vedere tutte le opzioni.");
                valid = false;
                return valid;
            }

            // Valida esistenza cartella sorgente
            if (!Directory.Exists(opts.SourceFolder))
            {
                ConsoleHelper.WriteRed("Errore: cartella sorgente non trovata: " + opts.SourceFolder);
                valid = false;
                return valid;
            }

            // Valida esistenza cartella lingua
            if (!Directory.Exists(opts.LanguageFolder))
            {
                ConsoleHelper.WriteRed("Errore: cartella lingua non trovata: " + opts.LanguageFolder);
                valid = false;
                return valid;
            }

            // Valida formato codice lingua
            Regex langRegex = new Regex(@"^[a-z]{2,3}$");
            for (int i = 0; i < opts.TargetLanguage.Count; i++)
            {
                if (!langRegex.IsMatch(opts.TargetLanguage[i].ToLower()))
                {
                    ConsoleHelper.WriteRed("Errore: lingua non valida '" + opts.TargetLanguage[i] + "'. Usa codice ISO 639-2 (es: ita, eng, jpn)");
                    valid = false;
                    return valid;
                }
            }

            // Valida lingue target contro lista ISO 639-2
            for (int i = 0; i < opts.TargetLanguage.Count; i++)
            {
                if (!LanguageValidator.IsValid(opts.TargetLanguage[i]))
                {
                    ConsoleHelper.WriteRed("Errore: lingua '" + opts.TargetLanguage[i] + "' non riconosciuta.");
                    List<string> suggestions = LanguageValidator.GetSimilar(opts.TargetLanguage[i], 3);
                    if (suggestions.Count > 0)
                    {
                        ConsoleHelper.WriteYellow("Forse intendevi: " + string.Join(", ", suggestions) + "?");
                    }
                    else
                    {
                        ConsoleHelper.WriteYellow("Usa codici ISO 639-2 (es: ita, eng, jpn, ger, fra, spa)");
                    }
                    valid = false;
                    return valid;
                }
            }

            // Valida KeepSourceAudioLangs
            for (int i = 0; i < opts.KeepSourceAudioLangs.Count; i++)
            {
                if (!LanguageValidator.IsValid(opts.KeepSourceAudioLangs[i]))
                {
                    ConsoleHelper.WriteRed("Errore: lingua '" + opts.KeepSourceAudioLangs[i] + "' in -ksa non riconosciuta.");
                    List<string> suggestions = LanguageValidator.GetSimilar(opts.KeepSourceAudioLangs[i], 3);
                    if (suggestions.Count > 0)
                    {
                        ConsoleHelper.WriteYellow("Forse intendevi: " + string.Join(", ", suggestions) + "?");
                    }
                    valid = false;
                    return valid;
                }
            }

            // Valida KeepSourceSubtitleLangs
            for (int i = 0; i < opts.KeepSourceSubtitleLangs.Count; i++)
            {
                if (!LanguageValidator.IsValid(opts.KeepSourceSubtitleLangs[i]))
                {
                    ConsoleHelper.WriteRed("Errore: lingua '" + opts.KeepSourceSubtitleLangs[i] + "' in -kss non riconosciuta.");
                    List<string> suggestions = LanguageValidator.GetSimilar(opts.KeepSourceSubtitleLangs[i], 3);
                    if (suggestions.Count > 0)
                    {
                        ConsoleHelper.WriteYellow("Forse intendevi: " + string.Join(", ", suggestions) + "?");
                    }
                    valid = false;
                    return valid;
                }
            }

            // Valida mutua esclusione SubOnly e AudioOnly
            if (opts.SubOnly && opts.AudioOnly)
            {
                ConsoleHelper.WriteRed("Errore: -so e -ao non possono essere usati insieme.");
                valid = false;
                return valid;
            }

            // Valida modalita' output
            if (!string.Equals(opts.OutputMode, "Destination", StringComparison.OrdinalIgnoreCase) && !string.Equals(opts.OutputMode, "Overwrite", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteRed("Errore: OutputMode deve essere 'Destination' o 'Overwrite'.");
                valid = false;
                return valid;
            }

            // Valida requisito cartella destinazione
            if (string.Equals(opts.OutputMode, "Destination", StringComparison.OrdinalIgnoreCase) && opts.DestinationFolder.Length == 0)
            {
                ConsoleHelper.WriteRed("Errore: DestinationFolder e' obbligatorio quando OutputMode e' 'Destination'.");
                valid = false;
                return valid;
            }

            // Valida codec audio se specificato
            if (opts.AudioCodec.Length > 0)
            {
                string[] codecPatterns = CodecMapping.GetCodecPatterns(opts.AudioCodec);
                if (codecPatterns == null)
                {
                    ConsoleHelper.WriteRed("Errore: codec '" + opts.AudioCodec + "' non riconosciuto.");
                    ConsoleHelper.WriteYellow("Codec validi: " + CodecMapping.GetAllCodecNames());
                    valid = false;
                    return valid;
                }
            }

            return valid;
        }

        /// <summary>
        /// Stampa il riepilogo configurazione corrente sulla console.
        /// </summary>
        /// <param name="opts">Le opzioni parsate e validate.</param>
        /// <param name="codecPatterns">Pattern codec risolti, o null se nessun filtro codec.</param>
        private static void PrintConfiguration(Options opts, string[] codecPatterns)
        {
            ConsoleHelper.WriteYellow("Configurazione:");
            ConsoleHelper.WritePlain("  Cartella sorgente:   " + opts.SourceFolder);
            ConsoleHelper.WritePlain("  Cartella lingua:     " + opts.LanguageFolder);
            ConsoleHelper.WritePlain("  Lingua target:       " + string.Join(", ", opts.TargetLanguage));
            ConsoleHelper.WritePlain("  Pattern matching:    " + opts.MatchPattern);
            ConsoleHelper.WritePlain("  Estensioni file:     " + string.Join(", ", opts.FileExtensions));
            ConsoleHelper.WritePlain("  Modalita' output:    " + opts.OutputMode);

            if (string.Equals(opts.OutputMode, "Destination", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WritePlain("  Cartella output:     " + opts.DestinationFolder);
            }

            // Mostra configurazione sync
            if (opts.AutoSync)
            {
                ConsoleHelper.WriteGreen("  Auto-sync:           ATTIVO (audio fingerprint)");
                if (opts.AudioDelay != 0 || opts.SubtitleDelay != 0)
                {
                    ConsoleHelper.WriteDarkYellow("  Offset manuale:      Audio " + FormatDelay(opts.AudioDelay) + ", Sub " + FormatDelay(opts.SubtitleDelay) + " (sommato ad auto)");
                }
            }
            else
            {
                ConsoleHelper.WritePlain("  Delay audio:         " + FormatDelay(opts.AudioDelay));
                ConsoleHelper.WritePlain("  Delay sottotitoli:   " + FormatDelay(opts.SubtitleDelay));
            }

            // Mostra flag filtro
            if (opts.SubOnly)
            {
                ConsoleHelper.WriteCyan("  Solo sottotitoli:    SI (audio ignorato)");
            }
            if (opts.AudioOnly)
            {
                ConsoleHelper.WriteCyan("  Solo audio:          SI (sottotitoli ignorati)");
            }
            else if (opts.AudioCodec.Length > 0 && codecPatterns != null)
            {
                ConsoleHelper.WriteGreen("  Codec selezionato: " + opts.AudioCodec + " -> matcha: " + string.Join(", ", codecPatterns));
            }

            // Mostra filtri tracce sorgente
            if (opts.KeepSourceAudioLangs.Count > 0)
            {
                ConsoleHelper.WritePlain("  Mantieni audio src:  " + string.Join(", ", opts.KeepSourceAudioLangs));
            }
            if (opts.KeepSourceSubtitleLangs.Count > 0)
            {
                ConsoleHelper.WritePlain("  Mantieni sub src:    " + string.Join(", ", opts.KeepSourceSubtitleLangs));
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Estrae le lingue uniche delle tracce audio da una lista di tracce.
        /// </summary>
        /// <param name="tracks">Lista di tracce.</param>
        /// <returns>Lista di codici lingua unici.</returns>
        private static List<string> GetAudioLanguages(List<TrackInfo> tracks)
        {
            List<string> langs = new List<string>();

            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (string.Equals(tracks[i].Type, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        string lang = tracks[i].Language.Length > 0 ? tracks[i].Language : "und";
                        if (!langs.Contains(lang))
                        {
                            langs.Add(lang);
                        }
                    }
                }
            }

            return langs;
        }

        /// <summary>
        /// Estrae le lingue uniche delle tracce sottotitoli da una lista di tracce.
        /// </summary>
        /// <param name="tracks">Lista di tracce.</param>
        /// <returns>Lista di codici lingua unici.</returns>
        private static List<string> GetSubtitleLanguages(List<TrackInfo> tracks)
        {
            List<string> langs = new List<string>();

            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (string.Equals(tracks[i].Type, "subtitles", StringComparison.OrdinalIgnoreCase))
                    {
                        string lang = tracks[i].Language.Length > 0 ? tracks[i].Language : "und";
                        if (!langs.Contains(lang))
                        {
                            langs.Add(lang);
                        }
                    }
                }
            }

            return langs;
        }

        /// <summary>
        /// Stampa il report dettagliato con tabelle per source, lang e result.
        /// </summary>
        /// <param name="records">Lista dei record di elaborazione.</param>
        /// <param name="isDryRun">Se in modalita' dry run.</param>
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

            if (validRecords.Count == 0)
            {
                return;
            }

            ConsoleHelper.WriteCyan("\n========================================");
            ConsoleHelper.WriteCyan("  Report Dettagliato");
            ConsoleHelper.WriteCyan("========================================\n");

            // Tabella 1: Source Files
            ConsoleHelper.WriteYellow("SOURCE FILES:");
            ConsoleHelper.WritePlain("  " + PadRight("Episode", 12) + PadRight("Audio", 20) + PadRight("Subtitles", 20) + PadRight("Size", 12));
            ConsoleHelper.WriteDarkGray("  " + new string('-', 64));

            for (int i = 0; i < validRecords.Count; i++)
            {
                FileProcessingRecord r = validRecords[i];
                string line = "  " + PadRight(r.EpisodeId, 12) + PadRight(FileProcessingRecord.FormatLangs(r.SourceAudioLangs), 20) + PadRight(FileProcessingRecord.FormatLangs(r.SourceSubLangs), 20) + PadRight(FileProcessingRecord.FormatSize(r.SourceSize), 12);
                ConsoleHelper.WritePlain(line);
            }

            Console.WriteLine();

            // Tabella 2: Language Files
            ConsoleHelper.WriteYellow("LANGUAGE FILES:");
            ConsoleHelper.WritePlain("  " + PadRight("Episode", 12) + PadRight("Audio", 20) + PadRight("Subtitles", 20) + PadRight("Size", 12));
            ConsoleHelper.WriteDarkGray("  " + new string('-', 64));

            for (int i = 0; i < validRecords.Count; i++)
            {
                FileProcessingRecord r = validRecords[i];
                string line = "  " + PadRight(r.EpisodeId, 12) + PadRight(FileProcessingRecord.FormatLangs(r.LangAudioLangs), 20) + PadRight(FileProcessingRecord.FormatLangs(r.LangSubLangs), 20) + PadRight(FileProcessingRecord.FormatSize(r.LangSize), 12);
                ConsoleHelper.WritePlain(line);
            }

            Console.WriteLine();

            // Tabella 3: Result Files
            ConsoleHelper.WriteYellow("RESULT FILES:");
            ConsoleHelper.WritePlain("  " + PadRight("Episode", 12) + PadRight("Audio", 15) + PadRight("Subtitles", 15) + PadRight("Size", 10) + PadRight("Delay", 12) + PadRight("FFmpeg", 10) + PadRight("AutoSync", 10) + PadRight("Merge", 10));
            ConsoleHelper.WriteDarkGray("  " + new string('-', 94));

            for (int i = 0; i < validRecords.Count; i++)
            {
                FileProcessingRecord r = validRecords[i];
                string sizeStr = isDryRun ? "N/A" : FileProcessingRecord.FormatSize(r.ResultSize);
                string delayStr = FormatDelay(r.AudioDelayApplied);
                string ffmpegStr = r.FfmpegTimeMs > 0 ? r.FfmpegTimeMs + "ms" : "-";
                string autoSyncStr = r.AutoSyncTimeMs > 0 ? r.AutoSyncTimeMs + "ms" : "-";
                string mergeStr = r.MergeTimeMs > 0 ? r.MergeTimeMs + "ms" : (isDryRun ? "N/A" : "-");

                string line = "  " + PadRight(r.EpisodeId, 12) + PadRight(FileProcessingRecord.FormatLangs(r.ResultAudioLangs), 15) + PadRight(FileProcessingRecord.FormatLangs(r.ResultSubLangs), 15) + PadRight(sizeStr, 10) + PadRight(delayStr, 12) + PadRight(ffmpegStr, 10) + PadRight(autoSyncStr, 10) + PadRight(mergeStr, 10);
                ConsoleHelper.WritePlain(line);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Pad a destra una stringa per allineamento tabellare.
        /// </summary>
        /// <param name="text">Testo da allineare.</param>
        /// <param name="width">Larghezza totale.</param>
        /// <returns>Stringa con padding.</returns>
        private static string PadRight(string text, int width)
        {
            if (text.Length >= width)
            {
                return text.Substring(0, width - 1) + " ";
            }
            return text + new string(' ', width - text.Length);
        }

        /// <summary>
        /// Stampa il riepilogo elaborazione finale con statistiche colorate.
        /// </summary>
        /// <param name="stats">Statistiche elaborazione.</param>
        /// <param name="autoSync">Se auto-sync era abilitato.</param>
        private static void PrintSummary(ProcessingStats stats, bool autoSync)
        {
            ConsoleHelper.WriteCyan("\n========================================");
            ConsoleHelper.WriteCyan("  Riepilogo");
            ConsoleHelper.WriteCyan("========================================");
            ConsoleHelper.WriteGreen("  Elaborati:     " + stats.Processed);
            ConsoleHelper.WriteYellow("  Saltati:       " + stats.Skipped);
            ConsoleHelper.WriteYellow("  Senza match:   " + stats.NoMatch);
            ConsoleHelper.WriteYellow("  Senza tracce:  " + stats.NoTracks);

            if (autoSync)
            {
                if (stats.SyncFailed > 0)
                {
                    ConsoleHelper.WriteYellow("  Sync falliti:  " + stats.SyncFailed);
                }
                else
                {
                    ConsoleHelper.WriteGreen("  Sync falliti:  " + stats.SyncFailed);
                }
            }

            if (stats.Errors > 0)
            {
                ConsoleHelper.WriteRed("  Errori:        " + stats.Errors);
            }
            else
            {
                ConsoleHelper.WriteGreen("  Errori:        " + stats.Errors);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Elabora un singolo file sorgente.
        /// </summary>
        /// <param name="sourceFilePath">Percorso completo al file MKV sorgente.</param>
        /// <param name="languageIndex">Dizionario che mappa ID episodio a percorsi file lingua.</param>
        /// <param name="opts">Le opzioni parsate.</param>
        /// <param name="service">L'istanza del servizio MKV tools.</param>
        /// <param name="syncService">L'istanza del servizio audio sync, o null se auto-sync non abilitato.</param>
        /// <param name="stats">Statistiche elaborazione.</param>
        /// <param name="records">Lista record per report dettagliato.</param>
        /// <param name="codecPatterns">Pattern codec risolti per il filtraggio, o null.</param>
        /// <param name="filterSourceAudio">Se le tracce audio sorgente devono essere filtrate.</param>
        /// <param name="filterSourceSubs">Se le tracce sottotitoli sorgente devono essere filtrate.</param>
        private static void ProcessFile(string sourceFilePath, Dictionary<string, string> languageIndex, Options opts, MkvToolsService service, AudioSyncService syncService, ProcessingStats stats, List<FileProcessingRecord> records, string[] codecPatterns, bool filterSourceAudio, bool filterSourceSubs)
        {
            string sourceFileName = Path.GetFileName(sourceFilePath);

            // Crea record per questo file
            FileProcessingRecord record = new FileProcessingRecord();
            record.SourceFileName = sourceFileName;

            // Ottieni dimensione file sorgente
            FileInfo sourceFileInfo = new FileInfo(sourceFilePath);
            record.SourceSize = sourceFileInfo.Length;

            ConsoleHelper.WriteDarkGray("----------------------------------------");
            ConsoleHelper.WriteWhite("Elaborazione: " + sourceFileName);

            // Estrai identificatore episodio
            string episodeId = GetEpisodeIdentifier(sourceFileName, opts.MatchPattern);

            if (episodeId.Length == 0)
            {
                ConsoleHelper.WriteYellow("  [SKIP] Impossibile estrarre ID episodio dal nome file");
                record.SkipReason = "No episode ID";
                records.Add(record);
                stats.Skipped++;
                return;
            }

            record.EpisodeId = episodeId;
            ConsoleHelper.WriteDarkGray("  ID Episodio: " + episodeId);

            // Trova file lingua corrispondente
            if (!languageIndex.ContainsKey(episodeId))
            {
                ConsoleHelper.WriteYellow("  [SKIP] Nessun file lingua corrispondente");
                record.SkipReason = "No match";
                records.Add(record);
                stats.NoMatch++;
                return;
            }

            string languageFilePath = languageIndex[episodeId];
            record.LangFileName = Path.GetFileName(languageFilePath);

            // Ottieni dimensione file lingua
            FileInfo langFileInfo = new FileInfo(languageFilePath);
            record.LangSize = langFileInfo.Length;

            ConsoleHelper.WriteDarkCyan("  Match: " + Path.GetFileName(languageFilePath));

            // Ottieni info tracce per entrambi i file
            List<TrackInfo> sourceTracks = service.GetTrackInfo(sourceFilePath);
            List<TrackInfo> langTracks = service.GetTrackInfo(languageFilePath);

            // Popola lingue sorgente nel record
            record.SourceAudioLangs = GetAudioLanguages(sourceTracks);
            record.SourceSubLangs = GetSubtitleLanguages(sourceTracks);

            // Popola lingue lingua nel record
            record.LangAudioLangs = GetAudioLanguages(langTracks);
            record.LangSubLangs = GetSubtitleLanguages(langTracks);

            if (langTracks == null)
            {
                ConsoleHelper.WriteRed("  [ERRORE] Impossibile leggere info tracce file lingua");
                record.SkipReason = "Track read error";
                records.Add(record);
                stats.Errors++;
                return;
            }

            // Calcola ritardi effettivi
            int effectiveAudioDelay = opts.AudioDelay;
            int effectiveSubDelay = opts.SubtitleDelay;

            // Calcolo auto-sync
            if (opts.AutoSync && syncService != null)
            {
                ConsoleHelper.WriteCyan("\n  [AUTO-SYNC] Modalita': Audio fingerprinting (silenzi + picchi)");

                int autoOffset = syncService.ComputeAutoSyncOffset(sourceFilePath, languageFilePath, sourceTracks, opts.TargetLanguage, service.IsLanguageInList, opts.AnalysisTime);

                // Recupera tempi misurati dal servizio
                record.FfmpegTimeMs = syncService.FfmpegTimeMs;
                record.AutoSyncTimeMs = syncService.AutoSyncTimeMs;

                if (autoOffset != int.MinValue)
                {
                    ConsoleHelper.WriteGreen("  [AUTO-SYNC] Offset rilevato: " + FormatDelay(autoOffset) + " (FFmpeg: " + record.FfmpegTimeMs + "ms, Sync: " + record.AutoSyncTimeMs + "ms)");

                    // Somma offset manuale con offset auto
                    effectiveAudioDelay = autoOffset + opts.AudioDelay;
                    effectiveSubDelay = autoOffset + opts.SubtitleDelay;

                    if (opts.AudioDelay != 0 || opts.SubtitleDelay != 0)
                    {
                        ConsoleHelper.WriteDarkYellow("  [AUTO-SYNC] Offset finale (auto + manuale): Audio " + FormatDelay(effectiveAudioDelay) + ", Sub " + FormatDelay(effectiveSubDelay));
                    }
                }
                else
                {
                    ConsoleHelper.WriteYellow("  [AUTO-SYNC] Impossibile calcolare offset, uso valori manuali");
                    stats.SyncFailed++;
                }
            }

            // Ottieni ID tracce sorgente da mantenere
            List<int> sourceAudioIds = new List<int>();
            List<int> sourceSubIds = new List<int>();

            if (sourceTracks != null)
            {
                if (filterSourceAudio)
                {
                    sourceAudioIds = service.GetSourceTrackIds(sourceTracks, "audio", opts.KeepSourceAudioLangs);
                    ConsoleHelper.WriteDarkYellow("\n  Audio sorgente da mantenere: " + FormatTrackIdList(sourceAudioIds));
                }
                if (filterSourceSubs)
                {
                    sourceSubIds = service.GetSourceTrackIds(sourceTracks, "subtitles", opts.KeepSourceSubtitleLangs);
                    ConsoleHelper.WriteDarkYellow("  Sub sorgente da mantenere:   " + FormatTrackIdList(sourceSubIds));
                }
            }

            // Raccogli tracce dal file lingua per tutte le lingue target
            List<TrackInfo> audioTracks = new List<TrackInfo>();
            List<TrackInfo> subtitleTracks = new List<TrackInfo>();

            for (int t = 0; t < opts.TargetLanguage.Count; t++)
            {
                string tl = opts.TargetLanguage[t];

                // Tracce audio (a meno che SubOnly)
                if (!opts.SubOnly)
                {
                    List<TrackInfo> foundAudio = service.GetFilteredTracks(langTracks, tl, "audio", codecPatterns);
                    for (int a = 0; a < foundAudio.Count; a++)
                    {
                        audioTracks.Add(foundAudio[a]);
                    }
                }

                // Tracce sottotitoli (a meno che AudioOnly)
                if (!opts.AudioOnly)
                {
                    List<TrackInfo> foundSubs = service.GetFilteredTracks(langTracks, tl, "subtitles", null);
                    for (int s = 0; s < foundSubs.Count; s++)
                    {
                        subtitleTracks.Add(foundSubs[s]);
                    }
                }
            }

            // Mostra tracce trovate
            string codecSuffix = (opts.AudioCodec.Length > 0) ? " / " + opts.AudioCodec : "";
            ConsoleHelper.WriteMagenta("\n  Audio file lingua (" + string.Join(",", opts.TargetLanguage) + codecSuffix + "):");
            ConsoleHelper.WritePlain(FormatTrackInfo(audioTracks));

            ConsoleHelper.WriteMagenta("\n  Sottotitoli file lingua (" + string.Join(",", opts.TargetLanguage) + "):");
            ConsoleHelper.WritePlain(FormatTrackInfo(subtitleTracks));

            // Salta se nessuna traccia trovata
            if (audioTracks.Count == 0 && subtitleTracks.Count == 0)
            {
                ConsoleHelper.WriteYellow("\n  [SKIP] Nessuna traccia corrispondente trovata");
                record.SkipReason = "No matching tracks";
                records.Add(record);
                stats.NoTracks++;
                return;
            }

            // Determina percorso output
            string tempOutput = "";
            string finalOutput = "";

            if (string.Equals(opts.OutputMode, "Overwrite", StringComparison.OrdinalIgnoreCase))
            {
                // Usa file temp, poi sostituisci originale
                string sourceDir = Path.GetDirectoryName(sourceFilePath);
                string sourceNameNoExt = Path.GetFileNameWithoutExtension(sourceFilePath);
                tempOutput = Path.Combine(sourceDir, sourceNameNoExt + "_TEMP.mkv");
                finalOutput = sourceFilePath;
            }
            else
            {
                // Modalita' Destination: preserva struttura directory
                string normalizedSource = NormalizePath(sourceFilePath);
                string normalizedFolder = NormalizePath(opts.SourceFolder);
                string relativePath = normalizedSource.Substring(normalizedFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                finalOutput = Path.Combine(opts.DestinationFolder, relativePath);
                tempOutput = finalOutput;

                // Crea sottodirectory destinazione se necessario
                string destDir = Path.GetDirectoryName(finalOutput);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
            }

            // Costruisci argomenti merge
            List<string> mergeArgs = service.BuildMergeArguments(sourceFilePath, languageFilePath, tempOutput, sourceAudioIds, sourceSubIds, audioTracks, subtitleTracks, effectiveAudioDelay, effectiveSubDelay, filterSourceAudio, filterSourceSubs);

            ConsoleHelper.WriteDarkGray("\n  Output: " + finalOutput);
            ConsoleHelper.WriteDarkGray("  Delay applicato: Audio " + FormatDelay(effectiveAudioDelay) + ", Sub " + FormatDelay(effectiveSubDelay));

            // Popola record con info risultato previste
            record.AudioDelayApplied = effectiveAudioDelay;
            record.SubDelayApplied = effectiveSubDelay;
            record.ResultFileName = Path.GetFileName(finalOutput);

            // Calcola lingue risultato (source filtrate + lang importate)
            List<string> resultAudioLangs = new List<string>();
            List<string> resultSubLangs = new List<string>();

            // Audio dal sorgente (se non filtrate, tutte; se filtrate, solo quelle che esistono E sono in KeepSourceAudioLangs)
            if (!filterSourceAudio)
            {
                for (int i = 0; i < record.SourceAudioLangs.Count; i++)
                {
                    if (!resultAudioLangs.Contains(record.SourceAudioLangs[i]))
                    {
                        resultAudioLangs.Add(record.SourceAudioLangs[i]);
                    }
                }
            }
            else
            {
                // Aggiungi solo le lingue che esistono nel sorgente E sono nella lista keep
                for (int i = 0; i < record.SourceAudioLangs.Count; i++)
                {
                    string srcLang = record.SourceAudioLangs[i];
                    bool keepThis = false;
                    for (int k = 0; k < opts.KeepSourceAudioLangs.Count; k++)
                    {
                        if (string.Equals(srcLang, opts.KeepSourceAudioLangs[k], StringComparison.OrdinalIgnoreCase))
                        {
                            keepThis = true;
                            break;
                        }
                    }
                    if (keepThis && !resultAudioLangs.Contains(srcLang))
                    {
                        resultAudioLangs.Add(srcLang);
                    }
                }
            }

            // Audio importate dal file lingua
            for (int i = 0; i < audioTracks.Count; i++)
            {
                string lang = audioTracks[i].Language.Length > 0 ? audioTracks[i].Language : "und";
                if (!resultAudioLangs.Contains(lang))
                {
                    resultAudioLangs.Add(lang);
                }
            }

            // Sottotitoli dal sorgente (se non filtrati, tutti; se filtrati, solo quelli che esistono E sono in KeepSourceSubtitleLangs)
            if (!filterSourceSubs)
            {
                for (int i = 0; i < record.SourceSubLangs.Count; i++)
                {
                    if (!resultSubLangs.Contains(record.SourceSubLangs[i]))
                    {
                        resultSubLangs.Add(record.SourceSubLangs[i]);
                    }
                }
            }
            else
            {
                // Aggiungi solo le lingue che esistono nel sorgente E sono nella lista keep
                for (int i = 0; i < record.SourceSubLangs.Count; i++)
                {
                    string srcLang = record.SourceSubLangs[i];
                    bool keepThis = false;
                    for (int k = 0; k < opts.KeepSourceSubtitleLangs.Count; k++)
                    {
                        if (string.Equals(srcLang, opts.KeepSourceSubtitleLangs[k], StringComparison.OrdinalIgnoreCase))
                        {
                            keepThis = true;
                            break;
                        }
                    }
                    if (keepThis && !resultSubLangs.Contains(srcLang))
                    {
                        resultSubLangs.Add(srcLang);
                    }
                }
            }

            // Sottotitoli importati dal file lingua
            for (int i = 0; i < subtitleTracks.Count; i++)
            {
                string lang = subtitleTracks[i].Language.Length > 0 ? subtitleTracks[i].Language : "und";
                if (!resultSubLangs.Contains(lang))
                {
                    resultSubLangs.Add(lang);
                }
            }

            record.ResultAudioLangs = resultAudioLangs;
            record.ResultSubLangs = resultSubLangs;

            // Esegui o visualizza comando
            if (opts.DryRun)
            {
                ConsoleHelper.WriteCyan("\n  [DRY-RUN] Comando che verrebbe eseguito:");
                ConsoleHelper.WriteDarkGray("  " + service.FormatMergeCommand(mergeArgs));

                // In dry run segna come success per includerlo nel report
                record.Success = true;
                records.Add(record);
            }
            else
            {
                ConsoleHelper.WriteYellow("\n  Unione in corso...");

                // Misura tempo merge
                Stopwatch mergeStopwatch = new Stopwatch();
                mergeStopwatch.Start();

                string mergeOutput = "";
                int exitCode = service.ExecuteMerge(mergeArgs, out mergeOutput);

                mergeStopwatch.Stop();
                record.MergeTimeMs = mergeStopwatch.ElapsedMilliseconds;

                // Exit code 0 e 1 sono entrambi considerati successo da mkvmerge
                if (exitCode == 0 || exitCode == 1)
                {
                    ConsoleHelper.WriteGreen("  [OK] Unione completata");

                    // Gestisci modalita' overwrite: sostituisci originale
                    if (string.Equals(opts.OutputMode, "Overwrite", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(sourceFilePath);
                        File.Move(tempOutput, finalOutput);
                        ConsoleHelper.WriteGreen("  [OK] File originale sostituito");
                    }

                    // Ottieni dimensione file risultato
                    if (File.Exists(finalOutput))
                    {
                        FileInfo resultFileInfo = new FileInfo(finalOutput);
                        record.ResultSize = resultFileInfo.Length;
                    }

                    record.Success = true;
                    records.Add(record);
                    stats.Processed++;
                }
                else
                {
                    ConsoleHelper.WriteRed("  [ERRORE] mkvmerge fallito con codice " + exitCode);
                    if (mergeOutput.Length > 0)
                    {
                        ConsoleHelper.WriteDarkRed("  Output: " + mergeOutput);
                    }

                    // Pulisci output temp fallito
                    if (File.Exists(tempOutput))
                    {
                        try { File.Delete(tempOutput); } catch { }
                    }

                    record.SkipReason = "Merge failed: " + exitCode;
                    records.Add(record);
                    stats.Errors++;
                }
            }
        }

        #endregion

        #region Entry point

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args">Argomenti da riga di comando.</param>
        /// <returns>Codice uscita: 0 per successo, 1 per errore.</returns>
        static int Main(string[] args)
        {
            int exitCode = 0;

            // Parsa argomenti da riga di comando
            Options opts = Options.Parse(args);

            // Gestisci richiesta help
            if (opts.Help || args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            // Normalizza percorsi
            if (opts.SourceFolder.Length > 0)
            {
                opts.SourceFolder = NormalizePath(opts.SourceFolder);
            }
            if (opts.LanguageFolder.Length > 0)
            {
                opts.LanguageFolder = NormalizePath(opts.LanguageFolder);
            }
            if (opts.DestinationFolder.Length > 0)
            {
                opts.DestinationFolder = NormalizePath(opts.DestinationFolder);
            }

            // Determina cartella tools
            if (opts.ToolsFolder.Length == 0)
            {
                string appDir = AppContext.BaseDirectory;
                opts.ToolsFolder = Path.Combine(appDir, "tools");
            }

            // Valida tutte le opzioni
            if (!ValidateOptions(opts))
            {
                return 1;
            }

            // Crea cartella destinazione se necessario
            if (string.Equals(opts.OutputMode, "Destination", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(opts.DestinationFolder))
            {
                ConsoleHelper.WriteYellow("Creazione cartella destinazione: " + opts.DestinationFolder);
                Directory.CreateDirectory(opts.DestinationFolder);
            }

            // Risolvi pattern codec
            string[] codecPatterns = null;
            if (opts.AudioCodec.Length > 0)
            {
                codecPatterns = CodecMapping.GetCodecPatterns(opts.AudioCodec);
            }

            // Verifica mkvmerge
            MkvToolsService tempService = new MkvToolsService(opts.MkvMergePath);
            if (!tempService.VerifyMkvMerge())
            {
                ConsoleHelper.WriteRed("mkvmerge non trovato. Installa MKVToolNix o specifica -mkv");
                return 1;
            }
            ConsoleHelper.WriteGreen("Trovato mkvmerge: " + opts.MkvMergePath);

            // Inizializza servizio audio sync se auto-sync e' abilitato
            AudioSyncService syncService = null;
            if (opts.AutoSync)
            {
                FfmpegProvider ffmpegProvider = new FfmpegProvider(opts.ToolsFolder);
                if (!ffmpegProvider.Resolve())
                {
                    ConsoleHelper.WriteRed("ffmpeg non trovato e impossibile scaricarlo. Installalo manualmente.");
                    return 1;
                }
                ConsoleHelper.WriteGreen("Trovato ffmpeg: " + ffmpegProvider.FfmpegPath);
                syncService = new AudioSyncService(ffmpegProvider.FfmpegPath);
            }

            // Crea il servizio principale, le statistiche e la lista record
            MkvToolsService service = new MkvToolsService(opts.MkvMergePath);
            ProcessingStats stats = new ProcessingStats();
            List<FileProcessingRecord> records = new List<FileProcessingRecord>();

            // Stampa banner
            ConsoleHelper.WriteCyan("\n========================================");
            ConsoleHelper.WriteCyan("  MKV Language Track Merger");
            ConsoleHelper.WriteCyan("========================================\n");

            // Stampa configurazione
            PrintConfiguration(opts, codecPatterns);

            // Determina flag filtraggio tracce sorgente
            bool filterSourceAudio = (opts.KeepSourceAudioLangs.Count > 0);
            bool filterSourceSubs = (opts.KeepSourceSubtitleLangs.Count > 0);

            // Trova tutti i file sorgente
            string extList = string.Join(", ", opts.FileExtensions);
            List<string> sourceFiles = FindVideoFiles(opts.SourceFolder, opts.FileExtensions, opts.Recursive);
            ConsoleHelper.WriteGreen("Trovati " + sourceFiles.Count + " file sorgente (" + extList + ")\n");

            // Costruisci indice file lingua
            ConsoleHelper.WriteYellow("Indicizzazione cartella lingua...");
            List<string> languageFiles = FindVideoFiles(opts.LanguageFolder, opts.FileExtensions, opts.Recursive);
            Dictionary<string, string> languageIndex = new Dictionary<string, string>();

            for (int i = 0; i < languageFiles.Count; i++)
            {
                string langFileName = Path.GetFileName(languageFiles[i]);
                string langEpisodeId = GetEpisodeIdentifier(langFileName, opts.MatchPattern);
                if (langEpisodeId.Length > 0)
                {
                    languageIndex[langEpisodeId] = languageFiles[i];
                }
            }

            ConsoleHelper.WriteGreen("Indicizzati " + languageIndex.Count + " file lingua\n");

            // Elabora ogni file sorgente
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                ProcessFile(sourceFiles[i], languageIndex, opts, service, syncService, stats, records, codecPatterns, filterSourceAudio, filterSourceSubs);
            }

            // Stampa report dettagliato
            PrintDetailedReport(records, opts.DryRun);

            // Stampa riepilogo
            PrintSummary(stats, opts.AutoSync);

            return exitCode;
        }

        #endregion
    }
}
