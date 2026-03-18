using System;
using System.Collections.Generic;
using System.Threading;
using MergeLanguageTracks.Core;

namespace MergeLanguageTracks.Web.Services
{
    /// <summary>
    /// Orchestratore singleton che gestisce il ProcessingPipeline per la WebUI
    /// </summary>
    public class MergeOrchestrator
    {
        #region Variabili di classe

        /// <summary>
        /// Pipeline di elaborazione
        /// </summary>
        private ProcessingPipeline _pipeline;

        /// <summary>
        /// Lista dei record file correnti
        /// </summary>
        private List<FileProcessingRecord> _records;

        /// <summary>
        /// Opzioni correnti
        /// </summary>
        private Options _options;

        /// <summary>
        /// Lock per accesso thread-safe ai record
        /// </summary>
        private object _lock;

        /// <summary>
        /// Flag: indica se un'operazione e' in corso
        /// </summary>
        private bool _isBusy;

        /// <summary>
        /// Buffer log accumulato
        /// </summary>
        private string _logText;

        /// <summary>
        /// Indice riga selezionata nella tabella episodi
        /// </summary>
        private int _selectedIndex;

        #endregion

        #region Eventi

        /// <summary>
        /// Evento emesso per ogni messaggio di log
        /// </summary>
        public event Action<string> OnLog;

        /// <summary>
        /// Evento emesso quando i record vengono aggiornati
        /// </summary>
        public event Action OnRecordsChanged;

        /// <summary>
        /// Evento emesso quando un'operazione inizia o termina
        /// </summary>
        public event Action<bool> OnBusyChanged;

        #endregion

        #region Costruttore

        /// <summary>
        /// Costruttore
        /// </summary>
        public MergeOrchestrator()
        {
            this._pipeline = new ProcessingPipeline();
            this._records = new List<FileProcessingRecord>();
            this._options = new Options();
            this._lock = new object();
            this._isBusy = false;
            this._logText = "Pronto. Premere F2 per configurare, F5 per scan.";
            this._selectedIndex = -1;

            // Collega eventi pipeline
            this._pipeline.OnLogMessage += (string text, ConsoleColor color) =>
            {
                this.AppendLog(text);
            };

            this._pipeline.OnFileUpdated += (FileProcessingRecord record) =>
            {
                this.OnRecordsChanged?.Invoke();
            };
        }

        #endregion

        #region Metodi pubblici

        /// <summary>
        /// Salva le opzioni correnti (non inizializza il pipeline)
        /// </summary>
        /// <param name="opts">Opzioni di configurazione</param>
        public void ApplyOptions(Options opts)
        {
            this._options = opts;
        }

