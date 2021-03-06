﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Timer;
    using AtomicTorch.CBND.CoreMod.ClientOptions.General;
    using AtomicTorch.CBND.CoreMod.Helpers;
    using AtomicTorch.CBND.CoreMod.Systems.Console;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Data;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Chat;
    using AtomicTorch.CBND.GameApi.Logging;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;
    using AtomicTorch.GameEngine.Common.Logging;

    [SuppressMessage("ReSharper", "CanExtractXamlLocalizableStringCSharp")]
    public partial class ConsoleControl : BaseUserControl
    {
        private const int SuggestionsListPageUpDownItemsDistance = 19;

        private static readonly ClientConsoleCommandsHistory CommandsHistory = new ClientConsoleCommandsHistory();

        private static readonly IInputClientService Input = Api.Client.Input;

        private readonly List<LogEntryWithOrigin> logEntriesQueue = new List<LogEntryWithOrigin>();

        private readonly HashSet<LogSeverity> selectedLogSeverities = new HashSet<LogSeverity>();

        private string filterText;

        private bool isAutoScrollToBottom = true;

        private bool isDisplayed = false;

        private bool isUserScrollChange = true;

        private ListView listViewSuggestionsList;

        private SuperObservableCollection<ViewModelLogEntry> logEntriesObservableCollection;

        private ILogEntriesProvider logEntriesProvider;

        private ScrollViewer scrollViewerLog;

        private ConsoleControlSuggestionsProvider suggestionsProvider;

        private TextBox textBoxConsoleInput;

        private TextBox textBoxSearch;

        private ViewModelConsoleControl viewModel;

        public ConsoleControl()
        {
        }

        public static ConsoleControl Instance { get; private set; }

        public bool IsDisplayed
        {
            get => this.isDisplayed;
            set
            {
                if (!value
                    && !this.isLoaded)
                {
                    return;
                }

                if (this.isDisplayed == value)
                {
                    return;
                }

                this.isDisplayed = value;

                this.Visibility = this.isDisplayed ? Visibility.Visible : Visibility.Collapsed;
                this.textBoxConsoleInput.Focusable = this.textBoxSearch.Focusable = this.isDisplayed;

                if (this.isDisplayed)
                {
                    LogOverlayControl.Instance.IsDisplayed = false;

                    this.logEntriesProvider.NewLogEntry += this.LogEntryAddedHandler;

                    this.textBoxConsoleInput.Focus();
                    this.SetInputText(CommandsHistory.TryGetNotCommitedCommand());

                    this.logEntriesObservableCollection = this.viewModel.LogEntriesCollection;
                    this.RefreshFilters();
                    this.RefillList();
                }
                else // when hidden
                {
                    LogOverlayControl.Instance.IsDisplayed = true;

                    this.logEntriesProvider.NewLogEntry -= this.LogEntryAddedHandler;
                    this.logEntriesQueue.Clear();
                }
            }
        }

        public static void Toggle()
        {
            if (!GeneralOptionDeveloperMode.IsEnabled)
            {
                // developer mode off - close console if opened, never open it
                if (Instance != null)
                {
                    Instance.IsDisplayed = false;
                }

                return;
            }

            if (ChatPanel.Instance != null
                && ChatPanel.Instance.IsActive)
            {
                return;
            }

            if (Instance != null)
            {
                Instance.IsDisplayed = !Instance.IsDisplayed;
                return;
            }

            var layoutRootChildren = Api.Client.UI.LayoutRootChildren;
            layoutRootChildren.Add(new ConsoleControl());
        }

        public void Clear()
        {
            this.viewModel.LogEntriesCollection.Clear();
        }

        protected override void InitControl()
        {
            ConsoleLogHelper.InitBrushes(this);

            var itemsControlLogEntries = this.GetByName<ItemsControl>("ItemsControlLogEntries");
            this.scrollViewerLog =
                (ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(itemsControlLogEntries, 0), 0);
            this.listViewSuggestionsList = this.GetByName<ListView>("ListViewSuggestionsList");
            this.textBoxConsoleInput = this.GetByName<TextBox>("TextBoxConsoleInput");
            this.textBoxSearch = this.GetByName<TextBox>("TextBoxSearch");
            var textBlockSuggestionGhost = this.GetByName<TextBlock>("TextBlockSuggestionGhost");

            this.logEntriesProvider = Api.Logger.LogEntriesProvider;

            this.viewModel = new ViewModelConsoleControl(this.OnFilterChanged);
            this.suggestionsProvider = new ConsoleControlSuggestionsProvider(
                this.textBoxConsoleInput,
                textBlockSuggestionGhost,
                this.viewModel);
            this.DataContext = this.viewModel;

            Instance = this;
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            this.textBoxConsoleInput.PreviewKeyUp += this.GenericPreviewKeyUpHandler;
            this.textBoxConsoleInput.PreviewKeyDown += this.ConsoleInputPreviewKeyDownHandler;
            this.textBoxConsoleInput.KeyDown += this.ConsoleInputKeyDownHandler;
            this.textBoxConsoleInput.PreviewKeyUp += this.ConsoleInputPreviewKeyUpHandler;
            this.textBoxSearch.PreviewKeyUp += this.GenericPreviewKeyUpHandler;
            this.scrollViewerLog.ScrollChanged += this.ScrollViewerLogScrollChangedHandler;
            this.listViewSuggestionsList.SelectionChanged += this.ListViewSuggestionsListSelectionChangedHandler;

            this.IsDisplayed = true;
        }

        protected override void OnUnloaded()
        {
            this.textBoxConsoleInput.PreviewKeyUp -= this.GenericPreviewKeyUpHandler;
            this.textBoxConsoleInput.PreviewKeyDown -= this.ConsoleInputPreviewKeyDownHandler;
            this.textBoxConsoleInput.KeyDown -= this.ConsoleInputKeyDownHandler;
            this.textBoxConsoleInput.PreviewKeyUp -= this.ConsoleInputPreviewKeyUpHandler;
            this.textBoxSearch.PreviewKeyUp -= this.GenericPreviewKeyUpHandler;
            this.scrollViewerLog.ScrollChanged -= this.ScrollViewerLogScrollChangedHandler;
            this.listViewSuggestionsList.SelectionChanged -= this.ListViewSuggestionsListSelectionChangedHandler;

            this.IsDisplayed = false;
        }

        private void AddQueuedEntries(bool forceScrollToBottom, bool isReset = false)
        {
            if (this.logEntriesQueue.Count == 0
                && !isReset)
            {
                return;
            }

            this.isUserScrollChange = false;

            var list = isReset
                           ? new List<ViewModelLogEntry>()
                           : (IList<ViewModelLogEntry>)this.logEntriesObservableCollection;

            // process queue and add each entry to log list
            foreach (var logEntryWithSource in this.logEntriesQueue) //.OrderBy(q => q.LogEntry.Date))
            {
                var logEntry = logEntryWithSource.LogEntry;

                if (!this.selectedLogSeverities.Contains(logEntry.Severity))
                {
                    // not selected log severity
                    continue;
                }

                if (this.filterText != null
                    && logEntry.Message.IndexOf(this.filterText, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    // doesn't contain filter text
                    continue;
                }

                list.Add(
                    ConsoleLogHelper.CreateViewModelLogEntry(
                        logEntry,
                        isFromServer: logEntryWithSource.IsServerLogEntry));
            }

            this.logEntriesQueue.Clear();

            if (isReset)
            {
                // refill collection
                this.viewModel.LogEntriesCollection = null;
                this.logEntriesObservableCollection.ClearAndAddRange(list);
                this.viewModel.LogEntriesCollection = this.logEntriesObservableCollection;
                forceScrollToBottom = true;
            }

            this.ScrollToBottom(force: forceScrollToBottom);

            this.isUserScrollChange = true;
        }

        private void AddToClientLog(string message)
        {
            this.logEntriesObservableCollection.Add(
                ConsoleLogHelper.CreateViewModelLogEntry(
                    new LogEntry(message, severity: LogSeverity.Dev),
                    isFromServer: false));
            this.ScrollToBottom(force: true);
        }

        private void ConsoleInputKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key.IsModifier())
            {
                return;
            }

            this.suggestionsProvider.UpdateSuggestionGhostOnly();
        }

        private void ConsoleInputPreviewKeyDownHandler(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            if (key.IsModifier())
            {
                return;
            }

            switch (key)
            {
                case Key.OemTilde:
                {
                    e.Handled = true;
                    break;
                }

                case Key.Up:
                {
                    e.Handled = true;
                    if (this.suggestionsProvider.SelectSuggestion(isPreviousSuggestion: true))
                    {
                        break;
                    }

                    this.SetInputText(
                        CommandsHistory.TryGetPreviousEntry(this.textBoxConsoleInput.Text));
                    break;
                }

                case Key.Down:
                {
                    e.Handled = true;
                    if (this.suggestionsProvider.SelectSuggestion(isPreviousSuggestion: false))
                    {
                        break;
                    }

                    this.SetInputText(
                        CommandsHistory.TryGetNextEntry(this.textBoxConsoleInput.Text));
                    break;
                }

                case Key.PageUp:
                case Key.PageDown:
                {
                    e.Handled = true;
                    this.suggestionsProvider.SelectSuggestion(
                        isPreviousSuggestion: key == Key.PageUp,
                        itemsDistance: SuggestionsListPageUpDownItemsDistance);
                    break;
                }

                case Key.Left:
                case Key.Right:
                    if (this.textBoxConsoleInput.Text.Length == 0)
                    {
                        e.Handled = true;
                    }

                    break;

                case Key.Tab:
                {
                    e.Handled = true;
                    this.suggestionsProvider.OnTab();
                    break;
                }

                case Key.Space:
                    this.suggestionsProvider.UpdateSuggestionGhostOnly();
                    this.suggestionsProvider.ExitSuggestionMode(resetSuggestions: true);
                    this.suggestionsProvider.OnTextChanged();
                    break;

                case Key.Back:
                case Key.Delete:
                    this.suggestionsProvider.UpdateSuggestionGhostOnly();
                    this.suggestionsProvider.ExitSuggestionMode(resetSuggestions: false);
                    this.suggestionsProvider.OnTextChanged();
                    break;

                default:
                    this.suggestionsProvider.UpdateSuggestionGhostOnly();
                    this.suggestionsProvider.ExitSuggestionMode(resetSuggestions: false);
                    break;
            }
        }

        private void ConsoleInputPreviewKeyUpHandler(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            if (key.IsModifier())
            {
                return;
            }

            if (key == Key.Return)
            {
                // execute command
                e.Handled = true;

                if (this.viewModel.SuggestionsListVisibility == Visibility.Visible)
                {
                    this.suggestionsProvider.OnTab();
                    this.suggestionsProvider.ExitSuggestionMode(resetSuggestions: true);
                    this.suggestionsProvider.OnTextChanged();
                    return;
                }

                var consoleCommandText = this.textBoxConsoleInput.Text
                                             .Replace("\r", string.Empty)
                                             .Replace("\n", string.Empty)
                                             .Trim();

                if (!string.IsNullOrEmpty(consoleCommandText))
                {
                    CommandsHistory.AddEntry(consoleCommandText);
                }
                else
                {
                    this.AddToClientLog("Please enter command. Enter \"help\" for commands list.");
                    this.SetInputText(string.Empty);
                    return;
                }

                this.AddToClientLog(consoleCommandText);
                ConsoleCommandsSystem.SharedExecuteConsoleCommand(consoleCommandText);

                this.SetInputText(string.Empty);
            }

            switch (key)
            {
                case Key.Up:
                case Key.Down:
                case Key.PageUp:
                case Key.PageDown:
                    // do not handle these keys there (disallow input)
                    return;
            }

            if (this.suggestionsProvider.IsSuggestionMode
                && (key != Key.Tab
                    && key != Key.LeftShift
                    && key != Key.RightShift
                    || key == Key.Back
                    || key == Key.Delete))
            {
                this.suggestionsProvider.ExitSuggestionMode(resetSuggestions: false);
            }

            if (key != Key.Tab
                && key != Key.LeftShift
                && key != Key.RightShift)
            {
                this.suggestionsProvider.OnTextChanged();
            }
        }

        private void GenericPreviewKeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F
                && Input.IsKeyHeld(InputKey.Control, evenIfHandled: true))
            {
                this.textBoxSearch.Focus();
                this.textBoxSearch.CaretIndex = this.textBoxSearch.Text.Length;
                e.Handled = true;
            }

            this.textBoxSearch.Text = this.textBoxSearch.Text.Replace("`", string.Empty);
        }

        // scroll to display selected item
        private void ListViewSuggestionsListSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            // unfortunately we must do in the next frame
            ClientComponentTimersManager.AddAction(
                0.05,
                () =>
                {
                    var selectedItem = this.listViewSuggestionsList.SelectedItem;
                    if (selectedItem != null)
                    {
                        this.listViewSuggestionsList.ScrollIntoView(selectedItem);
                    }
                });
        }

        private void LogEntryAddedHandler(LogEntryWithOrigin logEntryWithOrigin)
        {
            this.logEntriesQueue.Add(logEntryWithOrigin);
            this.AddQueuedEntries(forceScrollToBottom: false);
        }

        private void OnFilterChanged()
        {
            this.RefreshFilters();
            this.RefillList();
        }

        private void RefillList()
        {
            this.logEntriesQueue.Clear();
            this.logEntriesQueue.AddRange(this.logEntriesProvider.Log);
            this.AddQueuedEntries(forceScrollToBottom: true, isReset: true);
        }

        private void RefreshFilters()
        {
            this.selectedLogSeverities.Clear();

            if (this.viewModel.DisplaySeverityDebug)
            {
                this.selectedLogSeverities.Add(LogSeverity.Debug);
            }

            if (this.viewModel.DisplaySeverityInfo)
            {
                this.selectedLogSeverities.Add(LogSeverity.Info);
            }

            if (this.viewModel.DisplaySeverityImportant)
            {
                this.selectedLogSeverities.Add(LogSeverity.Important);
            }

            if (this.viewModel.DisplaySeverityWarning)
            {
                this.selectedLogSeverities.Add(LogSeverity.Warning);
            }

            if (this.viewModel.DisplaySeverityError)
            {
                this.selectedLogSeverities.Add(LogSeverity.Error);
            }

            if (this.viewModel.DisplaySeverityDev)
            {
                this.selectedLogSeverities.Add(LogSeverity.Dev);
            }

            this.filterText = this.viewModel.FilterText;
        }

        private void ScrollToBottom(bool force)
        {
            if (force)
            {
                this.isAutoScrollToBottom = true;
            }
            else if (!this.isAutoScrollToBottom)
            {
                return;
            }

            this.isUserScrollChange = false;
            this.scrollViewerLog.ScrollToBottom();
            this.isUserScrollChange = true;
        }

        private void ScrollViewerLogScrollChangedHandler(object sender, ScrollChangedEventArgs e)
        {
            if (!this.isUserScrollChange)
            {
                return;
            }

            // when user scrolled the log
            var verticalOffset = this.scrollViewerLog.VerticalOffset;
            var scrollableHeight = this.scrollViewerLog.ScrollableHeight;
            this.isAutoScrollToBottom = verticalOffset == scrollableHeight;
        }

        private void SetInputText(string text)
        {
            this.textBoxConsoleInput.SetTextAndMoveCaret(text);
            this.suggestionsProvider.UpdateSuggestionGhostOnly();
        }
    }
}