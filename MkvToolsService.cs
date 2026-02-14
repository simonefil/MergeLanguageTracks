using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MergeLanguageTracks
{
    public class MkvToolsService
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso dell'eseguibile mkvmerge.
        /// </summary>
        private string _mkvMergePath;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="mkvMergePath">Percorso di mkvmerge.</param>
        public MkvToolsService(string mkvMergePath)
        {
            this._mkvMergePath = mkvMergePath;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Verifica che mkvmerge sia accessibile e funzionante.
        /// </summary>
        /// <returns>True se mkvmerge --version ha successo.</returns>
        public bool VerifyMkvMerge()
        {
            bool result = false;

            try
            {
                // Esegue mkvmerge --version per confermare esistenza
                string output = this.RunProcess(this._mkvMergePath, "--version");
                result = (output.Length > 0);
            }
            catch
            {
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Ottiene le informazioni sulle tracce da un file MKV tramite mkvmerge -J.
        /// </summary>
        /// <param name="filePath">Percorso del file MKV.</param>
        /// <returns>Lista di tracce, o null se la lettura fallisce.</returns>
        public List<TrackInfo> GetTrackInfo(string filePath)
        {
            List<TrackInfo> tracks = null;
            string jsonOutput = "";

            try
            {
                // Esegue mkvmerge -J per ottenere info tracce in JSON
                jsonOutput = this.RunProcess(this._mkvMergePath, "-J \"" + filePath + "\"");
            }
            catch
            {
                ConsoleHelper.WriteWarning("Impossibile leggere info tracce per: " + filePath);
                return null;
            }

            // Verifica output valido
            if (jsonOutput.Length == 0)
            {
                return null;
            }

            try
            {
                tracks = new List<TrackInfo>();

                // Parsing del documento JSON
                JsonDocument doc = JsonDocument.Parse(jsonOutput);
                JsonElement root = doc.RootElement;

                // Itera sull'array delle tracce
                if (root.TryGetProperty("tracks", out JsonElement tracksElement))
                {
                    foreach (JsonElement trackEl in tracksElement.EnumerateArray())
                    {
                        TrackInfo track = new TrackInfo();

                        // Legge ID traccia
                        if (trackEl.TryGetProperty("id", out JsonElement idEl))
                        {
                            track.Id = idEl.GetInt32();
                        }

                        // Legge tipo traccia
                        if (trackEl.TryGetProperty("type", out JsonElement typeEl))
                        {
                            track.Type = typeEl.GetString();
                        }

                        // Legge codec
                        if (trackEl.TryGetProperty("codec", out JsonElement codecEl))
                        {
                            track.Codec = codecEl.GetString();
                        }

                        // Legge sotto-oggetto properties
                        if (trackEl.TryGetProperty("properties", out JsonElement propsEl))
                        {
                            // Lingua ISO 639-2
                            if (propsEl.TryGetProperty("language", out JsonElement langEl))
                            {
                                track.Language = langEl.GetString();
                            }

                            // Tag lingua IETF
                            if (propsEl.TryGetProperty("language_ietf", out JsonElement langIetfEl))
                            {
                                string ietfVal = langIetfEl.ValueKind == JsonValueKind.Null ? "" : langIetfEl.GetString();
                                track.LanguageIetf = ietfVal;
                            }

                            // Nome traccia
                            if (propsEl.TryGetProperty("track_name", out JsonElement nameEl))
                            {
                                string nameVal = nameEl.ValueKind == JsonValueKind.Null ? "" : nameEl.GetString();
                                track.Name = nameVal;
                            }
                        }

                        tracks.Add(track);
                    }
                }

                doc.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning("Errore parsing JSON tracce: " + ex.Message);
                tracks = null;
            }

            return tracks;
        }

        /// <summary>
        /// Verifica se una traccia corrisponde al codice lingua specificato.
        /// </summary>
        /// <param name="track">La traccia da verificare.</param>
        /// <param name="language">Il codice lingua ISO 639-2.</param>
        /// <returns>True se la lingua della traccia corrisponde.</returns>
        public bool IsLanguageMatch(TrackInfo track, string language)
        {
            bool match = false;

            // Verifica lingua ISO 639-2
            if (string.Equals(track.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                match = true;
            }
            // Verifica prefisso o corrispondenza esatta tag IETF
            else if (track.LanguageIetf.Length > 0)
            {
                if (track.LanguageIetf.StartsWith(language, StringComparison.OrdinalIgnoreCase) || string.Equals(track.LanguageIetf, language, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                }
            }

            return match;
        }

        /// <summary>
        /// Verifica se una traccia corrisponde a uno qualsiasi dei codici lingua specificati.
        /// </summary>
        /// <param name="track">La traccia da verificare.</param>
        /// <param name="languages">Lista di codici lingua ISO 639-2.</param>
        /// <returns>True se la traccia corrisponde a una lingua nella lista.</returns>
        public bool IsLanguageInList(TrackInfo track, List<string> languages)
        {
            bool match = false;

            for (int i = 0; i < languages.Count; i++)
            {
                if (this.IsLanguageMatch(track, languages[i]))
                {
                    match = true;
                    break;
                }
            }

            return match;
        }

        /// <summary>
        /// Filtra le tracce dall'MKV per tipo, lingua e opzionalmente codec.
        /// </summary>
        /// <param name="allTracks">Lista completa delle tracce dall'MKV.</param>
        /// <param name="language">Codice lingua per il filtro.</param>
        /// <param name="trackType">Tipo traccia: "audio" o "subtitles".</param>
        /// <param name="codecPatterns">Array di pattern codec per filtro audio, o null.</param>
        /// <returns>Lista di tracce corrispondenti.</returns>
        public List<TrackInfo> GetFilteredTracks(List<TrackInfo> allTracks, string language, string trackType, string[] codecPatterns)
        {
            List<TrackInfo> result = new List<TrackInfo>();

            for (int i = 0; i < allTracks.Count; i++)
            {
                TrackInfo track = allTracks[i];

                // Filtra per tipo
                if (!string.Equals(track.Type, trackType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Filtra per lingua
                if (!this.IsLanguageMatch(track, language))
                {
                    continue;
                }

                // Filtra per codec per tracce audio
                if (codecPatterns != null && string.Equals(trackType, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CodecMapping.MatchesCodec(track.Codec, codecPatterns))
                    {
                        continue;
                    }
                }

                result.Add(track);
            }

            return result;
        }

        /// <summary>
        /// Ottiene gli ID traccia dal file sorgente da mantenere in base ai filtri lingua e codec
        /// </summary>
        /// <param name="allTracks">Tutte le tracce dall'MKV sorgente</param>
        /// <param name="trackType">Tipo traccia: "audio" o "subtitles"</param>
        /// <param name="keepLanguages">Lista di codici lingua da mantenere, o lista vuota per tutte</param>
        /// <param name="codecPatterns">Array di pattern codec per filtro, o null per nessun filtro codec</param>
        /// <returns>Lista di ID traccia da mantenere</returns>
        public List<int> GetSourceTrackIds(List<TrackInfo> allTracks, string trackType, List<string> keepLanguages, string[] codecPatterns)
        {
            List<int> trackIds = new List<int>();

            for (int i = 0; i < allTracks.Count; i++)
            {
                TrackInfo track = allTracks[i];

                // Salta tracce di tipo diverso
                if (!string.Equals(track.Type, trackType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Filtro lingua: se specificato, mantiene solo le lingue nella lista
                if (keepLanguages.Count > 0 && !this.IsLanguageInList(track, keepLanguages))
                {
                    continue;
                }

                // Filtro codec: se specificato, mantiene solo le tracce con codec corrispondente
                if (codecPatterns != null && !CodecMapping.MatchesCodec(track.Codec, codecPatterns))
                {
                    continue;
                }

                trackIds.Add(track.Id);
            }

            return trackIds;
        }

        /// <summary>
        /// Costruisce la lista argomenti per mkvmerge per unire tracce sorgente e lingua.
        /// Gestisce filtro tracce sorgente, selezione tracce lingua e applicazione delay.
        /// </summary>
        /// <param name="sourceFile">Percorso dell'MKV sorgente.</param>
        /// <param name="languageFile">Percorso dell'MKV lingua.</param>
        /// <param name="outputFile">Percorso dell'MKV output.</param>
        /// <param name="sourceAudioIds">ID tracce audio sorgente da mantenere.</param>
        /// <param name="sourceSubIds">ID tracce sottotitoli sorgente da mantenere.</param>
        /// <param name="langAudioTracks">Tracce audio lingua da aggiungere.</param>
        /// <param name="langSubTracks">Tracce sottotitoli lingua da aggiungere.</param>
        /// <param name="audioDelayMs">Delay per tracce audio lingua in millisecondi.</param>
        /// <param name="subDelayMs">Delay per tracce sottotitoli lingua in millisecondi.</param>
        /// <param name="filterSourceAudio">Se le tracce audio sorgente devono essere filtrate.</param>
        /// <param name="filterSourceSubs">Se le tracce sottotitoli sorgente devono essere filtrate.</param>
        /// <returns>Lista di argomenti stringa per mkvmerge.</returns>
        public List<string> BuildMergeArguments(string sourceFile, string languageFile, string outputFile, List<int> sourceAudioIds, List<int> sourceSubIds, List<TrackInfo> langAudioTracks, List<TrackInfo> langSubTracks, int audioDelayMs, int subDelayMs, bool filterSourceAudio, bool filterSourceSubs)
        {
            List<string> mkvArgs = new List<string>();

            // File output
            mkvArgs.Add("-o");
            mkvArgs.Add(outputFile);

            // Selezione tracce audio sorgente
            if (filterSourceAudio && sourceAudioIds.Count > 0)
            {
                mkvArgs.Add("--audio-tracks");
                mkvArgs.Add(JoinInts(sourceAudioIds));
            }
            else if (filterSourceAudio && sourceAudioIds.Count == 0)
            {
                // Rimuove tutto l'audio dal sorgente
                mkvArgs.Add("-A");
            }

            // Selezione tracce sottotitoli sorgente
            if (filterSourceSubs && sourceSubIds.Count > 0)
            {
                mkvArgs.Add("--subtitle-tracks");
                mkvArgs.Add(JoinInts(sourceSubIds));
            }
            else if (filterSourceSubs && sourceSubIds.Count == 0)
            {
                // Rimuove tutti i sottotitoli dal sorgente
                mkvArgs.Add("-S");
            }

            // File sorgente
            mkvArgs.Add(sourceFile);

            // File lingua: niente video
            mkvArgs.Add("-D");

            // Tracce audio lingua
            List<int> langAudioIds = new List<int>();
            for (int i = 0; i < langAudioTracks.Count; i++)
            {
                langAudioIds.Add(langAudioTracks[i].Id);
            }

            if (langAudioIds.Count > 0)
            {
                mkvArgs.Add("--audio-tracks");
                mkvArgs.Add(JoinInts(langAudioIds));

                // Applica delay audio se non zero
                if (audioDelayMs != 0)
                {
                    for (int i = 0; i < langAudioIds.Count; i++)
                    {
                        mkvArgs.Add("--sync");
                        mkvArgs.Add(langAudioIds[i].ToString() + ":" + audioDelayMs.ToString());
                    }
                }
            }
            else
            {
                // Niente audio dal file lingua
                mkvArgs.Add("-A");
            }

            // Tracce sottotitoli lingua
            List<int> langSubIds = new List<int>();
            for (int i = 0; i < langSubTracks.Count; i++)
            {
                langSubIds.Add(langSubTracks[i].Id);
            }

            if (langSubIds.Count > 0)
            {
                mkvArgs.Add("--subtitle-tracks");
                mkvArgs.Add(JoinInts(langSubIds));

                // Applica delay sottotitoli se non zero
                if (subDelayMs != 0)
                {
                    for (int i = 0; i < langSubIds.Count; i++)
                    {
                        mkvArgs.Add("--sync");
                        mkvArgs.Add(langSubIds[i].ToString() + ":" + subDelayMs.ToString());
                    }
                }
            }
            else
            {
                // Niente sottotitoli dal file lingua
                mkvArgs.Add("-S");
            }

            // Percorso file lingua
            mkvArgs.Add(languageFile);

            return mkvArgs;
        }

        /// <summary>
        /// Formatta gli argomenti merge come stringa singola per log o output dry-run.
        /// </summary>
        /// <param name="args">Lista argomenti.</param>
        /// <returns>Stringa comando formattata.</returns>
        public string FormatMergeCommand(List<string> args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this._mkvMergePath);

            for (int i = 0; i < args.Count; i++)
            {
                sb.Append(" ");

                // Quota argomenti che contengono spazi o backslash
                if (args[i].IndexOf(' ') >= 0 || args[i].IndexOf('\\') >= 0)
                {
                    sb.Append("\"" + args[i] + "\"");
                }
                else
                {
                    sb.Append(args[i]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Esegue mkvmerge con gli argomenti dati e restituisce il codice di uscita.
        /// </summary>
        /// <param name="args">Lista argomenti per mkvmerge.</param>
        /// <param name="output">Riceve l'output combinato stdout/stderr.</param>
        /// <returns>Codice di uscita del processo.</returns>
        public int ExecuteMerge(List<string> args, out string output)
        {
            int exitCode = -1;
            StringBuilder sb = new StringBuilder();

            // Costruisce la stringa argomenti
            StringBuilder argBuilder = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0)
                {
                    argBuilder.Append(" ");
                }

                // Quota argomenti con spazi
                if (args[i].IndexOf(' ') >= 0)
                {
                    argBuilder.Append("\"" + args[i] + "\"");
                }
                else
                {
                    argBuilder.Append(args[i]);
                }
            }

            Process proc = null;
            try
            {
                // Configura e avvia processo
                proc = new Process();
                proc.StartInfo.FileName = this._mkvMergePath;
                proc.StartInfo.Arguments = argBuilder.ToString();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;

                proc.Start();

                // Legge stream output
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                exitCode = proc.ExitCode;
                sb.Append(stdout);
                if (stderr.Length > 0)
                {
                    sb.Append(stderr);
                }
            }
            catch (Exception ex)
            {
                sb.Append("Eccezione durante l'esecuzione di mkvmerge: " + ex.Message);
            }
            finally
            {
                if (proc != null)
                {
                    proc.Dispose();
                    proc = null;
                }
            }

            output = sb.ToString();
            return exitCode;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Esegue un processo e cattura sia stdout che stderr come stringa singola.
        /// </summary>
        /// <param name="fileName">Eseguibile da eseguire.</param>
        /// <param name="arguments">Argomenti riga di comando.</param>
        /// <returns>Output combinato stdout e stderr.</returns>
        private string RunProcess(string fileName, string arguments)
        {
            StringBuilder sb = new StringBuilder();
            Process proc = null;

            try
            {
                // Configura processo
                proc = new Process();
                proc.StartInfo.FileName = fileName;
                proc.StartInfo.Arguments = arguments;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;

                proc.Start();

                // Legge stdout e stderr in parallelo per prevenire deadlock
                string stdout = "";
                string stderr = "";
                Thread convergence = new Thread(() => { stdout = proc.StandardOutput.ReadToEnd(); });
                convergence.Start();
                stderr = proc.StandardError.ReadToEnd();
                convergence.Join();

                proc.WaitForExit();

                // Combina output
                sb.Append(stdout);
                if (stderr.Length > 0)
                {
                    sb.Append(stderr);
                }
            }
            finally
            {
                if (proc != null)
                {
                    proc.Dispose();
                    proc = null;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Unisce una lista di interi in una stringa separata da virgole.
        /// </summary>
        /// <param name="values">Lista di interi.</param>
        /// <returns>Stringa separata da virgole.</returns>
        private static string JoinInts(List<int> values)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                sb.Append(values[i]);
            }

            return sb.ToString();
        }

        #endregion
    }
}
