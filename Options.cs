using System.Collections.Generic;

namespace MergeLanguageTracks
{
    public class Options
    {
        #region Proprieta

        /// <summary>
        /// Indica se e' stato richiesto il messaggio di aiuto (-h).
        /// </summary>
        public bool Help { get; set; }

        /// <summary>
        /// Percorso della cartella sorgente contenente i file MKV (-s).
        /// </summary>
        public string SourceFolder { get; set; }

        /// <summary>
        /// Percorso della cartella lingua contenente i file MKV nella lingua alternativa (-l).
        /// </summary>
        public string LanguageFolder { get; set; }

        /// <summary>
        /// Lista di codici lingua ISO 639-2 da estrarre (-t). Supporta valori multipli separati da virgola.
        /// </summary>
        public List<string> TargetLanguage { get; set; }

        /// <summary>
        /// Pattern regex per il matching degli episodi (-m). Default: S(\d+)E(\d+).
        /// </summary>
        public string MatchPattern { get; set; }

        /// <summary>
        /// Modalita' output: "Overwrite" o "Destination" (-o). Default: Destination.
        /// </summary>
        public string OutputMode { get; set; }

        /// <summary>
        /// Cartella di destinazione per l'output quando OutputMode e' Destination (-d).
        /// </summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Ritardo audio manuale in millisecondi (-ad). Sommato all'offset auto-sync se abilitato.
        /// </summary>
        public int AudioDelay { get; set; }

        /// <summary>
        /// Ritardo sottotitoli manuale in millisecondi (-sd). Sommato all'offset auto-sync se abilitato.
        /// </summary>
        public int SubtitleDelay { get; set; }

        /// <summary>
        /// Indica se la sincronizzazione automatica tramite audio fingerprint e' abilitata (-as).
        /// </summary>
        public bool AutoSync { get; set; }

        /// <summary>
        /// Stringa filtro codec audio (-ac). Solo le tracce con questo codec verranno importate.
        /// </summary>
        public string AudioCodec { get; set; }

        /// <summary>
        /// Importa solo sottotitoli, ignora tracce audio (-so).
        /// </summary>
        public bool SubOnly { get; set; }

        /// <summary>
        /// Importa solo tracce audio, ignora sottotitoli (-ao).
        /// </summary>
        public bool AudioOnly { get; set; }

        /// <summary>
        /// Lista di codici lingua da mantenere nelle tracce audio sorgente (-ksa).
        /// </summary>
        public List<string> KeepSourceAudioLangs { get; set; }

        /// <summary>
        /// Lista di codici lingua da mantenere nelle tracce sottotitoli sorgente (-kss).
        /// </summary>
        public List<string> KeepSourceSubtitleLangs { get; set; }

        /// <summary>
        /// Percorso dell'eseguibile mkvmerge (-mkv). Default: cerca nel PATH.
        /// </summary>
        public string MkvMergePath { get; set; }

        /// <summary>
        /// Cartella per i tool scaricati come ffmpeg (-tools). Default: cartella applicazione.
        /// </summary>
        public string ToolsFolder { get; set; }

        /// <summary>
        /// Indica se cercare ricorsivamente nelle sottocartelle (-r). Default: true.
        /// </summary>
        public bool Recursive { get; set; }

