using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MergeLanguageTracks.Core;
using MergeLanguageTracks.Web.Services;

namespace MergeLanguageTracks.Web.Components.Pages
{
    /// <summary>
    /// Pagina principale Dashboard - replica il layout della TUI
    /// </summary>
    public partial class Dashboard : IDisposable
    {
        #region Servizi iniettati

        /// <summary>
        /// Runtime JS per interop
        /// </summary>
        [Inject]
        private IJSRuntime JsRuntime { get; set; }

        #endregion

        #region Variabili di classe

        /// <summary>
        /// Lista record episodi correnti (letta dall'orchestratore)
        /// </summary>
        private List<FileProcessingRecord> _records;

        /// <summary>
        /// Record selezionato per il pannello dettaglio
        /// </summary>
        private FileProcessingRecord _selectedRecord;

        /// <summary>
        /// Tema corrente
        /// </summary>
        private string _currentTheme;

        /// <summary>
        /// Flag: mostra dialog configurazione
        /// </summary>
        private bool _showConfig;

        /// <summary>
        /// Flag: mostra dialog impostazioni audio
        /// </summary>
        private bool _showAudioSettings;

        /// <summary>
        /// Flag: mostra dialog delay
        /// </summary>
        private bool _showDelay;

        /// <summary>
        /// Flag: mostra dialog help
        /// </summary>
        private bool _showHelp;

        /// <summary>
        /// Modulo JS interop importato
        /// </summary>
        private IJSObjectReference _jsModule;

        /// <summary>
        /// Riferimento .NET per callback da JS
        /// </summary>
        private DotNetObjectReference<Dashboard> _dotNetRef;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Inizializzazione componente
        /// </summary>
        protected override void OnInitialized()
        {
            this._currentTheme = "nord";
            this._showConfig = false;
            this._showAudioSettings = false;
            this._showDelay = false;
            this._showHelp = false;

            // Carica stato corrente dall'orchestratore
            this._records = this.Orchestrator.GetRecords();
            this.SyncSelectedFromOrchestrator();

            // Sottoscrivi eventi orchestratore
            this.Orchestrator.OnLog += this.HandleLog;
            this.Orchestrator.OnRecordsChanged += this.HandleRecordsChanged;
        }

        /// <summary>
        /// Importa modulo JS e inizializza tastiera e tema dopo il primo render
        /// </summary>
        /// <param name="firstRender">True se primo render</param>
        protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Importa modulo JS interop
                this._jsModule = await this.JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");

                // Cattura tastiera via JS
                this._dotNetRef = DotNetObjectReference.Create(this);
                await this._jsModule.InvokeVoidAsync("captureKeyboard", this._dotNetRef);

