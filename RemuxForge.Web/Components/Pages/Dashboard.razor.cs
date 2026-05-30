using RemuxForge.Core.Configuration;
using RemuxForge.Core.Localization;
using RemuxForge.Core.Media;
using RemuxForge.Core.Models;
using RemuxForge.Core.Tools;
using RemuxForge.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RemuxForge.Web.Components.Pages
{
    /// <summary>
    /// Pagina principale Dashboard - dashboard operativa
    /// </summary>
    public partial class Dashboard : IAsyncDisposable
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
        /// Lista record split correnti
        /// </summary>
        private List<MkvSplitRecord> _splitRecords;

        /// <summary>
        /// Record selezionato per il pannello dettaglio
        /// </summary>
        private FileProcessingRecord _selectedRecord;

        /// <summary>
        /// Record split selezionato
        /// </summary>
        private MkvSplitRecord _selectedSplitRecord;

        /// <summary>
        /// Indici episodi selezionati in modalita' multi-select
        /// </summary>
        private List<int> _selectedIndices;

        /// <summary>
        /// Anchor per selezione range con Shift
        /// </summary>
        private int _selectionAnchorIndex;

        /// <summary>
        /// Tema corrente
        /// </summary>
        private string _currentTheme;

        /// <summary>
        /// Lingua corrente
        /// </summary>
        private string _currentLanguage;

        /// <summary>
        /// Modalita' corrente UI
        /// </summary>
        private string _currentMode;

        /// <summary>
        /// Flag: mostra dialog configurazione
        /// </summary>
        private bool _showConfig;

        /// <summary>
        /// Flag: mostra dialog percorsi tool
        /// </summary>
        private bool _showToolPaths;

        /// <summary>
        /// Flag: mostra dialog impostazioni audio
        /// </summary>
        private bool _showAudioSettings;

        /// <summary>
        /// Flag: mostra dialog impostazioni avanzate
        /// </summary>
        private bool _showAdvancedSettings;

        /// <summary>
        /// Flag: mostra dialog delay
        /// </summary>
        private bool _showDelay;

        /// <summary>
        /// Flag: mostra dialog profili encoding
        /// </summary>
        private bool _showEncodingProfiles;

        /// <summary>
        /// Flag: mostra dialog pipeline
        /// </summary>
        private bool _showPipeline;

        /// <summary>
        /// Flag: mostra dialog info
        /// </summary>
        private bool _showInfo;

        /// <summary>
        /// Flag: mostra context menu episodio
        /// </summary>
        private bool _showContextMenu;

        /// <summary>
        /// Voci del context menu corrente
        /// </summary>
        private List<string> _contextMenuItems;

        /// <summary>
        /// Azioni corrispondenti alle voci del context menu
        /// </summary>
        private List<Action> _contextMenuActions;

        /// <summary>
        /// Voce attiva nel context menu per navigazione tastiera
        /// </summary>
        private int _contextMenuSelectedIndex;

        /// <summary>
        /// Coordinata X del context menu (pixel dal bordo sinistro viewport)
        /// </summary>
        private double _contextMenuX;

        /// <summary>
        /// Coordinata Y del context menu (pixel dal bordo superiore viewport)
        /// </summary>
        private double _contextMenuY;

        /// <summary>
        /// Flag: mostra dialog mediainfo
        /// </summary>
        private bool _showMediaInfo;

        /// <summary>
        /// Titolo dialog mediainfo
        /// </summary>
        private string _mediaInfoTitle;

        /// <summary>
        /// Report mediainfo testuale
        /// </summary>
        private string _mediaInfoReport;

        /// <summary>
        /// Modulo JS interop importato
        /// </summary>
        private IJSObjectReference _jsModule;

        /// <summary>
        /// Riferimento .NET per callback da JS
        /// </summary>
        private DotNetObjectReference<Dashboard> _dotNetRef;

        /// <summary>
        /// Riferimento menu bar per navigazione tastiera
        /// </summary>
        private MenuBarComponent _menuBar;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Inizializzazione componente
        /// </summary>
        protected override void OnInitialized()
        {
            this._currentTheme = AppSettingsService.Instance.Settings.Ui.Theme;
            this._currentLanguage = AppText.NormalizeLanguage(AppSettingsService.Instance.Settings.Ui.Language);
            if (this._currentLanguage.Length == 0)
            {
                this._currentLanguage = AppText.LANG_EN;
            }
            this._currentMode = AppSettingsService.Instance.Settings.Ui.LastMode;
            if (this._currentMode != Options.MODE_REMUX && this._currentMode != Options.MODE_SPLIT)
            {
                this._currentMode = Options.MODE_REMUX;
            }
            this._showConfig = false;
            this._showToolPaths = false;
            this._showAudioSettings = false;
            this._showAdvancedSettings = false;
            this._showDelay = false;
            this._showEncodingProfiles = false;
            this._showPipeline = false;
            this._showInfo = false;
            this._showContextMenu = false;
            this._contextMenuItems = new List<string>();
            this._contextMenuActions = new List<Action>();
            this._showMediaInfo = false;
            this._mediaInfoTitle = "";
            this._mediaInfoReport = "";
            this._selectedIndices = new List<int>();
            this._selectionAnchorIndex = -1;
            this._contextMenuSelectedIndex = 0;

            // Carica stato corrente dall'orchestratore
            this._records = this.Orchestrator.GetRecords();
            this._splitRecords = this.SplitOrchestrator.GetRecords();
            this.SyncSelectedFromOrchestrator();
            this.SyncSelectedFromSplitOrchestrator();

            // Sottoscrivi eventi orchestratore
            this.Orchestrator.OnLog += this.HandleLog;
            this.Orchestrator.OnRecordsChanged += this.HandleRecordsChanged;
            this.Orchestrator.OnProgressChanged += this.HandleProgressChanged;
            this.SplitOrchestrator.OnLog += this.HandleLog;
            this.SplitOrchestrator.OnRecordsChanged += this.HandleSplitRecordsChanged;
            this.SplitOrchestrator.OnProgressChanged += this.HandleProgressChanged;
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

                // Carica tema da AppSettings e applica via JS
                this._currentTheme = AppSettingsService.Instance.Settings.Ui.Theme;
                await this._jsModule.InvokeVoidAsync("setTheme", AppSettingsService.Instance.Settings.Ui.Theme);
                await this._jsModule.InvokeVoidAsync("setLanguage", this._currentLanguage);
                this.StateHasChanged();
            }
        }

        /// <summary>
        /// Cleanup sottoscrizioni e risorse JS
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            // Rimuovi sottoscrizioni eventi
            if (this.Orchestrator != null)
            {
                this.Orchestrator.OnLog -= this.HandleLog;
                this.Orchestrator.OnRecordsChanged -= this.HandleRecordsChanged;
                this.Orchestrator.OnProgressChanged -= this.HandleProgressChanged;
                this.SplitOrchestrator.OnLog -= this.HandleLog;
                this.SplitOrchestrator.OnRecordsChanged -= this.HandleSplitRecordsChanged;
                this.SplitOrchestrator.OnProgressChanged -= this.HandleProgressChanged;
            }

            // Dispose riferimento .NET per JS interop
            if (this._dotNetRef != null)
            {
                this._dotNetRef.Dispose();
            }

            // Rilascia handler tastiera e dispose modulo JS
            if (this._jsModule != null)
            {
                try
                {
                    await this._jsModule.InvokeVoidAsync("releaseKeyboard");
                }
                catch
                {
                    // Ignora errori durante dispose (circuito chiuso)
                }

                try
                {
                    await this._jsModule.DisposeAsync();
                }
                catch
                {
                    // Ignora errori durante dispose (circuito chiuso)
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
            this.InvokeAsync(() =>
            {
                this._records = this.Orchestrator.GetRecords();
                this.NormalizeSelection();
                this.SyncSelectedFromOrchestrator();
                this.StateHasChanged();
            });
        }

        /// <summary>
        /// Gestisce aggiornamento record split
        /// </summary>
        private void HandleSplitRecordsChanged()
        {
            this.InvokeAsync(() =>
            {
                this._splitRecords = this.SplitOrchestrator.GetRecords();
                this.SyncSelectedFromSplitOrchestrator();
                this.StateHasChanged();
            });
        }

        /// <summary>
        /// Gestisce aggiornamento avanzamento dall'orchestratore
        /// </summary>
        private void HandleProgressChanged()
        {
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
            if (this.IsBlockingDialogOpen())
            {
                if (key == "Escape")
                {
                    this.CloseAllDialogs();
                    this.StateHasChanged();
                }

                return;
            }

            if (this._showContextMenu && this.HandleContextMenuKey(key))
            {
                this.StateHasChanged();
                return;
            }

            if (this._menuBar != null && this._menuBar.HandleKeyboardKey(key, ctrl, shift, alt))
            {
                this.StateHasChanged();
                return;
            }

            if (this._currentMode == Options.MODE_SPLIT)
            {
                if (key == "F2") { this.ShowConfig(); }
                else if (key == "F5") { this.DoScan(); }
                else if (key == "F10") { this.DoMergeAll(); }
                else if (key == "F12") { this.DoStop(); }
                else if (key == "Escape") { this.CloseAllDialogs(); }
                else if (key == "ArrowUp") { this.MoveSplitSelection(-1); }
                else if (key == "ArrowDown") { this.MoveSplitSelection(1); }
                else if (key == "Home") { this.SelectSplitRow(0); }
                else if (key == "End") { this.SelectSplitRow(this._splitRecords.Count - 1); }
            }
            else
            {
                if (key == "F2") { this.ShowConfig(); }
                else if (key == "F5") { this.DoScan(); }
                else if (key == "F6") { this.DoAnalyzeSelected(); }
                else if (key == "F7") { this.DoAnalyzeAll(); }
                else if (key == "F8") { this.DoToggleSkip(); }
                else if (key == "F9") { this.DoMergeSelected(); }
                else if (key == "F10") { this.DoMergeAll(); }
                else if (key == "F12") { this.DoStop(); }
                else if (key == "Enter") { this.ShowContextMenuForSelected(); }
                else if (key == "Escape") { this.CloseAllDialogs(); }
                else if (ctrl && string.Equals(key, "a", StringComparison.OrdinalIgnoreCase)) { this.SelectAllRows(); }
                else if (key == "ArrowUp") { this.MoveSelection(-1, shift, ctrl); }
                else if (key == "ArrowDown") { this.MoveSelection(1, shift, ctrl); }
                else if (key == "Home") { this.SelectIndexFromKeyboard(0, shift, ctrl); }
                else if (key == "End") { this.SelectIndexFromKeyboard(this._records.Count - 1, shift, ctrl); }
                else if (key == "PageUp") { this.MoveSelection(-10, shift, ctrl); }
                else if (key == "PageDown") { this.MoveSelection(10, shift, ctrl); }
                else if (key == " ") { this.ToggleFocusedSelection(); }
            }

            this.StateHasChanged();
        }

        /// <summary>
        /// Seleziona riga nella tabella episodi
        /// </summary>
        /// <param name="args">Indice riga e modifier tastiera</param>
        private void SelectRow((int Index, bool Ctrl, bool Shift) args)
        {
            this.ApplyRowSelection(args.Index, args.Ctrl, args.Shift);
        }

        /// <summary>
        /// Seleziona riga split
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void SelectSplitRow(int index)
        {
            this.SplitOrchestrator.SelectedIndex = index;
            if (index >= 0 && index < this._splitRecords.Count)
            {
                this._selectedSplitRecord = this._splitRecords[index];
                this.ScrollSplitRowIntoView(index);
            }
            else
            {
                this._selectedSplitRecord = null;
            }
        }

        /// <summary>
        /// Applica selezione mouse stile file explorer
        /// </summary>
        /// <param name="index">Indice riga</param>
        /// <param name="ctrl">True se selezione additiva/toggle</param>
        /// <param name="shift">True se selezione range</param>
        private void ApplyRowSelection(int index, bool ctrl, bool shift)
        {
            if (index < 0 || index >= this._records.Count)
            {
                return;
            }

            if (shift)
            {
                if (this._selectionAnchorIndex < 0 || this._selectionAnchorIndex >= this._records.Count)
                {
                    this._selectionAnchorIndex = this.Orchestrator.SelectedIndex >= 0 ? this.Orchestrator.SelectedIndex : index;
                }

                if (!ctrl)
                {
                    this._selectedIndices.Clear();
                }

                this.AddSelectionRange(this._selectionAnchorIndex, index);
            }
            else if (ctrl)
            {
                if (this.IsRowSelected(index))
                {
                    this._selectedIndices.Remove(index);
                }
                else
                {
                    this._selectedIndices.Add(index);
                    this.SortSelectedIndices();
                }

                this._selectionAnchorIndex = index;
            }
            else
            {
                this._selectedIndices.Clear();
                this._selectedIndices.Add(index);
                this._selectionAnchorIndex = index;
            }

            this.SetFocusedRow(index);
        }

        /// <summary>
        /// Aggiorna focus riga mantenendo invariata la selezione multi
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void SetFocusedRow(int index)
        {
            this.Orchestrator.SelectedIndex = index;

            if (index >= 0 && index < this._records.Count)
            {
                this._selectedRecord = this._records[index];
                this.ScrollEpisodeRowIntoView(index);
            }
            else
            {
                this._selectedRecord = null;
            }
        }

        /// <summary>
        /// Muove la selezione da tastiera
        /// </summary>
        /// <param name="delta">Spostamento relativo</param>
        /// <param name="shift">True se estende range</param>
        /// <param name="ctrl">True se muove solo focus</param>
        private void MoveSelection(int delta, bool shift, bool ctrl)
        {
            int currentIndex = this.Orchestrator.SelectedIndex;
            int targetIndex;

            if (this._records.Count == 0)
            {
                return;
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            targetIndex = currentIndex + delta;
            this.SelectIndexFromKeyboard(targetIndex, shift, ctrl);
        }

        /// <summary>
        /// Seleziona un indice da tastiera applicando modifier
        /// </summary>
        /// <param name="index">Indice richiesto</param>
        /// <param name="shift">True se estende range</param>
        /// <param name="ctrl">True se muove solo focus</param>
        private void SelectIndexFromKeyboard(int index, bool shift, bool ctrl)
        {
            int targetIndex = index;

            if (this._records.Count == 0)
            {
                return;
            }

            if (targetIndex < 0) { targetIndex = 0; }
            if (targetIndex >= this._records.Count) { targetIndex = this._records.Count - 1; }

            if (ctrl && !shift)
            {
                this.SetFocusedRow(targetIndex);
                return;
            }

            this.ApplyRowSelection(targetIndex, ctrl, shift);
        }

        /// <summary>
        /// Muove selezione split con frecce
        /// </summary>
        /// <param name="delta">Spostamento relativo</param>
        private void MoveSplitSelection(int delta)
        {
            int currentIndex = this.SplitOrchestrator.SelectedIndex;
            int targetIndex;

            if (this._splitRecords.Count == 0)
            {
                return;
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            targetIndex = currentIndex + delta;
            if (targetIndex < 0) { targetIndex = 0; }
            if (targetIndex >= this._splitRecords.Count) { targetIndex = this._splitRecords.Count - 1; }
            this.SelectSplitRow(targetIndex);
        }

        /// <summary>
        /// Seleziona tutti gli episodi
        /// </summary>
        private void SelectAllRows()
        {
            this._selectedIndices.Clear();
            for (int i = 0; i < this._records.Count; i++)
            {
                this._selectedIndices.Add(i);
            }

            if (this._records.Count > 0 && this.Orchestrator.SelectedIndex < 0)
            {
                this.SetFocusedRow(0);
                this._selectionAnchorIndex = 0;
            }
        }

        /// <summary>
        /// Toggle selezione della riga con focus
        /// </summary>
        private void ToggleFocusedSelection()
        {
            int index = this.Orchestrator.SelectedIndex;
            if (index < 0 || index >= this._records.Count)
            {
                return;
            }

            if (this.IsRowSelected(index))
            {
                this._selectedIndices.Remove(index);
            }
            else
            {
                this._selectedIndices.Add(index);
                this.SortSelectedIndices();
            }

            this._selectionAnchorIndex = index;
        }

        /// <summary>
        /// Aggiunge range selezione inclusivo
        /// </summary>
        private void AddSelectionRange(int startIndex, int endIndex)
        {
            int first = Math.Min(startIndex, endIndex);
            int last = Math.Max(startIndex, endIndex);

            for (int i = first; i <= last; i++)
            {
                if (!this.IsRowSelected(i))
                {
                    this._selectedIndices.Add(i);
                }
            }

            this.SortSelectedIndices();
        }

        /// <summary>
        /// True se una riga e' nella selezione multi
        /// </summary>
        private bool IsRowSelected(int index)
        {
            bool result = false;

            for (int i = 0; i < this._selectedIndices.Count; i++)
            {
                if (this._selectedIndices[i] == index)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Ordina gli indici selezionati
        /// </summary>
        private void SortSelectedIndices()
        {
            this._selectedIndices.Sort();
        }

        /// <summary>
        /// Restituisce gli indici su cui applicare un'azione selezionata
        /// </summary>
        private List<int> GetActionSelectionIndices()
        {
            List<int> result = new List<int>();

            for (int i = 0; i < this._selectedIndices.Count; i++)
            {
                if (this._selectedIndices[i] >= 0 && this._selectedIndices[i] < this._records.Count && !result.Contains(this._selectedIndices[i]))
                {
                    result.Add(this._selectedIndices[i]);
                }
            }

            if (result.Count == 0 && this.Orchestrator.SelectedIndex >= 0 && this.Orchestrator.SelectedIndex < this._records.Count)
            {
                result.Add(this.Orchestrator.SelectedIndex);
            }

            result.Sort();
            return result;
        }

        /// <summary>
        /// Rimuove selezioni non piu' valide dopo refresh record
        /// </summary>
        private void NormalizeSelection()
        {
            for (int i = this._selectedIndices.Count - 1; i >= 0; i--)
            {
                if (this._selectedIndices[i] < 0 || this._selectedIndices[i] >= this._records.Count)
                {
                    this._selectedIndices.RemoveAt(i);
                }
            }

            if (this._selectionAnchorIndex >= this._records.Count)
            {
                this._selectionAnchorIndex = this._records.Count - 1;
            }
        }

        /// <summary>
        /// True se c'e' un dialog modale aperto che deve bloccare scorciatoie tabella
        /// </summary>
        private bool IsBlockingDialogOpen()
        {
            return this._showConfig || this._showToolPaths || this._showAudioSettings || this._showAdvancedSettings || this._showDelay || this._showEncodingProfiles || this._showPipeline || this._showInfo || this._showMediaInfo;
        }

        /// <summary>
        /// Gestisce tastiera context menu
        /// </summary>
        /// <param name="key">Tasto</param>
        /// <returns>True se gestito</returns>
        private bool HandleContextMenuKey(string key)
        {
            bool result = false;

            if (key == "Escape")
            {
                this.CloseContextMenu();
                result = true;
            }
            else if (key == "ArrowDown")
            {
                if (this._contextMenuItems.Count > 0)
                {
                    this._contextMenuSelectedIndex++;
                    if (this._contextMenuSelectedIndex >= this._contextMenuItems.Count) { this._contextMenuSelectedIndex = 0; }
                }

                result = true;
            }
            else if (key == "ArrowUp")
            {
                if (this._contextMenuItems.Count > 0)
                {
                    this._contextMenuSelectedIndex--;
                    if (this._contextMenuSelectedIndex < 0) { this._contextMenuSelectedIndex = this._contextMenuItems.Count - 1; }
                }

                result = true;
            }
            else if (key == "Enter" || key == " ")
            {
                this.HandleContextMenuSelect(this._contextMenuSelectedIndex);
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Scorre la riga episodio selezionata dentro la viewport tabella
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void ScrollEpisodeRowIntoView(int index)
        {
            if (this._jsModule == null)
            {
                return;
            }

            try
            {
                _ = this._jsModule.InvokeVoidAsync("scrollEpisodeRowIntoView", index);
            }
            catch
            {
                // Ignora errori JS se il circuito si sta chiudendo
            }
        }

        /// <summary>
        /// Scorre la riga split selezionata dentro la viewport tabella
        /// </summary>
        /// <param name="index">Indice riga</param>
        private void ScrollSplitRowIntoView(int index)
        {
            if (this._jsModule == null)
            {
                return;
            }

            try
            {
                _ = this._jsModule.InvokeVoidAsync("scrollSplitRowIntoView", index);
            }
            catch
            {
                // Ignora errori JS se il circuito si sta chiudendo
            }
        }

        /// <summary>
        /// Mostra context menu per l'episodio all'indice specificato
        /// </summary>
        /// <param name="args">Tupla (indice riga, clientX, clientY)</param>
        private void ShowContextMenu((int Index, double X, double Y) args)
        {
            if (!this.IsRowSelected(args.Index))
            {
                this.ApplyRowSelection(args.Index, false, false);
            }
            else
            {
                this.SetFocusedRow(args.Index);
            }

            if (this._selectedRecord == null) { return; }

            this._contextMenuX = args.X;
            this._contextMenuY = args.Y;
            this.BuildContextMenu(this._selectedRecord);
            this._contextMenuSelectedIndex = 0;
            this._showContextMenu = true;
        }

        /// <summary>
        /// Mostra context menu per l'episodio selezionato (da Enter)
        /// </summary>
        private void ShowContextMenuForSelected()
        {
            if (this._selectedRecord == null) { return; }

            // Da tastiera: posiziona al centro dello schermo
            this._contextMenuX = 400;
            this._contextMenuY = 300;
            this.BuildContextMenu(this._selectedRecord);
            this._contextMenuSelectedIndex = 0;
            this._showContextMenu = true;
        }

        /// <summary>
        /// Costruisce le voci del context menu in base al record
        /// </summary>
        /// <param name="record">Record episodio</param>
        private void BuildContextMenu(FileProcessingRecord record)
        {
            // Verifica disponibilita' mediainfo
            bool mediaInfoAvailable = (AppSettingsService.Instance.Settings.Tools.MediaInfoPath.Length > 0
                && System.IO.File.Exists(AppSettingsService.Instance.Settings.Tools.MediaInfoPath)
                && MediaInfoProvider.IsCliExecutablePath(AppSettingsService.Instance.Settings.Tools.MediaInfoPath));

            this._contextMenuItems = new List<string>();
            this._contextMenuActions = new List<Action>();

            // Delay: sempre visibile
            this._contextMenuItems.Add(AppText.T("web.context.delay"));
            this._contextMenuActions.Add(() =>
            {
                this._showContextMenu = false;
                this._showDelay = true;
            });

            // MediaInfo sorgente
            if (mediaInfoAvailable && record.SourceFilePath.Length > 0 && System.IO.File.Exists(record.SourceFilePath))
            {
                this._contextMenuItems.Add(AppText.T("web.context.mediaInfoSource"));
                this._contextMenuActions.Add(() => { this.OpenMediaInfo(record.SourceFilePath, AppText.F("web.mediaInfo.sourceTitle", record.SourceFileName)); });
            }

            // MediaInfo lingua
            if (mediaInfoAvailable && record.LangFilePath.Length > 0 && System.IO.File.Exists(record.LangFilePath))
            {
                this._contextMenuItems.Add(AppText.T("web.context.mediaInfoLanguage"));
                this._contextMenuActions.Add(() => { this.OpenMediaInfo(record.LangFilePath, AppText.F("web.mediaInfo.languageTitle", record.LangFileName)); });
            }

            // MediaInfo risultato
            if (mediaInfoAvailable && record.ResultFilePath.Length > 0 && System.IO.File.Exists(record.ResultFilePath))
            {
                this._contextMenuItems.Add(AppText.T("web.context.mediaInfoResult"));
                this._contextMenuActions.Add(() => { this.OpenMediaInfo(record.ResultFilePath, AppText.F("web.mediaInfo.resultTitle", record.ResultFileName)); });
            }
        }

        /// <summary>
        /// Gestisce selezione voce dal context menu
        /// </summary>
        /// <param name="index">Indice voce selezionata</param>
        private void HandleContextMenuSelect(int index)
        {
            this._showContextMenu = false;

            if (index >= 0 && index < this._contextMenuActions.Count)
            {
                this._contextMenuActions[index]();
            }
        }

        /// <summary>
        /// Aggiorna voce attiva del context menu da hover mouse
        /// </summary>
        /// <param name="index">Indice voce attiva</param>
        private void SetContextMenuActiveIndex(int index)
        {
            this._contextMenuSelectedIndex = index;
        }

        /// <summary>
        /// Chiude context menu
        /// </summary>
        private void CloseContextMenu()
        {
            this._showContextMenu = false;
        }

        /// <summary>
        /// Genera report mediainfo e mostra dialog
        /// </summary>
        /// <param name="filePath">Percorso file da analizzare</param>
        /// <param name="title">Titolo del dialog</param>
        private void OpenMediaInfo(string filePath, string title)
        {
            this._showContextMenu = false;

            MediaInfoService miService = new MediaInfoService(AppSettingsService.Instance.Settings.Tools.MediaInfoPath);
            this._mediaInfoReport = miService.GetReport(filePath);
            this._mediaInfoTitle = title;
            this._showMediaInfo = true;
        }

        /// <summary>
        /// Chiude dialog mediainfo
        /// </summary>
        private void CloseMediaInfo()
        {
            this._showMediaInfo = false;
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

        /// <summary>
        /// Sincronizza record split selezionato
        /// </summary>
        private void SyncSelectedFromSplitOrchestrator()
        {
            int index = this.SplitOrchestrator.SelectedIndex;

            if (index >= 0 && index < this._splitRecords.Count)
            {
                this._selectedSplitRecord = this._splitRecords[index];
            }
            else
            {
                this._selectedSplitRecord = null;
            }
        }

        #endregion

        #region Azioni

        /// <summary>
        /// Cambia modalita' UI e salva preferenza
        /// </summary>
        /// <param name="mode">Modalita' richiesta</param>
        private void SwitchMode(string mode)
        {
            if (mode != Options.MODE_REMUX && mode != Options.MODE_SPLIT)
            {
                return;
            }

            this._currentMode = mode;
            AppSettingsService.Instance.Settings.Ui.LastMode = mode;
            AppSettingsService.Instance.Save();
        }

        /// <summary>
        /// Applica configurazione split rapida
        /// </summary>
        private bool ApplySplitConfig()
        {
            string errorMessage;
            Options opts = this.SplitOrchestrator.CurrentOptions;
            opts.Mode = Options.MODE_SPLIT;
            opts.Split.SourcePath = opts.SourceFolder;
            if (!this.SplitOrchestrator.ApplyOptions(opts, out errorMessage) && errorMessage.Length > 0)
            {
                this.SplitOrchestrator.Log(errorMessage);
                this.StateHasChanged();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Esegue scan cartelle
        /// </summary>
        private void DoScan()
        {
            if (this._currentMode == Options.MODE_SPLIT)
            {
                if (!this.ApplySplitConfig())
                {
                    return;
                }

                if (!this.SplitOrchestrator.IsBusy)
                {
                    this.SplitOrchestrator.Scan();
                }
                return;
            }

            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.Scan();
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.scanBusy"));
            }
        }

        /// <summary>
        /// Analizza episodio selezionato
        /// </summary>
        private void DoAnalyzeSelected()
        {
            List<int> selectedIndices;
            if (this._currentMode == Options.MODE_SPLIT)
            {
                return;
            }

            selectedIndices = this.GetActionSelectionIndices();
            if (!this.Orchestrator.IsBusy && selectedIndices.Count > 1)
            {
                this.Orchestrator.AnalyzeFiles(selectedIndices);
            }
            else if (!this.Orchestrator.IsBusy && selectedIndices.Count == 1)
            {
                this.Orchestrator.AnalyzeFile(selectedIndices[0]);
            }
            else if (this.Orchestrator.IsBusy)
            {
                this.Orchestrator.Log(AppText.T("web.log.analyzeBusy"));
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.selectEpisodeAnalyze"));
            }
        }

        /// <summary>
        /// Analizza tutti gli episodi pendenti
        /// </summary>
        private void DoAnalyzeAll()
        {
            if (this._currentMode == Options.MODE_SPLIT)
            {
                return;
            }

            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.AnalyzeAll();
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.analyzeBatchBusy"));
            }
        }

        /// <summary>
        /// Alterna stato skip episodio selezionato
        /// </summary>
        private void DoToggleSkip()
        {
            List<int> selectedIndices;
            if (this._currentMode == Options.MODE_SPLIT)
            {
                return;
            }

            selectedIndices = this.GetActionSelectionIndices();
            if (selectedIndices.Count > 1)
            {
                this.Orchestrator.ToggleSkip(selectedIndices);
            }
            else if (selectedIndices.Count == 1)
            {
                this.Orchestrator.ToggleSkip(selectedIndices[0]);
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.selectEpisodeSkip"));
            }
        }

        /// <summary>
        /// Esegue merge episodio selezionato
        /// </summary>
        private void DoMergeSelected()
        {
            List<int> selectedIndices;
            if (this._currentMode == Options.MODE_SPLIT)
            {
                this.DoMergeAll();
                return;
            }

            selectedIndices = this.GetActionSelectionIndices();
            if (!this.Orchestrator.IsBusy && selectedIndices.Count > 1)
            {
                this.Orchestrator.MergeFiles(selectedIndices);
            }
            else if (!this.Orchestrator.IsBusy && selectedIndices.Count == 1)
            {
                this.Orchestrator.MergeFile(selectedIndices[0]);
            }
            else if (this.Orchestrator.IsBusy)
            {
                this.Orchestrator.Log(AppText.T("web.log.mergeBusy"));
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.selectEpisodeProcess"));
            }
        }

        /// <summary>
        /// Esegue merge di tutti gli episodi analizzati
        /// </summary>
        private void DoMergeAll()
        {
            if (this._currentMode == Options.MODE_SPLIT)
            {
                if (!this.ApplySplitConfig())
                {
                    return;
                }

                if (!this.SplitOrchestrator.IsBusy)
                {
                    this.SplitOrchestrator.SplitAll();
                }
                return;
            }

            if (!this.Orchestrator.IsBusy)
            {
                this.Orchestrator.MergeAll();
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.mergeBatchBusy"));
            }
        }

        /// <summary>
        /// Richiede stop cooperativo dell'operazione corrente
        /// </summary>
        private void DoStop()
        {
            if (this._currentMode == Options.MODE_SPLIT)
            {
                this.SplitOrchestrator.Stop();
                return;
            }

            if (this.Orchestrator.IsBusy)
            {
                this.Orchestrator.RequestStop();
            }
            else
            {
                this.Orchestrator.Log(AppText.T("web.log.noOperationToStop"));
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
            string errorMessage;

            if (this._currentMode == Options.MODE_SPLIT)
            {
                if (this.SplitOrchestrator.ApplyOptions(opts, out errorMessage))
                {
                    this._showConfig = false;
                }
                else if (errorMessage.Length > 0)
                {
                    this.SplitOrchestrator.Log(errorMessage);
                }
            }
            else if (this.Orchestrator.ApplyOptions(opts, out errorMessage))
            {
                this._showConfig = false;
            }
            else if (errorMessage.Length > 0)
            {
                this.Orchestrator.Log(errorMessage);
            }
        }

        /// <summary>
        /// Mostra dialog percorsi tool
        /// </summary>
        private void ShowToolPaths()
        {
            this._showToolPaths = true;
        }

        /// <summary>
        /// Chiude dialog percorsi tool
        /// </summary>
        private void CloseToolPaths()
        {
            this._showToolPaths = false;
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
        /// Mostra dialog impostazioni avanzate
        /// </summary>
        private void ShowAdvancedSettings()
        {
            this._showAdvancedSettings = true;
        }

        /// <summary>
        /// Chiude dialog impostazioni avanzate
        /// </summary>
        private void CloseAdvancedSettings()
        {
            this._showAdvancedSettings = false;
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
        /// Mostra dialog info
        /// </summary>
        private void ShowInfo()
        {
            this._showInfo = true;
        }

        /// <summary>
        /// Chiude dialog info
        /// </summary>
        private void CloseInfo()
        {
            this._showInfo = false;
        }

        /// <summary>
        /// Mostra dialog profili encoding
        /// </summary>
        private void ShowEncodingProfiles()
        {
            this._showEncodingProfiles = true;
        }

        /// <summary>
        /// Chiude dialog profili encoding
        /// </summary>
        private void CloseEncodingProfiles()
        {
            this._showEncodingProfiles = false;
        }

        /// <summary>
        /// Mostra dialog pipeline
        /// </summary>
        private void ShowPipeline()
        {
            this._showPipeline = true;
        }

        /// <summary>
        /// Chiude dialog pipeline
        /// </summary>
        private void ClosePipeline()
        {
            this._showPipeline = false;
        }

        /// <summary>
        /// Chiude tutti i dialog aperti
        /// </summary>
        private void CloseAllDialogs()
        {
            this._showConfig = false;
            this._showToolPaths = false;
            this._showAudioSettings = false;
            this._showAdvancedSettings = false;
            this._showDelay = false;
            this._showEncodingProfiles = false;
            this._showPipeline = false;
            this._showInfo = false;
            this._showContextMenu = false;
            this._showMediaInfo = false;
        }

        /// <summary>
        /// Cambia tema via modulo JS interop e salva in AppSettings
        /// </summary>
        /// <param name="theme">Nome tema kebab-case</param>
        private void ChangeTheme(string theme)
        {
            this._currentTheme = theme;

            // Salva in AppSettings
            AppSettingsService.Instance.Settings.Ui.Theme = theme;
            AppSettingsService.Instance.Save();

            if (this._jsModule != null)
            {
                try
                {
                    _ = this._jsModule.InvokeVoidAsync("setTheme", theme);
                }
                catch
                {
                    // Ignora errori JS durante dispose
                }
            }
        }

        /// <summary>
        /// Cambia lingua UI e salva in AppSettings
        /// </summary>
        /// <param name="language">Codice lingua</param>
        private void ChangeLanguage(string language)
        {
            string normalizedLanguage = AppText.NormalizeLanguage(language);
            if (normalizedLanguage.Length == 0)
            {
                return;
            }

            this._currentLanguage = normalizedLanguage;

            // Salva in AppSettings
            AppSettingsService.Instance.Settings.Ui.Language = normalizedLanguage;
            AppSettingsService.Instance.Save();

            // Aggiorna il catalogo testi della sessione corrente
            AppText.Initialize(normalizedLanguage, normalizedLanguage);

            if (this._jsModule != null)
            {
                try
                {
                    _ = this._jsModule.InvokeVoidAsync("setLanguage", normalizedLanguage);
                }
                catch
                {
                    // Ignora errori JS durante dispose
                }
            }

            this.StateHasChanged();
        }

        #endregion
    }
}