        /// <summary>
        /// Modalita' dry run: mostra cosa verrebbe fatto senza eseguire (-dry, -n).
        /// </summary>
        public bool DryRun { get; set; }

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public Options()
        {
            this.Help = false;
            this.SourceFolder = "";
            this.LanguageFolder = "";
            this.TargetLanguage = new List<string>();
            this.MatchPattern = @"S(\d+)E(\d+)";
            this.OutputMode = "Destination";
            this.DestinationFolder = "";
            this.AudioDelay = 0;
            this.SubtitleDelay = 0;
            this.AutoSync = false;
            this.AudioCodec = "";
            this.SubOnly = false;
            this.AudioOnly = false;
            this.KeepSourceAudioLangs = new List<string>();
            this.KeepSourceSubtitleLangs = new List<string>();
            this.MkvMergePath = "mkvmerge";
            this.ToolsFolder = "";
            this.Recursive = true;
            this.DryRun = false;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Parsa gli argomenti da riga di comando in un'istanza.
        /// </summary>
        /// <param name="args">L'array di argomenti da riga di comando.</param>
        /// <returns>Un'istanza <see cref="Options"/> popolata.</returns>
        public static Options Parse(string[] args)
        {
            Options options = new Options();
            int i = 0;

            while (i < args.Length)
            {
                // Normalizza la chiave argomento in minuscolo per il confronto
                string key = args[i].ToLower().TrimStart('-');

                // Determina se l'argomento successivo e' un valore o un altro flag
                bool hasNextValue = (i + 1 < args.Length) && !args[i + 1].StartsWith("-");

                // Gestione switch che non richiedono un valore
                if (key == "h" || key == "help" || key == "?")
                {
                    options.Help = true;
                    i++;
                    continue;
                }
                if (key == "as" || key == "autosync")
                {
                    options.AutoSync = true;
                    i++;
                    continue;
                }
                if (key == "so" || key == "subonly")
                {
                    options.SubOnly = true;
                    i++;
                    continue;
                }
                if (key == "ao" || key == "audioonly")
                {
                    options.AudioOnly = true;
                    i++;
                    continue;
                }
                if (key == "r" || key == "recursive")
                {
                    options.Recursive = true;
                    i++;
                    continue;
                }
                if (key == "dry" || key == "dryrun" || key == "n")
                {
                    options.DryRun = true;
                    i++;
                    continue;
                }

                // Gestione opzioni che richiedono un valore successivo
                if (!hasNextValue)
                {
                    // Flag sconosciuto o valore mancante, salta
                    i++;
                    continue;
                }

                string value = args[i + 1];

                if (key == "s" || key == "sourcefolder")
                {
                    options.SourceFolder = value;
                }
                else if (key == "l" || key == "languagefolder")
                {
                    options.LanguageFolder = value;
                }
                else if (key == "t" || key == "targetlanguage")
                {
                    // Supporta lingue separate da virgola
                    string[] langs = value.Split(',');
                    foreach (string lang in langs)
                    {
                        string trimmed = lang.Trim();
                        if (trimmed.Length > 0)
                        {
                            options.TargetLanguage.Add(trimmed);
                        }
                    }
                }
                else if (key == "m" || key == "matchpattern")
                {
                    options.MatchPattern = value;
                }
                else if (key == "o" || key == "outputmode")
                {
                    options.OutputMode = value;
                }
                else if (key == "d" || key == "destinationfolder")
                {
                    options.DestinationFolder = value;
                }
                else if (key == "ad" || key == "audiodelay")
                {
                    int.TryParse(value, out int delay);
                    options.AudioDelay = delay;
                }
                else if (key == "sd" || key == "subtitledelay")
                {
                    int.TryParse(value, out int delay);
                    options.SubtitleDelay = delay;
                }
                else if (key == "ac" || key == "audiocodec")
                {
                    options.AudioCodec = value;
                }
                else if (key == "ksa" || key == "keepsourceaudiolangs")
                {
                    string[] langs = value.Split(',');
                    foreach (string lang in langs)
                    {
                        string trimmed = lang.Trim();
                        if (trimmed.Length > 0)
                        {
                            options.KeepSourceAudioLangs.Add(trimmed);
                        }
                    }
                }
                else if (key == "kss" || key == "keepsourcesubtitlelangs")
                {
                    string[] langs = value.Split(',');
                    foreach (string lang in langs)
                    {
                        string trimmed = lang.Trim();
                        if (trimmed.Length > 0)
                        {
                            options.KeepSourceSubtitleLangs.Add(trimmed);
                        }
                    }
                }
                else if (key == "mkv" || key == "mkvmergepath")
                {
                    options.MkvMergePath = value;
                }
                else if (key == "tools" || key == "toolsfolder")
                {
                    options.ToolsFolder = value;
                }

                // Avanza oltre la coppia chiave-valore
                i += 2;
            }

            return options;
        }

        #endregion
    }
}
