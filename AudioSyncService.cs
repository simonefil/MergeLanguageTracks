using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MergeLanguageTracks
{
    public class AudioSyncService
    {
        #region Variabili di classe

        /// <summary>
        /// Percorso dell'eseguibile ffmpeg.
        /// </summary>
        private string _ffmpegPath;

        /// <summary>
        /// Risultato offset fase grossolana in millisecondi.
        /// </summary>
        private int _coarseOffset;

        /// <summary>
        /// Punteggio conteggio match fase grossolana.
        /// </summary>
        private int _coarseScore;

        /// <summary>
        /// Risultato offset fase fine in millisecondi.
        /// </summary>
        private int _fineOffset;

        /// <summary>
        /// Punteggio pesato fase fine.
        /// </summary>
        private double _fineScore;

        /// <summary>
        /// Risultato offset fase ultra-fine in millisecondi.
        /// </summary>
        private int _ultraFineOffset;

        /// <summary>
        /// Punteggio pesato fase ultra-fine.
        /// </summary>
        private double _ultraFineScore;

        #endregion

        #region Regex statiche pre-compilate

        /// <summary>
        /// Regex pre-compilata per marker silence_start.
        /// </summary>
        private static readonly Regex s_silenceStartRegex = new Regex(@"silence_start:\s*([\d.]+)", RegexOptions.Compiled);

        /// <summary>
        /// Regex pre-compilata per marker silence_end.
        /// </summary>
        private static readonly Regex s_silenceEndRegex = new Regex(@"silence_end:\s*([\d.]+)", RegexOptions.Compiled);

        /// <summary>
        /// Regex pre-compilata per estrazione livelli RMS dall'output astats.
        /// </summary>
        private static readonly Regex s_rmsRegex = new Regex(@"pts_time:([\d.]+).*?lavfi\.astats\.Overall\.RMS_level=(-?[\d.]+)", RegexOptions.Compiled);

        #endregion

        #region Proprieta

        /// <summary>
        /// Ottiene l'offset fase grossolana in millisecondi.
        /// </summary>
        public int CoarseOffset { get { return this._coarseOffset; } }

        /// <summary>
        /// Ottiene il conteggio match fase grossolana.
        /// </summary>
        public int CoarseScore { get { return this._coarseScore; } }

        /// <summary>
        /// Ottiene l'offset fase fine in millisecondi.
        /// </summary>
        public int FineOffset { get { return this._fineOffset; } }

        /// <summary>
        /// Ottiene il punteggio pesato fase fine.
        /// </summary>
        public double FineScore { get { return this._fineScore; } }

        /// <summary>
        /// Ottiene l'offset fase ultra-fine in millisecondi.
        /// </summary>
        public int UltraFineOffset { get { return this._ultraFineOffset; } }

        /// <summary>
        /// Ottiene il punteggio pesato fase ultra-fine.
        /// </summary>
        public double UltraFineScore { get { return this._ultraFineScore; } }

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        /// <param name="ffmpegPath">Percorso dell'eseguibile ffmpeg.</param>
        public AudioSyncService(string ffmpegPath)
        {
            this._ffmpegPath = ffmpegPath;
            this._coarseOffset = 0;
            this._coarseScore = 0;
            this._fineOffset = 0;
            this._fineScore = 0.0;
            this._ultraFineOffset = 0;
            this._ultraFineScore = 0.0;
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Calcola l'offset auto-sync tra un video sorgente e un file lingua.
        /// </summary>
        /// <param name="sourceVideo">Percorso del file MKV sorgente.</param>
        /// <param name="languageFile">Percorso del file MKV lingua.</param>
        /// <param name="sourceTracks">Lista tracce dall'MKV sorgente.</param>
        /// <param name="targetLanguages">Codici lingua target in importazione.</param>
        /// <param name="isLanguageInList">Funzione per verificare se una traccia corrisponde a lingue nella lista.</param>
        /// <returns>Offset calcolato in millisecondi, o int.MinValue in caso di fallimento.</returns>
        public int ComputeAutoSyncOffset(string sourceVideo, string languageFile, List<TrackInfo> sourceTracks, List<string> targetLanguages, Func<TrackInfo, List<string>, bool> isLanguageInList)
        {
            int resultOffset = int.MinValue;

            // Determina quale traccia audio sorgente usare come riferimento sync
            int syncTrackIndex = -1;
            List<TrackInfo> audioTracks = new List<TrackInfo>();

            // Raccoglie tutte le tracce audio
            for (int i = 0; i < sourceTracks.Count; i++)
            {
                if (string.Equals(sourceTracks[i].Type, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioTracks.Add(sourceTracks[i]);
                }
            }

            // Seleziona traccia di riferimento evitando la lingua target
            if (audioTracks.Count > 0)
            {
                TrackInfo defaultTrack = audioTracks[0];
                bool defaultIsTarget = isLanguageInList(defaultTrack, targetLanguages);

                if (defaultIsTarget)
                {
                    // Cerca una traccia che NON sia nella lingua target
                    for (int i = 0; i < audioTracks.Count; i++)
                    {
                        if (!isLanguageInList(audioTracks[i], targetLanguages))
                        {
                            syncTrackIndex = audioTracks[i].Id;
                            ConsoleHelper.WriteDarkYellow("  [AUTO-SYNC] Traccia default in lingua " + string.Join(",", targetLanguages) + ", uso traccia " + syncTrackIndex + " (" + audioTracks[i].Language + ") come riferimento");
                            break;
                        }
                    }

                    // Avviso se tutte le tracce sono nella lingua target
                    if (syncTrackIndex < 0)
                    {
                        ConsoleHelper.WriteWarning("  [AUTO-SYNC] Tutte le tracce audio sono in lingua " + string.Join(",", targetLanguages) + ", sync potrebbe non essere affidabile");
                    }
                }
            }

            ConsoleHelper.WriteDarkGray("  Estrazione e analisi audio via pipe (5 min, 8kHz mono)...");

            // Approccio pipe: producer estrae a PCM raw, consumer analizza
            string analysisFilters = "silencedetect=noise=-35dB:d=0.3,astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-";

            // Costruzione argomenti producer (estrazione audio a PCM raw 8kHz mono)
            // -nostdin impedisce a ffmpeg di aprire /dev/tty per input tastiera,
            // evitando corruzione delle impostazioni terminale su Linux
            string sourceProducerArgs = "";
            if (syncTrackIndex >= 0)
            {
                // Usa traccia specifica come riferimento
                sourceProducerArgs = "-nostdin -hide_banner -hwaccel auto -threads 0 -i \"" + sourceVideo + "\" -map 0:" + syncTrackIndex + " -t 300 -ac 1 -ar 8000 -f s16le -";
            }
            else
            {
                // Usa prima traccia audio disponibile
                sourceProducerArgs = "-nostdin -hide_banner -hwaccel auto -threads 0 -i \"" + sourceVideo + "\" -vn -t 300 -ac 1 -ar 8000 -f s16le -";
            }
            string langProducerArgs = "-nostdin -hide_banner -hwaccel auto -threads 0 -i \"" + languageFile + "\" -vn -t 300 -ac 1 -ar 8000 -f s16le -";

            // Argomenti consumer (analisi da input PCM raw)
            string consumerArgs = "-nostdin -hide_banner -threads 0 -f s16le -ar 8000 -ac 1 -i - -af \"" + analysisFilters + "\" -f null -";

            // Esegue entrambe le analisi pipe in parallelo
            string sourceOutput = "";
            string langOutput = "";
            string sourceProducerArgsCopy = sourceProducerArgs;
            string langProducerArgsCopy = langProducerArgs;
            string consumerArgsCopy = consumerArgs;

            // Thread per analisi sorgente
            Thread sourceThread = new Thread(() => { sourceOutput = this.RunPipedProcess(this._ffmpegPath, sourceProducerArgsCopy, this._ffmpegPath, consumerArgsCopy); });

            // Thread per analisi lingua
            Thread langThread = new Thread(() => { langOutput = this.RunPipedProcess(this._ffmpegPath, langProducerArgsCopy, this._ffmpegPath, consumerArgsCopy); });

            // Avvia e attende completamento
            sourceThread.Start();
            langThread.Start();
            sourceThread.Join();
            langThread.Join();

            // Verifica output valido
            if (sourceOutput.Length == 0 || langOutput.Length == 0)
            {
                ConsoleHelper.WriteWarning("  Impossibile analizzare audio");
                return resultOffset;
            }

            ConsoleHelper.WriteDarkGray("  Analisi pattern audio...");

            // Parsing marker usando regex statiche pre-compilate
            List<double> sourceMarkers = new List<double>();
            List<double> langMarkers = new List<double>();
            int sourceSilenceStartCount = 0;
            int sourceSilenceEndCount = 0;
            int langSilenceStartCount = 0;
            int langSilenceEndCount = 0;
            int sourceTransientCount = 0;
            int langTransientCount = 0;

            // Parsing marker silenzi sorgente
            MatchCollection sourceStartMatches = s_silenceStartRegex.Matches(sourceOutput);
            for (int i = 0; i < sourceStartMatches.Count; i++)
            {
                double time = double.Parse(sourceStartMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                sourceMarkers.Add(time);
                sourceSilenceStartCount++;
            }

            MatchCollection sourceEndMatches = s_silenceEndRegex.Matches(sourceOutput);
            for (int i = 0; i < sourceEndMatches.Count; i++)
            {
                double time = double.Parse(sourceEndMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                sourceMarkers.Add(time);
                sourceSilenceEndCount++;
            }

            // Parsing marker silenzi lingua
            MatchCollection langStartMatches = s_silenceStartRegex.Matches(langOutput);
            for (int i = 0; i < langStartMatches.Count; i++)
            {
                double time = double.Parse(langStartMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                langMarkers.Add(time);
                langSilenceStartCount++;
            }

            MatchCollection langEndMatches = s_silenceEndRegex.Matches(langOutput);
            for (int i = 0; i < langEndMatches.Count; i++)
            {
                double time = double.Parse(langEndMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                langMarkers.Add(time);
                langSilenceEndCount++;
            }

            // Parsing livelli RMS sorgente e rilevamento transienti
            string sourceFlat = sourceOutput.Replace("\n", " ").Replace("\r", " ");
            List<double> sourceRmsTimes = new List<double>();
            List<double> sourceRmsLevels = new List<double>();

            MatchCollection sourceRmsMatches = s_rmsRegex.Matches(sourceFlat);
            for (int i = 0; i < sourceRmsMatches.Count; i++)
            {
                sourceRmsTimes.Add(double.Parse(sourceRmsMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                sourceRmsLevels.Add(double.Parse(sourceRmsMatches[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            // Rilevamento transienti sorgente (incrementi > 6dB)
            for (int i = 1; i < sourceRmsLevels.Count; i++)
            {
                double prev = sourceRmsLevels[i - 1];
                double curr = sourceRmsLevels[i];

                // Clamp valori molto bassi
                if (prev < -50.0) { prev = -50.0; }
                if (curr < -50.0) { curr = -50.0; }

                // Rileva transiente se incremento > 6dB
                if ((curr - prev) > 6.0)
                {
                    sourceMarkers.Add(sourceRmsTimes[i]);
                    sourceTransientCount++;
                }
            }

            // Parsing livelli RMS lingua e rilevamento transienti
            string langFlat = langOutput.Replace("\n", " ").Replace("\r", " ");
            List<double> langRmsTimes = new List<double>();
            List<double> langRmsLevels = new List<double>();

            MatchCollection langRmsMatches = s_rmsRegex.Matches(langFlat);
            for (int i = 0; i < langRmsMatches.Count; i++)
            {
                langRmsTimes.Add(double.Parse(langRmsMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                langRmsLevels.Add(double.Parse(langRmsMatches[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            // Rilevamento transienti lingua (incrementi > 6dB)
            for (int i = 1; i < langRmsLevels.Count; i++)
            {
                double prev = langRmsLevels[i - 1];
                double curr = langRmsLevels[i];

                // Clamp valori molto bassi
                if (prev < -50.0) { prev = -50.0; }
                if (curr < -50.0) { curr = -50.0; }

                // Rileva transiente se incremento > 6dB
                if ((curr - prev) > 6.0)
                {
                    langMarkers.Add(langRmsTimes[i]);
                    langTransientCount++;
                }
            }

            // Log statistiche marker
            ConsoleHelper.WriteDarkGray("  Source: " + sourceSilenceStartCount + " silence_start, " + sourceSilenceEndCount + " silence_end, " + sourceTransientCount + " transients");
            ConsoleHelper.WriteDarkGray("  Language: " + langSilenceStartCount + " silence_start, " + langSilenceEndCount + " silence_end, " + langTransientCount + " transients");
            ConsoleHelper.WriteDarkGray("  Totale marker: " + sourceMarkers.Count + " source, " + langMarkers.Count + " language");

            // Verifica marker sufficienti
            if (sourceMarkers.Count < 5 || langMarkers.Count < 5)
            {
                ConsoleHelper.WriteWarning("  Marker audio insufficienti per sync affidabile");
                return resultOffset;
            }

            // Esegue ricerca offset a 3 fasi
            ConsoleHelper.WriteDarkGray("  Fase 1: Ricerca grossolana...");

            double[] sourceTimesArray = sourceMarkers.ToArray();
            double[] langTimesArray = langMarkers.ToArray();
            this.FindBestOffset(sourceTimesArray, langTimesArray);

            // Log risultati fasi
            ConsoleHelper.WriteDarkGray("  Risultato grossolano: " + this._coarseOffset + "ms (" + this._coarseScore + " match)");
            ConsoleHelper.WriteDarkGray("  Fase 2: Ricerca fine...");
            ConsoleHelper.WriteDarkGray("  Risultato fine: " + this._fineOffset + "ms (score: " + Math.Round(this._fineScore, 2) + ")");
            ConsoleHelper.WriteDarkGray("  Fase 3: Ricerca ultra-fine...");
            ConsoleHelper.WriteDarkGray("  Risultato ultra-fine: " + this._ultraFineOffset + "ms (score: " + Math.Round(this._ultraFineScore, 2) + ")");

            // Avviso per bassa confidenza
            if (this._coarseScore < 3)
            {
                ConsoleHelper.WriteWarning("  Sync a bassa confidenza (solo " + this._coarseScore + " match grossolani)");
            }

            resultOffset = this._ultraFineOffset;

            return resultOffset;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Esegue la ricerca offset a 3 fasi per trovare il miglior offset di sincronizzazione.
        /// I tempi sorgente e lingua sono array di timestamp marcatori in secondi,
        /// derivati dalla rilevazione silenzi e transienti tramite ffmpeg.
        /// </summary>
        /// <param name="sourceTimes">Array di timestamp marcatori dal file sorgente, in secondi.</param>
        /// <param name="langTimes">Array di timestamp marcatori dal file lingua, in secondi.</param>
        private void FindBestOffset(double[] sourceTimes, double[] langTimes)
        {
            // Fase 1: Ricerca grossolana da -60s a +60s in passi da 500ms
            this._coarseOffset = 0;
            this._coarseScore = 0;

            for (int t = -60000; t <= 60000; t += 500)
            {
                int score = 0;
                double os = t / 1000.0;

                // Conta coppie marcatori entro tolleranza 500ms
                for (int i = 0; i < sourceTimes.Length; i++)
                {
                    for (int j = 0; j < langTimes.Length; j++)
                    {
                        if (Math.Abs((langTimes[j] + os) - sourceTimes[i]) < 0.5)
                        {
                            score++;
                        }
                    }
                }

                // Aggiorna miglior offset grossolano se migliorato
                if (score > this._coarseScore)
                {
                    this._coarseScore = score;
                    this._coarseOffset = t;
                }
            }

            // Fase 2: Ricerca fine attorno al risultato grossolano, +/- 2000ms in passi da 10ms
            this._fineOffset = this._coarseOffset;
            this._fineScore = 0.0;

            for (int t = this._coarseOffset - 2000; t <= this._coarseOffset + 2000; t += 10)
            {
                double score = 0.0;
                double os = t / 1000.0;

                // Punteggio pesato con finestra tolleranza 200ms
                for (int i = 0; i < sourceTimes.Length; i++)
                {
                    for (int j = 0; j < langTimes.Length; j++)
                    {
                        double d = Math.Abs((langTimes[j] + os) - sourceTimes[i]);
                        if (d < 0.2)
                        {
                            score += (0.2 - d);
                        }
                    }
                }

                // Aggiorna miglior offset fine se migliorato
                if (score > this._fineScore)
                {
                    this._fineScore = score;
                    this._fineOffset = t;
                }
            }

            // Fase 3: Ricerca ultra-fine attorno al risultato fine, +/- 100ms in passi da 1ms
            this._ultraFineOffset = this._fineOffset;
            this._ultraFineScore = 0.0;

            for (int t = this._fineOffset - 100; t <= this._fineOffset + 100; t++)
            {
                double score = 0.0;
                double os = t / 1000.0;

                // Punteggio pesato con finestra tolleranza 150ms
                for (int i = 0; i < sourceTimes.Length; i++)
                {
                    for (int j = 0; j < langTimes.Length; j++)
                    {
                        double d = Math.Abs((langTimes[j] + os) - sourceTimes[i]);
                        if (d < 0.15)
                        {
                            score += (0.15 - d);
                        }
                    }
                }

                // Aggiorna miglior offset ultra-fine se migliorato
                if (score > this._ultraFineScore)
                {
                    this._ultraFineScore = score;
                    this._ultraFineOffset = t;
                }
            }
        }

        /// <summary>
        /// Esegue due processi collegati via pipe: stdout del primo va a stdin del secondo.
        /// </summary>
        /// <param name="producerExe">Eseguibile primo processo.</param>
        /// <param name="producerArgs">Argomenti primo processo.</param>
        /// <param name="consumerExe">Eseguibile secondo processo.</param>
        /// <param name="consumerArgs">Argomenti secondo processo.</param>
        /// <returns>Stdout e stderr combinati dal processo consumer.</returns>
        private string RunPipedProcess(string producerExe, string producerArgs, string consumerExe, string consumerArgs)
        {
            StringBuilder result = new StringBuilder();
            Process producer = null;
            Process consumer = null;

            try
            {
                // Avvia processo producer (estrazione)
                producer = new Process();
                producer.StartInfo.FileName = producerExe;
                producer.StartInfo.Arguments = producerArgs;
                producer.StartInfo.UseShellExecute = false;
                producer.StartInfo.RedirectStandardOutput = true;
                producer.StartInfo.RedirectStandardError = true;
                producer.StartInfo.CreateNoWindow = true;
                producer.Start();

                // Avvia processo consumer (analisi)
                consumer = new Process();
                consumer.StartInfo.FileName = consumerExe;
                consumer.StartInfo.Arguments = consumerArgs;
                consumer.StartInfo.UseShellExecute = false;
                consumer.StartInfo.RedirectStandardInput = true;
                consumer.StartInfo.RedirectStandardOutput = true;
                consumer.StartInfo.RedirectStandardError = true;
                consumer.StartInfo.CreateNoWindow = true;
                consumer.Start();

                // Thread per copiare stdout producer a stdin consumer con buffer 1MB
                Thread pipeThread = new Thread(() =>
                {
                    try
                    {
                        producer.StandardOutput.BaseStream.CopyTo(consumer.StandardInput.BaseStream, 1048576);
                        consumer.StandardInput.Close();
                    }
                    catch { }
                });
                pipeThread.Start();

                // Thread per svuotare stderr producer (scarta)
                Thread producerErrThread = new Thread(() =>
                {
                    try { producer.StandardError.ReadToEnd(); }
                    catch { }
                });
                producerErrThread.Start();

                // Legge output consumer in parallelo - stdout in thread, stderr su main
                string consumerStdout = "";
                string consumerStderr = "";
                Thread consumerOutThread = new Thread(() =>
                {
                    try { consumerStdout = consumer.StandardOutput.ReadToEnd(); }
                    catch { }
                });
                consumerOutThread.Start();

                // Legge stderr su thread principale (critico per evitare deadlock)
                consumerStderr = consumer.StandardError.ReadToEnd();
                consumerOutThread.Join();

                // Attende completamento tutti i thread e processi
                pipeThread.Join();
                producerErrThread.Join();
                producer.WaitForExit();
                consumer.WaitForExit();

                // Combina output
                result.Append(consumerStdout);
                result.Append(consumerStderr);
            }
            catch (Exception ex)
            {
                result.Append("Errore pipe: " + ex.Message);
            }
            finally
            {
                // Rilascia risorse
                if (producer != null) { producer.Dispose(); producer = null; }
                if (consumer != null) { consumer.Dispose(); consumer = null; }
            }

            return result.ToString();
        }

        #endregion
    }
}