        /// <summary>
        /// Esegue scan delle cartelle in background (come TUI: check opts + Initialize + ScanFiles)
        /// </summary>
        public void Scan()
        {
            if (this._isBusy)
            {
                return;
            }

            // Verifica parametri minimi (come TUI DoScan)
            if (this._options.SourceFolder.Length == 0 || this._options.TargetLanguage.Count == 0)
            {
                this.AppendLog("Configurare prima source e lingua target (F2)");
                return;
            }

            Thread thread = new Thread(() =>
            {
                this.SetBusy(true);

                try
                {
                    // Inizializza pipeline con opzioni correnti (come TUI)
                    if (!this._pipeline.Initialize(this._options))
                    {
                        this.AppendLog("Errore inizializzazione pipeline");
                        this.SetBusy(false);
                        return;
                    }

                    // Scan
                    List<FileProcessingRecord> scanned = this._pipeline.ScanFiles();

                    // Ordina per EpisodeId (come TUI)
                    scanned.Sort((FileProcessingRecord a, FileProcessingRecord b) => string.Compare(a.EpisodeId, b.EpisodeId, StringComparison.OrdinalIgnoreCase));

                    lock (this._lock)
                    {
                        this._records = scanned;
                    }

                    // Conta file pronti e saltati
                    int pending = 0;
                    int skipped = 0;
                    for (int i = 0; i < scanned.Count; i++)
                    {
                        if (scanned[i].Status == FileStatus.Pending) { pending++; }
                        else if (scanned[i].Status == FileStatus.Skipped) { skipped++; }
                    }

                    this.OnRecordsChanged?.Invoke();
                    this.AppendLog("Scan completato: " + scanned.Count + " file trovati, " + pending + " pronti, " + skipped + " saltati");
                }
                catch (Exception ex)
                {
                    this.AppendLog("Errore durante scan: " + ex.Message);
                }

                this.SetBusy(false);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Analizza un singolo episodio in background
        /// </summary>
        /// <param name="index">Indice del record nella lista</param>
        public void AnalyzeFile(int index)
        {
            FileProcessingRecord record = this.GetRecord(index);

            if (record == null || this._isBusy)
            {
                return;
            }

            Thread thread = new Thread(() =>
            {
                this.SetBusy(true);

                try
                {
                    this._pipeline.AnalyzeFile(record);
                    this._pipeline.BuildMergeCommand(record);
                    this.OnRecordsChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    this.AppendLog("Errore durante analisi: " + ex.Message);
                }

                this.SetBusy(false);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Analizza tutti gli episodi pendenti in background
        /// </summary>
        public void AnalyzeAll()
        {
            if (this._isBusy)
            {
                return;
            }

            Thread thread = new Thread(() =>
            {
                this.SetBusy(true);
                List<FileProcessingRecord> snapshot = null;

                lock (this._lock)
                {
                    snapshot = new List<FileProcessingRecord>(this._records);
                }

                try
                {
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        if (snapshot[i].Status == FileStatus.Pending)
                        {
                            this._pipeline.AnalyzeFile(snapshot[i]);
                            this._pipeline.BuildMergeCommand(snapshot[i]);
                            this.OnRecordsChanged?.Invoke();
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.AppendLog("Errore durante analisi batch: " + ex.Message);
                }

                this.SetBusy(false);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Esegue merge di un singolo episodio in background
        /// </summary>
        /// <param name="index">Indice del record nella lista</param>
        public void MergeFile(int index)
        {
            FileProcessingRecord record = this.GetRecord(index);

            if (record == null || this._isBusy)
            {
                return;
            }

            Thread thread = new Thread(() =>
            {
                this.SetBusy(true);

                try
                {
                    this._pipeline.MergeFile(record);
                    this.OnRecordsChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    this.AppendLog("Errore durante merge: " + ex.Message);
                }

                this.SetBusy(false);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Esegue merge di tutti gli episodi analizzati in background
        /// </summary>
        public void MergeAll()
        {
            if (this._isBusy)
            {
                return;
            }

            Thread thread = new Thread(() =>
            {
                this.SetBusy(true);
                List<FileProcessingRecord> snapshot = null;

                lock (this._lock)
                {
                    snapshot = new List<FileProcessingRecord>(this._records);
                }

                try
                {
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        if (snapshot[i].Status == FileStatus.Analyzed)
                        {
                            this._pipeline.MergeFile(snapshot[i]);
                            this.OnRecordsChanged?.Invoke();
                        }
                    }

                    this.AppendLog("Merge batch completato.");
                }
                catch (Exception ex)
                {
                    this.AppendLog("Errore durante merge batch: " + ex.Message);
                }

                this.SetBusy(false);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Alterna lo stato skip di un episodio
        /// </summary>
        /// <param name="index">Indice del record nella lista</param>
        public void ToggleSkip(int index)
        {
            FileProcessingRecord record = this.GetRecord(index);

            if (record == null)
            {
                return;
            }

            if (record.Status == FileStatus.Skipped)
            {
                record.Status = FileStatus.Pending;
                record.SkipReason = "";
            }
            else if (record.Status == FileStatus.Pending || record.Status == FileStatus.Analyzed || record.Status == FileStatus.Error)
            {
                record.Status = FileStatus.Skipped;
                record.SkipReason = "Skippato dall'utente";
            }

            this.OnRecordsChanged?.Invoke();
        }

        /// <summary>
        /// Aggiorna il delay manuale di un episodio
        /// </summary>
        /// <param name="index">Indice del record</param>
        /// <param name="audioDelayMs">Delay audio in ms</param>
        /// <param name="subDelayMs">Delay sottotitoli in ms</param>
        public void UpdateDelay(int index, int audioDelayMs, int subDelayMs)
        {
            FileProcessingRecord record = this.GetRecord(index);

            if (record == null)
            {
                return;
            }

            record.ManualAudioDelayMs = audioDelayMs;
            record.ManualSubDelayMs = subDelayMs;
            this._pipeline.RecalculateDelays(record);
            this._pipeline.BuildMergeCommand(record);
            this.OnRecordsChanged?.Invoke();
        }

        /// <summary>
        /// Restituisce una copia della lista record corrente
        /// </summary>
        /// <returns>Lista di record</returns>
        public List<FileProcessingRecord> GetRecords()
        {
            List<FileProcessingRecord> result = null;

            lock (this._lock)
            {
                result = new List<FileProcessingRecord>(this._records);
            }

            return result;
        }

        /// <summary>
        /// Restituisce un record per indice
        /// </summary>
        /// <param name="index">Indice nella lista</param>
        /// <returns>Record o null se indice non valido</returns>
        public FileProcessingRecord GetRecord(int index)
        {
            FileProcessingRecord result = null;

            lock (this._lock)
            {
                if (index >= 0 && index < this._records.Count)
                {
                    result = this._records[index];
                }
            }

            return result;
        }

        #endregion

        #region Metodi privati

        /// <summary>
        /// Imposta lo stato busy e notifica
        /// </summary>
        /// <param name="busy">Stato busy</param>
        private void SetBusy(bool busy)
        {
            this._isBusy = busy;
            this.OnBusyChanged?.Invoke(busy);
        }

        /// <summary>
        /// Accoda un messaggio al log e notifica i client connessi
        /// </summary>
        /// <param name="message">Messaggio log</param>
        private void AppendLog(string message)
        {
            lock (this._lock)
            {
                if (this._logText.Length > 0)
                {
                    this._logText = this._logText + "\n" + message;
                }
                else
                {
                    this._logText = message;
                }
            }

            this.OnLog?.Invoke(message);
        }

        #endregion

        #region Metodi pubblici di stato

        /// <summary>
        /// Accoda un messaggio al log dall'esterno (es. Dashboard)
        /// </summary>
        /// <param name="message">Messaggio log</param>
        public void Log(string message)
        {
            this.AppendLog(message);
        }

        #endregion

        #region Proprieta

        /// <summary>
        /// Indica se un'operazione e' in corso
        /// </summary>
        public bool IsBusy { get { return this._isBusy; } }

        /// <summary>
        /// Opzioni correnti
        /// </summary>
        public Options CurrentOptions { get { return this._options; } }

        /// <summary>
        /// Testo log accumulato
        /// </summary>
        public string LogText
        {
            get
            {
                lock (this._lock)
                {
                    return this._logText;
                }
            }
        }

        /// <summary>
        /// Indice riga selezionata
        /// </summary>
        public int SelectedIndex
        {
            get { return this._selectedIndex; }
            set { this._selectedIndex = value; }
        }

        #endregion
    }
}
