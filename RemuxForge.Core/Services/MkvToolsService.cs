using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace RemuxForge.Core
{
    /// <summary>
    /// Servizio per operazioni su file MKV tramite mkvmerge
    /// </summary>
    public class MkvToolsService
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso eseguibile mkvmerge
        /// </summary>
        private string _mkvMergePath;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="mkvMergePath">Percorso eseguibile mkvmerge</param>
        public MkvToolsService(string mkvMergePath)
        {
            this._mkvMergePath = mkvMergePath;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Verifica che mkvmerge sia accessibile e funzionante
        /// </summary>
        /// <returns>True se mkvmerge e funzionante</returns>
        public bool VerifyMkvMerge()
        {
            bool result = false;

            try
            {
                // Esegue mkvmerge --version per confermare esistenza
                ProcessResult procResult = ProcessRunner.Run(this._mkvMergePath, new string[] { "--version" });
                string output = procResult.Stdout;
                result = (output.Length > 0);
            }
            catch
            {
                // mkvmerge non trovato o non eseguibile
                ConsoleHelper.Write(LogSection.Merge, LogLevel.Warning, "mkvmerge non accessibile");
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Ottiene informazioni complete su un file MKV incluso default_duration
        /// </summary>
        /// <param name="filePath">Percorso del file MKV</param>
        /// <returns>Info complete del file o null in caso di errore</returns>
        public MkvFileInfo GetFileInfo(string filePath)
        {
            MkvFileInfo result = null;
            string jsonOutput = "";
            JsonDocument doc = null;
            JsonElement root;
            JsonElement tracksElement;

            try
            {
                ProcessResult procResult = ProcessRunner.Run(this._mkvMergePath, new string[] { "-J", filePath });
                jsonOutput = procResult.Stdout;
            }
            catch
            {
                // mkvmerge non ha prodotto output valido
                ConsoleHelper.Write(LogSection.Merge, LogLevel.Warning, "Impossibile leggere info file per: " + filePath);
            }

            if (jsonOutput.Length > 0)
            {
                try
                {
                    result = new MkvFileInfo();

                    doc = JsonDocument.Parse(jsonOutput);
                    root = doc.RootElement;

                    // Parsing durata container
                    if (root.TryGetProperty("container", out JsonElement containerEl))
                    {
                        if (containerEl.TryGetProperty("properties", out JsonElement containerPropsEl))
                        {
                            if (containerPropsEl.TryGetProperty("duration", out JsonElement durationEl))
                            {
                                result.ContainerDurationNs = durationEl.GetInt64();
                            }

                            // Parsing titolo segmento
                            if (containerPropsEl.TryGetProperty("title", out JsonElement titleEl))
                            {
                                result.ContainerTitle = titleEl.GetString();
                                if (result.ContainerTitle == null) { result.ContainerTitle = ""; }
                            }
                        }
                    }

                    // Parsing tracce
                    if (root.TryGetProperty("tracks", out tracksElement))
                    {
                        foreach (JsonElement trackEl in tracksElement.EnumerateArray())
                        {
                            result.Tracks.Add(ParseTrackFromJson(trackEl));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Errore parsing JSON, info non disponibili
                    ConsoleHelper.Write(LogSection.Merge, LogLevel.Warning, "Errore parsing JSON file info: " + ex.Message);
                    result = null;
                }
                finally
                {
                    if (doc != null) { doc.Dispose(); }
                }
            }

            return result;
        }

        /// <summary>
        /// Verifica se una traccia corrisponde al codice lingua specificato
        /// </summary>
        /// <param name="track">Traccia da verificare</param>
        /// <param name="language">Codice lingua da confrontare</param>
        /// <returns>True se la traccia corrisponde alla lingua</returns>
        public bool IsLanguageMatch(TrackInfo track, string language)
        {
            bool match = false;

            // Verifica lingua ISO 639-2
            if (string.Equals(track.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                match = true;
            }
            // Verifica tag IETF
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
        /// Verifica se una traccia corrisponde a uno dei codici lingua nella lista
        /// </summary>
        /// <param name="track">Traccia da verificare</param>
        /// <param name="languages">Lista di codici lingua</param>
        /// <returns>True se la traccia corrisponde a una delle lingue</returns>
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
        /// Filtra tracce MKV per tipo, lingua e codec
        /// </summary>
        /// <param name="allTracks">Lista completa delle tracce</param>
        /// <param name="language">Codice lingua da filtrare</param>
        /// <param name="trackType">Tipo traccia (audio, subtitles, video)</param>
        /// <param name="codecPatterns">Pattern codec da confrontare</param>
        /// <returns>Lista delle tracce corrispondenti ai filtri</returns>
        public List<TrackInfo> GetFilteredTracks(List<TrackInfo> allTracks, string language, string trackType, string[] codecPatterns)
        {
            List<TrackInfo> result = new List<TrackInfo>();

            for (int i = 0; i < allTracks.Count; i++)
            {
                TrackInfo track = allTracks[i];

                if (!string.Equals(track.Type, trackType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!this.IsLanguageMatch(track, language))
                {
                    continue;
                }

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
        /// Ottiene gli ID traccia sorgente da mantenere in base a filtri lingua/codec
        /// </summary>
        /// <param name="allTracks">Lista completa delle tracce</param>
        /// <param name="trackType">Tipo traccia da filtrare</param>
        /// <param name="keepLanguages">Lingue da mantenere</param>
        /// <param name="codecPatterns">Pattern codec da confrontare</param>
        /// <returns>Lista degli ID traccia corrispondenti</returns>
        public List<int> GetSourceTrackIds(List<TrackInfo> allTracks, string trackType, List<string> keepLanguages, string[] codecPatterns)
        {
            List<int> trackIds = new List<int>();

            for (int i = 0; i < allTracks.Count; i++)
            {
                TrackInfo track = allTracks[i];

                if (!string.Equals(track.Type, trackType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (keepLanguages.Count > 0 && !this.IsLanguageInList(track, keepLanguages))
                {
                    continue;
                }

                if (codecPatterns != null && !CodecMapping.MatchesCodec(track.Codec, codecPatterns))
                {
                    continue;
                }

                trackIds.Add(track.Id);
            }

            return trackIds;
        }

        /// <summary>
        /// Costruisce gli argomenti mkvmerge per unire tracce sorgente e lingua
        /// </summary>
        /// <param name="req">Parametri per la costruzione del comando merge</param>
        /// <returns>Lista degli argomenti per mkvmerge</returns>
        public List<string> BuildMergeArguments(MergeRequest req)
        {
            List<string> mkvArgs = new List<string>();
            List<int> sourceAudioKeep = new List<int>();
            List<int> langAudioKeep = new List<int>();
            List<int> langSubIds = new List<int>();
            string syncValue = "";
            bool hasConvertedSource = (req.ConvertedSourceTracks != null && req.ConvertedSourceTracks.Count > 0);
            bool hasConvertedLang = (req.ConvertedLangTracks != null && req.ConvertedLangTracks.Count > 0);
            bool hasProcessedSubs = (req.ProcessedLangSubTracks != null && req.ProcessedLangSubTracks.Count > 0);

            // File output
            mkvArgs.Add("-o");
            mkvArgs.Add(req.OutputFile);

            // Imposta titolo segmento dal file sorgente (previene copia dal file lingua)
            mkvArgs.Add("--title");
            mkvArgs.Add(req.SourceTitle);

            // Separa tracce audio sorgente: non convertite (dal file) vs convertite (file separati)
            if (req.FilterSourceAudio)
            {
                for (int i = 0; i < req.SourceAudioIds.Count; i++)
                {
                    // Se la traccia e' stata convertita, non includerla dal file sorgente
                    if (hasConvertedSource && req.ConvertedSourceTracks.ContainsKey(req.SourceAudioIds[i]))
                    {
                        continue;
                    }
                    sourceAudioKeep.Add(req.SourceAudioIds[i]);
                }

                if (sourceAudioKeep.Count > 0)
                {
                    mkvArgs.Add("--audio-tracks");
                    mkvArgs.Add(JoinInts(sourceAudioKeep));
                }
                else if (!hasConvertedSource)
                {
                    // Nessuna traccia audio da sorgente e nessuna convertita
                    mkvArgs.Add("-A");
                }
                else
                {
                    // Tutte convertite, nessuna audio dal file sorgente
                    mkvArgs.Add("-A");
                }
            }

            // Rinomina tracce audio sorgente non convertite (se flag attivo)
            if (req.RenameAllTracks)
            {
                if (sourceAudioKeep.Count > 0)
                {
                    // Filtro attivo: rinomina solo le tracce selezionate
                    for (int i = 0; i < sourceAudioKeep.Count; i++)
                    {
                        TrackInfo srcTrack = FindTrackById(req.SourceAudioTracks, sourceAudioKeep[i]);
                        if (srcTrack != null)
                        {
                            string trackName = BuildOriginalTrackName(srcTrack);
                            if (trackName.Length > 0)
                            {
                                mkvArgs.Add("--track-name");
                                mkvArgs.Add(sourceAudioKeep[i] + ":" + trackName);
                            }
                        }
                    }
                }
                else if (req.SourceAudioTracks != null)
                {
                    // Nessun filtro: rinomina tutte le tracce audio source
                    for (int i = 0; i < req.SourceAudioTracks.Count; i++)
                    {
                        string trackName = BuildOriginalTrackName(req.SourceAudioTracks[i]);
                        if (trackName.Length > 0)
                        {
                            mkvArgs.Add("--track-name");
                            mkvArgs.Add(req.SourceAudioTracks[i].Id + ":" + trackName);
                        }
                    }
                }
            }

            // Selezione tracce sottotitoli sorgente
            if (req.FilterSourceSubs && req.SourceSubIds.Count > 0)
            {
                mkvArgs.Add("--subtitle-tracks");
                mkvArgs.Add(JoinInts(req.SourceSubIds));
            }
            else if (req.FilterSourceSubs && req.SourceSubIds.Count == 0)
            {
                mkvArgs.Add("-S");
            }

            // File sorgente
            mkvArgs.Add(req.SourceFile);

            // File convertiti da sorgente: aggiunti come input separati (solo audio, no video/sub)
            if (hasConvertedSource)
            {
                for (int i = 0; i < req.SourceAudioIds.Count; i++)
                {
                    int srcId = req.SourceAudioIds[i];
                    if (req.ConvertedSourceTracks.ContainsKey(srcId))
                    {
                        // Recupera TrackInfo originale per lingua e info audio
                        TrackInfo origTrack = FindTrackById(req.SourceAudioTracks, srcId);

                        // File convertito: no video, no sottotitoli
                        mkvArgs.Add("-D");
                        mkvArgs.Add("-S");

                        // Imposta lingua e titolo sulla traccia convertita (trackId 0 nel file standalone)
                        if (origTrack != null)
                        {
                            AddTrackMetadata(mkvArgs, origTrack, req.ConvertFormat, req.RenameAllTracks);
                        }

                        mkvArgs.Add(req.ConvertedSourceTracks[srcId]);
                    }
                }
            }

            // Sezione file lingua (solo se LanguageFile specificato)
            if (req.LanguageFile != null && req.LanguageFile.Length > 0)
            {
                // File lingua: niente video
                mkvArgs.Add("-D");

                // Separa tracce audio lingua: non convertite vs convertite
                for (int i = 0; i < req.LangAudioTracks.Count; i++)
                {
                    int langId = req.LangAudioTracks[i].Id;

                    if (hasConvertedLang && req.ConvertedLangTracks.ContainsKey(langId))
                    {
                        continue;
                    }
                    langAudioKeep.Add(langId);
                }

                if (langAudioKeep.Count > 0)
                {
                    mkvArgs.Add("--audio-tracks");
                    mkvArgs.Add(JoinInts(langAudioKeep));

                    // Applica delay e/o stretch alle tracce non convertite
                    if (req.AudioDelayMs != 0 || req.StretchFactor.Length > 0)
                    {
                        for (int i = 0; i < langAudioKeep.Count; i++)
                        {
                            syncValue = langAudioKeep[i].ToString() + ":" + req.AudioDelayMs.ToString();
                            if (req.StretchFactor.Length > 0)
                            {
                                syncValue = syncValue + "," + req.StretchFactor;
                            }
                            mkvArgs.Add("--sync");
                            mkvArgs.Add(syncValue);
                        }
                    }

                    // Rinomina tracce audio lingua non convertite (se flag attivo)
                    if (req.RenameAllTracks)
                    {
                        for (int i = 0; i < langAudioKeep.Count; i++)
                        {
                            TrackInfo langTrack = FindTrackById(req.LangAudioTracks, langAudioKeep[i]);
                            if (langTrack != null)
                            {
                                string trackName = BuildOriginalTrackName(langTrack);
                                if (trackName.Length > 0)
                                {
                                    mkvArgs.Add("--track-name");
                                    mkvArgs.Add(langAudioKeep[i] + ":" + trackName);
                                }
                            }
                        }
                    }
                }
                else if (!hasConvertedLang)
                {
                    mkvArgs.Add("-A");
                }
                else
                {
                    // Tutte convertite, nessuna audio dal file lingua
                    mkvArgs.Add("-A");
                }

                // Tracce sottotitoli lingua (esclude quelle pre-processate da deep analysis)
                for (int i = 0; i < req.LangSubTracks.Count; i++)
                {
                    if (hasProcessedSubs && req.ProcessedLangSubTracks.ContainsKey(req.LangSubTracks[i].Id))
                    {
                        continue;
                    }
                    langSubIds.Add(req.LangSubTracks[i].Id);
                }

                if (langSubIds.Count > 0)
                {
                    mkvArgs.Add("--subtitle-tracks");
                    mkvArgs.Add(JoinInts(langSubIds));

                    // Applica delay e/o stretch ai sottotitoli
                    if (req.SubDelayMs != 0 || req.StretchFactor.Length > 0)
                    {
                        for (int i = 0; i < langSubIds.Count; i++)
                        {
                            syncValue = langSubIds[i].ToString() + ":" + req.SubDelayMs.ToString();
                            if (req.StretchFactor.Length > 0)
                            {
                                syncValue = syncValue + "," + req.StretchFactor;
                            }
                            mkvArgs.Add("--sync");
                            mkvArgs.Add(syncValue);
                        }
                    }
                }
                else
                {
                    mkvArgs.Add("-S");
                }

                // Percorso file lingua (senza chapters per evitare duplicati con quelli del source)
                mkvArgs.Add("--no-chapters");
                mkvArgs.Add(req.LanguageFile);

                // File convertiti da lingua: aggiunti come input separati con delay/stretch
                if (hasConvertedLang)
                {
                    for (int i = 0; i < req.LangAudioTracks.Count; i++)
                    {
                        int langId = req.LangAudioTracks[i].Id;
                        if (req.ConvertedLangTracks.ContainsKey(langId))
                        {
                            // File convertito: no video, no sottotitoli
                            mkvArgs.Add("-D");
                            mkvArgs.Add("-S");

                            // Applica delay e/o stretch (trackId 0 nel file convertito)
                            if (req.AudioDelayMs != 0 || req.StretchFactor.Length > 0)
                            {
                                syncValue = "0:" + req.AudioDelayMs.ToString();
                                if (req.StretchFactor.Length > 0)
                                {
                                    syncValue = syncValue + "," + req.StretchFactor;
                                }
                                mkvArgs.Add("--sync");
                                mkvArgs.Add(syncValue);
                            }

                            // Imposta lingua e titolo sulla traccia (trackId 0 nel file standalone)
                            // Usa ConvertFormat solo se la traccia e' stata effettivamente convertita di codec
                            TrackInfo origLangTrack = FindTrackById(req.LangAudioTracks, langId);
                            if (origLangTrack != null)
                            {
                                string langConvertFmt = req.CodecConvertedLangIds.Contains(langId) ? req.ConvertFormat : "";
                                AddTrackMetadata(mkvArgs, origLangTrack, langConvertFmt, req.RenameAllTracks);
                            }

                            mkvArgs.Add(req.ConvertedLangTracks[langId]);
                        }
                    }
                }

                // Sottotitoli pre-processati da deep analysis: aggiunti come input separati con delay
                if (hasProcessedSubs)
                {
                    for (int i = 0; i < req.LangSubTracks.Count; i++)
                    {
                        int subId = req.LangSubTracks[i].Id;
                        if (req.ProcessedLangSubTracks.ContainsKey(subId))
                        {
                            // File processato: no video, no audio
                            mkvArgs.Add("-D");
                            mkvArgs.Add("-A");

                            // Applica delay iniziale (trackId 0 nel file processato)
                            if (req.SubDelayMs != 0)
                            {
                                mkvArgs.Add("--sync");
                                mkvArgs.Add("0:" + req.SubDelayMs.ToString());
                            }

                            // Lingua del sottotitolo originale
                            mkvArgs.Add("--language");
                            mkvArgs.Add("0:" + req.LangSubTracks[i].Language);

                            mkvArgs.Add(req.ProcessedLangSubTracks[subId]);
                        }
                    }
                }
            }

            return mkvArgs;
        }

        /// <summary>
        /// Formatta argomenti merge come stringa per log
        /// </summary>
        /// <param name="args">Lista degli argomenti mkvmerge</param>
        /// <returns>Stringa formattata del comando completo</returns>
        public string FormatMergeCommand(List<string> args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this._mkvMergePath);

            for (int i = 0; i < args.Count; i++)
            {
                sb.Append(" ");

                // Quota argomenti con spazi o backslash
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
        /// Esegue mkvmerge con gli argomenti dati
        /// </summary>
        /// <param name="args">Lista degli argomenti mkvmerge</param>
        /// <param name="output">Output combinato stdout+stderr</param>
        /// <returns>Codice di uscita del processo</returns>
        public int ExecuteMerge(List<string> args, out string output)
        {
            StringBuilder sb = new StringBuilder();
            ProcessResult result = ProcessRunner.Run(this._mkvMergePath, args.ToArray());

            sb.Append(result.Stdout);
            if (result.Stderr.Length > 0)
            {
                sb.Append(result.Stderr);
            }

            output = sb.ToString();

            return result.ExitCode;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Parsa una singola traccia dal JSON mkvmerge
        /// </summary>
        /// <param name="trackEl">Elemento JSON della traccia</param>
        /// <returns>TrackInfo popolato</returns>
        private static TrackInfo ParseTrackFromJson(JsonElement trackEl)
        {
            TrackInfo track = new TrackInfo();

            if (trackEl.TryGetProperty("id", out JsonElement idEl))
            {
                track.Id = idEl.GetInt32();
            }

            if (trackEl.TryGetProperty("type", out JsonElement typeEl))
            {
                track.Type = typeEl.GetString();
            }

            if (trackEl.TryGetProperty("codec", out JsonElement codecEl))
            {
                track.Codec = codecEl.GetString();
            }

            if (trackEl.TryGetProperty("properties", out JsonElement propsEl))
            {
                if (propsEl.TryGetProperty("language", out JsonElement langEl))
                {
                    track.Language = langEl.GetString();
                }

                if (propsEl.TryGetProperty("language_ietf", out JsonElement langIetfEl))
                {
                    string ietfVal = langIetfEl.ValueKind == JsonValueKind.Null ? "" : langIetfEl.GetString();
                    track.LanguageIetf = ietfVal;
                }

                if (propsEl.TryGetProperty("track_name", out JsonElement nameEl))
                {
                    string nameVal = nameEl.ValueKind == JsonValueKind.Null ? "" : nameEl.GetString();
                    track.Name = nameVal;
                }

                if (propsEl.TryGetProperty("default_duration", out JsonElement defDurEl))
                {
                    track.DefaultDurationNs = defDurEl.GetInt64();
                }

                if (propsEl.TryGetProperty("audio_channels", out JsonElement chEl))
                {
                    track.Channels = chEl.GetInt32();
                }

                if (propsEl.TryGetProperty("audio_bits_per_sample", out JsonElement bpsEl))
                {
                    track.BitsPerSample = bpsEl.GetInt32();
                }

                if (propsEl.TryGetProperty("audio_sampling_frequency", out JsonElement freqEl))
                {
                    track.SamplingFrequency = freqEl.GetInt32();
                }
            }

            return track;
        }

        /// <summary>
        /// Cerca una traccia per ID in una lista di TrackInfo
        /// </summary>
        /// <param name="tracks">Lista di tracce</param>
        /// <param name="trackId">ID traccia da cercare</param>
        /// <returns>TrackInfo corrispondente o null se non trovata</returns>
        private static TrackInfo FindTrackById(List<TrackInfo> tracks, int trackId)
        {
            TrackInfo result = null;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id == trackId)
                {
                    result = tracks[i];
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Aggiunge argomenti --language e --track-name per una traccia standalone (trackId 0)
        /// </summary>
        /// <param name="mkvArgs">Lista argomenti mkvmerge in costruzione</param>
        /// <param name="origTrack">Traccia originale con metadati lingua e audio</param>
        /// <param name="convertFormat">Formato conversione (flac, opus), vuoto se non convertita</param>
        /// <param name="renameAllTracks">Se rinominare anche tracce non convertite</param>
        private static void AddTrackMetadata(List<string> mkvArgs, TrackInfo origTrack, string convertFormat, bool renameAllTracks)
        {
            // Lingua: usa IETF se disponibile, altrimenti ISO 639-2
            if (origTrack.LanguageIetf.Length > 0)
            {
                mkvArgs.Add("--language");
                mkvArgs.Add("0:" + origTrack.LanguageIetf);
            }
            else if (origTrack.Language.Length > 0)
            {
                mkvArgs.Add("--language");
                mkvArgs.Add("0:" + origTrack.Language);
            }

            // Titolo: sempre per convertite, solo se flag attivo per non convertite
            string trackName = "";
            if (convertFormat.Length > 0)
            {
                trackName = BuildConvertedTrackName(origTrack, convertFormat);
            }
            else if (renameAllTracks)
            {
                trackName = BuildOriginalTrackName(origTrack);
            }

            if (trackName.Length > 0)
            {
                mkvArgs.Add("--track-name");
                mkvArgs.Add("0:" + trackName);
            }
        }

        /// <summary>
        /// Genera il nome traccia per una traccia audio non convertita (codec originale)
        /// Formato: "AC-3 5.1 48kHz" o "DTS 5.1 24bit/48kHz"
        /// </summary>
        /// <param name="track">Traccia con info codec, canali, bit depth, sample rate</param>
        /// <returns>Nome traccia generato o stringa vuota se codec non disponibile</returns>
        private static string BuildOriginalTrackName(TrackInfo track)
        {
            string result = "";

            if (track.Codec.Length == 0) { return result; }

            StringBuilder sb = new StringBuilder();
            string channelLayout = FormatChannelLayout(track.Channels);
            int sampleRateKhz = track.SamplingFrequency / 1000;

            // Codec originale
            sb.Append(track.Codec);

            if (channelLayout.Length > 0)
            {
                sb.Append(" " + channelLayout);
            }

            // Bit depth e sample rate (stesso formato delle tracce convertite)
            if (track.BitsPerSample > 0 && sampleRateKhz > 0)
            {
                sb.Append(" " + track.BitsPerSample + "bit/" + sampleRateKhz + "kHz");
            }
            else if (track.BitsPerSample > 0)
            {
                sb.Append(" " + track.BitsPerSample + "bit");
            }
            else if (sampleRateKhz > 0)
            {
                sb.Append(" " + sampleRateKhz + "kHz");
            }

            result = sb.ToString();

            return result;
        }

        /// <summary>
        /// Genera il nome traccia per una traccia audio convertita
        /// FLAC: "FLAC 5.1 24bit/48kHz"
        /// Opus: "Opus 5.1 48kHz 256kbps"
        /// </summary>
        /// <param name="origTrack">Traccia originale con info canali, bit depth, sample rate</param>
        /// <param name="convertFormat">Formato conversione (flac, opus)</param>
        /// <returns>Nome traccia generato</returns>
        private static string BuildConvertedTrackName(TrackInfo origTrack, string convertFormat)
        {
            StringBuilder sb = new StringBuilder();
            string channelLayout = FormatChannelLayout(origTrack.Channels);
            int sampleRateKhz = origTrack.SamplingFrequency / 1000;

            if (string.Equals(convertFormat, "flac", StringComparison.OrdinalIgnoreCase))
            {
                // Formato: FLAC 5.1 24bit/48kHz
                sb.Append("FLAC");

                if (channelLayout.Length > 0)
                {
                    sb.Append(" " + channelLayout);
                }

                if (origTrack.BitsPerSample > 0 && sampleRateKhz > 0)
                {
                    sb.Append(" " + origTrack.BitsPerSample + "bit/" + sampleRateKhz + "kHz");
                }
                else if (origTrack.BitsPerSample > 0)
                {
                    sb.Append(" " + origTrack.BitsPerSample + "bit");
                }
                else if (sampleRateKhz > 0)
                {
                    sb.Append(" " + sampleRateKhz + "kHz");
                }
            }
            else if (string.Equals(convertFormat, "opus", StringComparison.OrdinalIgnoreCase))
            {
                // Formato: Opus 5.1 48kHz 256kbps
                sb.Append("Opus");

                if (channelLayout.Length > 0)
                {
                    sb.Append(" " + channelLayout);
                }

                if (sampleRateKhz > 0)
                {
                    sb.Append(" " + sampleRateKhz + "kHz");
                }

                // Bitrate effettivo da impostazioni
                int bitrate = AppSettingsService.Instance.GetOpusBitrateForChannels(origTrack.Channels);
                sb.Append(" " + bitrate + "kbps");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formatta il numero di canali nel layout standard (1.0, 2.0, 5.1, 7.1)
        /// </summary>
        /// <param name="channels">Numero canali audio</param>
        /// <returns>Stringa layout canali o stringa vuota se canali non validi</returns>
        private static string FormatChannelLayout(int channels)
        {
            string result = "";

            if (channels == 1) { result = "1.0"; }
            else if (channels == 2) { result = "2.0"; }
            else if (channels == 6) { result = "5.1"; }
            else if (channels == 8) { result = "7.1"; }
            else if (channels > 0) { result = channels + ".0"; }

            return result;
        }

        /// <summary>
        /// Unisce una lista di interi in stringa separata da virgole
        /// </summary>
        /// <param name="values">Lista di interi da unire</param>
        /// <returns>Stringa con valori separati da virgole</returns>
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