                // Carica tema salvato
                string savedTheme = await this._jsModule.InvokeAsync<string>("loadSavedTheme");
                this._currentTheme = savedTheme;
                this.StateHasChanged();
            }
        }

        /// <summary>
        /// Cleanup sottoscrizioni e risorse JS
        /// </summary>
        public void Dispose()
        {
            this.Orchestrator.OnLog -= this.HandleLog;
            this.Orchestrator.OnRecordsChanged -= this.HandleRecordsChanged;

            // Dispose riferimento .NET
            if (this._dotNetRef != null)
            {
                this._dotNetRef.Dispose();
            }

            // Dispose modulo JS
            if (this._jsModule != null)
            {
                try
                {
                    this._jsModule.DisposeAsync();
                }
                catch
                {
                    // Ignora errori durante dispose
                }
            }
        }

        #endregion

        #region Gestori eventi

        /// <summary>
        /// Gestisce messaggio log dall'orchestratore
        /// </summary>
        /// <param name="message">Messaggio log</param>
        private void HandleLog(string message)
        {
            // Il log e' gia' accumulato nell'orchestratore, forza solo il re-render
            this.InvokeAsync(() => this.StateHasChanged());
        }

        /// <summary>
        /// Gestisce aggiornamento record dall'orchestratore
        /// </summary>
        private void HandleRecordsChanged()
        {
            this._records = this.Orchestrator.GetRecords();
            this.SyncSelectedFromOrchestrator();
            this.InvokeAsync(() => this.StateHasChanged());
        }

        /// <summary>
        /// Gestisce scorciatoie da tastiera (invocato da JS interop)
        /// </summary>
        /// <param name="key">Tasto premuto</param>
        /// <param name="ctrl">Flag Ctrl</param>
        /// <param name="shift">Flag Shift</param>
        /// <param name="alt">Flag Alt</param>
        [JSInvokable("OnKeyDown")]
        public void HandleKeyDown(string key, bool ctrl, bool shift, bool alt)
        {
            if (key == "F1") { this.ShowHelp(); }
            else if (key == "F2") { this.ShowConfig(); }
            else if (key == "F5") { this.DoScan(); }
            else if (key == "F6") { this.DoAnalyzeSelected(); }
            else if (key == "F7") { this.DoAnalyzeAll(); }
            else if (key == "F8") { this.DoToggleSkip(); }
            else if (key == "F9") { this.DoMergeSelected(); }
            else if (key == "F10") { this.DoMergeAll(); }
            else if (key == "Escape") { this.CloseAllDialogs(); }

            this.StateHasChanged();
        }

        /// <summary>
        /// Seleziona riga nella tabella episodi
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void SelectRow(int index)
        {
            // Salva la selezione nell'orchestratore
            this.Orchestrator.SelectedIndex = index;

            if (index >= 0 && index < this._records.Count)
            {
                this._selectedRecord = this._records[index];
            }
            else
            {
                this._selectedRecord = null;
            }
        }

        /// <summary>
        /// Attiva riga (doppio click / Enter) - mostra dialog delay
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void ActivateRow(int index)
        {
            this.SelectRow(index);

            if (this._selectedRecord != null)
            {
                this._showDelay = true;
            }
        }

        /// <summary>
        /// Sincronizza il record selezionato leggendo l'indice dall'orchestratore
        /// </summary>
        private void SyncSelectedFromOrchestrator()
        {
            int index = this.Orchestrator.SelectedIndex;

            if (index >= 0 && index < this._records.Count)
            {
                this._selectedRecord = this._records[index];
            }
            else
            {
                this._selectedRecord = null;
            }
        }

        #endregion

        #region Azioni

        /// <summary>
        /// Esegue scan cartelle
        /// </summary>
        private void DoScan()
        {
            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.Scan();
            }
        }

        /// <summary>
        /// Analizza episodio selezionato
        /// </summary>
        private void DoAnalyzeSelected()
        {
            if (!this.Orchestrator.IsBusy && this.Orchestrator.SelectedIndex >= 0)
            {
                this.Orchestrator.AnalyzeFile(this.Orchestrator.SelectedIndex);
            }
        }

        /// <summary>
        /// Analizza tutti gli episodi pendenti
        /// </summary>
        private void DoAnalyzeAll()
        {
            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.AnalyzeAll();
            }
        }

        /// <summary>
        /// Alterna stato skip episodio selezionato
        /// </summary>
        private void DoToggleSkip()
        {
            if (this.Orchestrator.SelectedIndex >= 0)
            {
                this.Orchestrator.ToggleSkip(this.Orchestrator.SelectedIndex);
            }
        }

        /// <summary>
        /// Esegue merge episodio selezionato
        /// </summary>
        private void DoMergeSelected()
        {
            if (!this.Orchestrator.IsBusy && this.Orchestrator.SelectedIndex >= 0)
            {
                this.Orchestrator.MergeFile(this.Orchestrator.SelectedIndex);
            }
        }

        /// <summary>
        /// Esegue merge di tutti gli episodi analizzati
        /// </summary>
        private void DoMergeAll()
        {
            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.MergeAll();
            }
        }

        /// <summary>
        /// Mostra dialog configurazione
        /// </summary>
        private void ShowConfig()
        {
            this._showConfig = true;
        }

        /// <summary>
        /// Chiude dialog configurazione
        /// </summary>
        private void CloseConfig()
        {
            this._showConfig = false;
        }

        /// <summary>
        /// Applica configurazione e reinizializza pipeline
        /// </summary>
        /// <param name="opts">Nuove opzioni</param>
        private void ApplyConfig(Options opts)
        {
            this._showConfig = false;
            this.Orchestrator.ApplyOptions(opts);
            this.Orchestrator.Log("Configurazione aggiornata");
        }

        /// <summary>
        /// Mostra dialog impostazioni audio
        /// </summary>
        private void ShowAudioSettings()
        {
            this._showAudioSettings = true;
        }

        /// <summary>
        /// Chiude dialog impostazioni audio
        /// </summary>
        private void CloseAudioSettings()
        {
            this._showAudioSettings = false;
        }

        /// <summary>
        /// Chiude dialog delay
        /// </summary>
        private void CloseDelay()
        {
            this._showDelay = false;
        }

        /// <summary>
        /// Applica delay per-file
        /// </summary>
        /// <param name="delays">Tupla (audioDelay, subDelay) in ms</param>
        private void ApplyDelay((int, int) delays)
        {
            this._showDelay = false;

            if (this.Orchestrator.SelectedIndex >= 0)
            {
                this.Orchestrator.UpdateDelay(this.Orchestrator.SelectedIndex, delays.Item1, delays.Item2);
            }
        }

        /// <summary>
        /// Mostra dialog help
        /// </summary>
        private void ShowHelp()
        {
            this._showHelp = true;
        }

        /// <summary>
        /// Chiude dialog help
        /// </summary>
        private void CloseHelp()
        {
            this._showHelp = false;
        }

        /// <summary>
        /// Chiude tutti i dialog aperti
        /// </summary>
        private void CloseAllDialogs()
        {
            this._showConfig = false;
            this._showAudioSettings = false;
            this._showDelay = false;
            this._showHelp = false;
        }

        /// <summary>
        /// Cambia tema via modulo JS interop
        /// </summary>
        /// <param name="theme">Nome tema</param>
        private void ChangeTheme(string theme)
        {
            this._currentTheme = theme;

            if (this._jsModule != null)
            {
                try
                {
                    this._jsModule.InvokeVoidAsync("setTheme", theme);
                }
                catch
                {
                    // Ignora errori JS durante dispose
                }
            }
        }

        #endregion
    }
}
