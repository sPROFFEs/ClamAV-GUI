using ClamAVGui.Models;
using ClamAVGui.Services;
using Ookii.Dialogs.Wpf;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;

namespace ClamAVGui.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string AppName = "ClamAV GUI";
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private readonly QuarantineService _quarantineService;
        private readonly SchedulerService _schedulerService;
        private string? _clamAVPath;
        private CancellationTokenSource? _scanCts;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingMonitoringDebounce = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _activeMonitoringScans = new(StringComparer.OrdinalIgnoreCase);

        // Status Properties
        private string _clamAVStatusText = "Initializing...";
        public string ClamAVStatusText { get => _clamAVStatusText; set { _clamAVStatusText = value; OnPropertyChanged(); } }

        private string? _virusDefinitionsVersion;
        public string? VirusDefinitionsVersion { get => _virusDefinitionsVersion; set { _virusDefinitionsVersion = value; OnPropertyChanged(); } }

        private string _clamAVDaemonStats = "N/A";
        public string ClamAVDaemonStats { get => _clamAVDaemonStats; set { _clamAVDaemonStats = value; OnPropertyChanged(); } }

        private DateTime? _lastUpdateTime;
        public DateTime? LastUpdateTime { get => _lastUpdateTime; set { _lastUpdateTime = value; OnPropertyChanged(); } }

        private int _totalScans;
        public int TotalScans { get => _totalScans; set { _totalScans = value; OnPropertyChanged(); } }

        private int _totalInfectedFiles;
        public int TotalInfectedFiles { get => _totalInfectedFiles; set { _totalInfectedFiles = value; OnPropertyChanged(); } }

        private bool _isClamAVConfigured;
        public bool IsClamAVConfigured { get => _isClamAVConfigured; set { _isClamAVConfigured = value; OnPropertyChanged(); } }

        private bool _isClamDRunning;
        public bool IsClamDRunning { get => _isClamDRunning; set { _isClamDRunning = value; OnPropertyChanged(); } }

        private bool _isDaemonBusy;
        public bool IsDaemonBusy { get => _isDaemonBusy; set { _isDaemonBusy = value; OnPropertyChanged(); } }

        private bool _isConfigInitialized;
        public bool IsConfigInitialized { get => _isConfigInitialized; set { _isConfigInitialized = value; OnPropertyChanged(); } }

        // Options
        public ScanOptions Options { get; set; }

        private ScanSummary? _currentScanSummary;
        public ScanSummary? CurrentScanSummary { get => _currentScanSummary; set { _currentScanSummary = value; OnPropertyChanged(); } }

        // Commands
        public ICommand SelectPathCommand { get; }
        public ICommand ChangePathCommand { get; }
        public ICommand InitializeConfigCommand { get; }
        public ICommand StartClamdCommand { get; }
        public ICommand StopClamdCommand { get; }
        public ICommand CheckDaemonStatusCommand { get; }
        public ICommand UpdateSignaturesCommand { get; }
        public ICommand ScanFolderCommand { get; }
        public ICommand ScanFileCommand { get; }
        public ICommand LoadHistoryCommand { get; }
        public ICommand ViewHistoryEventCommand { get; }
        public ICommand DeleteHistoryEventCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        // Other UI Properties
        public ObservableCollection<ScanResult> ScanResults { get; } = new ObservableCollection<ScanResult>();
        public ObservableCollection<HistoryEvent> HistoryEvents { get; } = new ObservableCollection<HistoryEvent>();
        public ICollectionView HistoryView { get; }
        private string _historyFilterText = string.Empty;
        public string HistoryFilterText
        {
            get => _historyFilterText;
            set
            {
                if (_historyFilterText == value) return;
                _historyFilterText = value;
                OnPropertyChanged();
                HistoryView.Refresh();
            }
        }

        private string _historyFilterType = "All";
        public string HistoryFilterType
        {
            get => _historyFilterType;
            set
            {
                if (_historyFilterType == value) return;
                _historyFilterType = value;
                OnPropertyChanged();
                HistoryView.Refresh();
            }
        }

        public ObservableCollection<string> HistoryFilterTypes { get; } = new ObservableCollection<string> { "All", "Scan", "Update", "Update Failed", "Config Initialized" };
        private string _updateOutputText = "";
        public string UpdateOutputText { get => _updateOutputText; set { _updateOutputText = value; OnPropertyChanged(); } }
        private string _updateStatusText = "";
        public string UpdateStatusText { get => _updateStatusText; set { _updateStatusText = value; OnPropertyChanged(); } }
        private bool _isUpdating;
        public bool IsUpdating { get => _isUpdating; set { _isUpdating = value; OnPropertyChanged(); } }
        private bool _isScanning;
        public bool IsScanning { get => _isScanning; set { _isScanning = value; OnPropertyChanged(); } }

        // Monitoring Properties
        private bool _isMonitoringActive;
        public bool IsMonitoringActive { get => _isMonitoringActive; set { _isMonitoringActive = value; OnPropertyChanged(); } }
        public ObservableCollection<string> MonitoredPaths { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MonitoringLogEntries { get; } = new ObservableCollection<string>();
        private readonly List<FileSystemWatcher> _monitoringWatchers = new List<FileSystemWatcher>();
        public ObservableCollection<string> FileTypeFilters { get; } = new ObservableCollection<string>();
        private string _newFilterText = string.Empty;
        public string NewFilterText { get => _newFilterText; set { _newFilterText = value; OnPropertyChanged(); } }
        public ObservableCollection<string> ExcludedPaths { get; } = new ObservableCollection<string>();

        private string? _lastKnownVirusCount;

        // Application Settings
        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (_startWithWindows != value)
                {
                    SetStartup(value);
                    _startWithWindows = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand ExportLogCommand { get; }
        public ICommand AddMonitoredPathCommand { get; }
        public ICommand RemoveMonitoredPathCommand { get; }
        public ICommand AddExcludedPathCommand { get; }
        public ICommand RemoveExcludedPathCommand { get; }
        public ICommand AddFilterCommand { get; }
        public ICommand RemoveFilterCommand { get; }
        public ICommand PingDaemonCommand { get; }
        public ICommand ReloadDatabaseCommand { get; }
        public ICommand GetVersionCommand { get; }
        public ICommand ShutdownDaemonCommand { get; }
        public ICommand GetStatsCommand { get; }
        public ICommand GetVersionCommandsCommand { get; }
        public ICommand CancelScanCommand { get; }
        public ICommand ExportHistoryCommand { get; }
        public ICommand RunHealthCheckCommand { get; }
        public ICommand LoadQuarantineCommand { get; }
        public ICommand RemoveQuarantineItemCommand { get; }
        public ICommand RestoreQuarantineItemCommand { get; }
        public ICommand ScanWithDaemonFolderCommand { get; }
        public ICommand ScheduleDailyScanCommand { get; }
        public ICommand RemoveScheduleCommand { get; }
        public ICommand BrowseScheduledPathCommand { get; }

        public ObservableCollection<QuarantineItem> QuarantineItems { get; } = new ObservableCollection<QuarantineItem>();
        private string _scheduledScanPath = string.Empty;
        public string ScheduledScanPath { get => _scheduledScanPath; set { _scheduledScanPath = value; OnPropertyChanged(); } }

        private string _scheduledScanTime = "02:00";
        public string ScheduledScanTime { get => _scheduledScanTime; set { _scheduledScanTime = value; OnPropertyChanged(); } }

        private bool _isScheduledScanEnabled;
        public bool IsScheduledScanEnabled { get => _isScheduledScanEnabled; set { _isScheduledScanEnabled = value; OnPropertyChanged(); } }


        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _historyService = new HistoryService();
            _quarantineService = new QuarantineService();
            _schedulerService = new SchedulerService();
            Options = new ScanOptions();
            HistoryView = CollectionViewSource.GetDefaultView(HistoryEvents);
            HistoryView.Filter = FilterHistory;

            SelectPathCommand = new AsyncRelayCommand(SelectAndSavePath);
            ChangePathCommand = new AsyncRelayCommand(SelectAndSavePath);
            InitializeConfigCommand = new AsyncRelayCommand(InitializeConfig, () => IsClamAVConfigured);
            StartClamdCommand = new AsyncRelayCommand(StartClamd, () => IsClamAVConfigured && !IsClamDRunning);
            StopClamdCommand = new AsyncRelayCommand(StopClamd, () => IsClamAVConfigured && IsClamDRunning);
            CheckDaemonStatusCommand = new AsyncRelayCommand(RefreshDaemonStatusAsync);
            UpdateSignaturesCommand = new AsyncRelayCommand(UpdateSignatures, () => IsClamAVConfigured && !IsUpdating);
            ScanFolderCommand = new AsyncRelayCommand(() => ScanPath(true), () => IsClamAVConfigured && !IsScanning);
            ScanFileCommand = new AsyncRelayCommand(() => ScanPath(false), () => IsClamAVConfigured && !IsScanning);
            LoadHistoryCommand = new AsyncRelayCommand(LoadHistory);
            ViewHistoryEventCommand = new RelayCommand<HistoryEvent>(ViewHistoryEvent);
            DeleteHistoryEventCommand = new AsyncRelayCommand<HistoryEvent>(DeleteHistoryEvent);
            ClearHistoryCommand = new AsyncRelayCommand(ClearHistory, () => HistoryEvents.Any());
            StartMonitoringCommand = new RelayCommand(StartMonitoring, () => IsClamAVConfigured && !IsMonitoringActive && MonitoredPaths.Any());
            StopMonitoringCommand = new RelayCommand(StopMonitoring, () => IsClamAVConfigured && IsMonitoringActive);
            ExportLogCommand = new AsyncRelayCommand(ExportLog, () => MonitoringLogEntries.Any());
            AddMonitoredPathCommand = new RelayCommand(AddMonitoredPath);
            RemoveMonitoredPathCommand = new RelayCommand<string>(RemoveMonitoredPath, (p) => p != null);
            AddExcludedPathCommand = new RelayCommand<string>(AddExcludedPath);
            RemoveExcludedPathCommand = new RelayCommand<string>(RemoveExcludedPath, (p) => p != null);
            AddFilterCommand = new RelayCommand(AddFilter, () => !string.IsNullOrWhiteSpace(NewFilterText));
            RemoveFilterCommand = new RelayCommand<string>(RemoveFilter);
            PingDaemonCommand = new AsyncRelayCommand(PingDaemon, () => IsClamAVConfigured && IsClamDRunning);
            ReloadDatabaseCommand = new AsyncRelayCommand(ReloadDatabase, () => IsClamAVConfigured && IsClamDRunning);
            GetVersionCommand = new AsyncRelayCommand(GetVersion, () => IsClamAVConfigured && IsClamDRunning);
            ShutdownDaemonCommand = new AsyncRelayCommand(ShutdownDaemon, () => IsClamAVConfigured && IsClamDRunning);
            GetStatsCommand = new AsyncRelayCommand(GetStats, () => IsClamAVConfigured && IsClamDRunning);
            GetVersionCommandsCommand = new AsyncRelayCommand(GetVersionCommands, () => IsClamAVConfigured && IsClamDRunning);
            CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
            ExportHistoryCommand = new AsyncRelayCommand(ExportHistory, () => HistoryEvents.Any());
            RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheck, () => IsClamAVConfigured);
            LoadQuarantineCommand = new AsyncRelayCommand(LoadQuarantine);
            RemoveQuarantineItemCommand = new AsyncRelayCommand<QuarantineItem>(RemoveQuarantineItem);
            RestoreQuarantineItemCommand = new AsyncRelayCommand<QuarantineItem>(RestoreQuarantineItem);
            ScanWithDaemonFolderCommand = new AsyncRelayCommand(ScanFolderUsingDaemon, () => IsClamAVConfigured && IsClamDRunning);
            ScheduleDailyScanCommand = new AsyncRelayCommand(ScheduleDailyScan, () => IsClamAVConfigured);
            RemoveScheduleCommand = new AsyncRelayCommand(RemoveScheduledScan, () => IsClamAVConfigured);
            BrowseScheduledPathCommand = new RelayCommand(BrowseScheduledPath, () => IsClamAVConfigured);


            _ = LoadInitialPathAsync();

            _startWithWindows = IsStartupEnabled();
            MonitoredPaths.CollectionChanged += async (s, e) => await _settingsService.SaveMonitoredPathsAsync(MonitoredPaths);
            ExcludedPaths.CollectionChanged += async (s, e) => await _settingsService.SaveMonitoringExclusionsAsync(ExcludedPaths);
            FileTypeFilters.CollectionChanged += async (s, e) => await _settingsService.SaveMonitoringFiltersAsync(FileTypeFilters);
        }

        private async Task LoadInitialPathAsync()
        {
            var path = await _settingsService.LoadPathAsync();
            await ValidateAndSetPath(path, true);
            await LoadHistory();
            await LoadQuarantine();
            IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
            var monitoredPaths = await _settingsService.LoadMonitoredPathsAsync();
            foreach (var p in monitoredPaths)
            {
                MonitoredPaths.Add(p);
            }
            var filters = await _settingsService.LoadMonitoringFiltersAsync();
            foreach (var f in filters)
            {
                FileTypeFilters.Add(f);
            }
            var excludedPaths = await _settingsService.LoadMonitoringExclusionsAsync();
            foreach (var p in excludedPaths)
            {
                ExcludedPaths.Add(p);
            }
        }

        private async Task ValidateAndSetPath(string? path, bool isInitialLoad = false)
        {
            if (ClamAVService.IsClamAVInstalled(path ?? string.Empty))
            {
                _clamAVPath = path;
                IsClamAVConfigured = true;
                ClamAVStatusText = $"ClamAV configured at: {path}";
                IsConfigInitialized = false; // Reset this when path changes

                await RefreshDaemonStatusAsync();

                if (isInitialLoad && !IsClamDRunning)
                {
                    await StartClamd();
                }
            }
            else
            {
                _clamAVPath = null;
                IsClamAVConfigured = false;
                ClamAVStatusText = "ClamAV is not configured. Please select the folder where you extracted ClamAV.";
                IsClamDRunning = false;
                IsConfigInitialized = false;
            }
        }

        private async Task SelectAndSavePath()
        {
            var dialog = new VistaFolderBrowserDialog { Description = "Please select the folder where you extracted ClamAV.", UseDescriptionForTitle = true };
            if (dialog.ShowDialog() == true)
            {
                if (ClamAVService.IsClamAVInstalled(dialog.SelectedPath))
                {
                    await _settingsService.SavePathAsync(dialog.SelectedPath);
                    await ValidateAndSetPath(dialog.SelectedPath);
                }
                else
                {
                    MessageBox.Show("The selected folder is not a valid ClamAV installation.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task InitializeConfig()
        {
            if (!IsClamAVConfigured || _clamAVPath == null) return;

            var confirmResult = MessageBox.Show(
                "This will overwrite your existing freshclam.conf and clamd.conf with the default templates. Any custom changes, including monitoring paths, will be lost.\n\nAre you sure you want to continue?",
                "Confirm Configuration Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes) return;

            var result = await ClamAVService.InitializeConfigurationAsync(_clamAVPath);
            MessageBox.Show(result, "Configuration Result", MessageBoxButton.OK, MessageBoxImage.Information);
            await _historyService.LogEventAsync("Config Initialized", result);
            if (!result.StartsWith("Error"))
            {
                IsConfigInitialized = true;
            }
        }

        private async Task RefreshDaemonStatusAsync()
        {
            var pingResult = await ClamAVService.PingDaemonAsync();
            IsClamDRunning = pingResult.Contains("PONG", StringComparison.OrdinalIgnoreCase);
            await UpdateDaemonInfoAsync();
            await UpdateDashboardAsync();
        }
        private async Task UpdateDaemonInfoAsync()
        {
            if (IsClamDRunning)
            {
                var versionString = await ClamAVService.GetVersionAsync();
                var statsString = await ClamAVService.GetStatsAsync();

                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    var versionParts = versionString.Split('/');
                    if (versionParts.Length >= 2)
                    {
                        VirusDefinitionsVersion = versionParts[1].Trim();
                    }
                    else
                    {
                        VirusDefinitionsVersion = versionString;
                    }
                }

                if (!string.IsNullOrWhiteSpace(statsString))
                {
                    ClamAVDaemonStats = statsString;
                }
                else
                {
                    ClamAVDaemonStats = "Could not retrieve stats.";
                }
            }
            else
            {
                VirusDefinitionsVersion = "N/A";
                ClamAVDaemonStats = "Daemon not running";
            }
        }


        private async Task StartClamd()
        {
            if (!IsClamAVConfigured || _clamAVPath == null) return;
            IsDaemonBusy = true;
            ClamAVStatusText = "Attempting to start the ClamAV daemon...";
            IsClamDRunning = false; // Assume it's not running until successfully started
            try
            {
                await ClamAVService.StartClamdAsync(_clamAVPath, Options);
                ClamAVStatusText = "ClamAV daemon is running.";
            }
            catch (Exception ex)
            {
                ClamAVStatusText = "ClamAV daemon failed to start.";
                MessageBox.Show(ex.Message, "ClamAV Daemon Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDaemonBusy = false;
                await RefreshDaemonStatusAsync();
                if (!IsClamDRunning)
                {
                    ClamAVStatusText = "ClamAV daemon is not running.";
                }
            }
        }

        private async Task StopClamd()
        {
            IsDaemonBusy = true;
            ClamAVStatusText = "Attempting to stop the ClamAV daemon...";
            try
            {
                await ClamAVService.StopClamdAsync();
            }
            finally
            {
                IsDaemonBusy = false;
                await RefreshDaemonStatusAsync();
                ClamAVStatusText = "ClamAV daemon is not running.";
            }
        }

        private async Task UpdateSignatures()
        {
            if (!IsClamAVConfigured || _clamAVPath == null) return;
            IsUpdating = true;
            UpdateStatusText = "Downloading database, this may take a few minutes...";
            UpdateOutputText = ""; // Clear previous output
            await _historyService.LogEventAsync("Update", "Signature update process started.");
            try
            {
                var (output, error) = await ClamAVService.RunFreshclamAsync(_clamAVPath);

                if (!string.IsNullOrWhiteSpace(error) || output.Contains("ERROR"))
                {
                    UpdateStatusText = "An error occurred during the update.";
                    await _historyService.LogEventAsync("Update Failed", $"Error: {error}\nOutput: {output}");
                }
                else if (output.Contains("up-to-date"))
                {
                    UpdateStatusText = "Virus database is already up-to-date.";
                    await _historyService.LogEventAsync("Update", "Database already up-to-date.");
                }
                else
                {
                    UpdateStatusText = "Update successful!";
                    await _historyService.LogEventAsync("Update", "Signature update process finished.");
                }

                UpdateOutputText = $"Output:\n{output}\n\nErrors:\n{error}";

                var sigsMatch = System.Text.RegularExpressions.Regex.Match(output, @"sigs: (\d+)");
                if (sigsMatch.Success)
                {
                    _lastKnownVirusCount = sigsMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"An unexpected error occurred during update: {ex.Message}";
                UpdateStatusText = "An unexpected error occurred.";
                UpdateOutputText = errorMessage;
                await _historyService.LogEventAsync("Update Failed", ex.ToString());
            }
            finally
            {
                IsUpdating = false;
                await UpdateDashboardAsync();
            }
        }

        public async void ScanPathFromCommandLine(string path)
        {
            await ScanPath(path);
        }

        private async Task ScanPath(bool isFolder)
        {
            string? pathToScan = null;
            if (isFolder)
            {
                var dialog = new VistaFolderBrowserDialog();
                if (dialog.ShowDialog() == true) pathToScan = dialog.SelectedPath;
            }
            else
            {
                var dialog = new VistaOpenFileDialog();
                if (dialog.ShowDialog() == true) pathToScan = dialog.FileName;
            }

            await ScanPath(pathToScan);
        }

        private async Task ScanPath(string? pathToScan)
        {
            if (string.IsNullOrEmpty(pathToScan) || _clamAVPath == null) return;

            IsScanning = true;
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();
            ScanResults.Clear();
            CurrentScanSummary = null;
            await _historyService.LogEventAsync("Scan", $"Scan started for: {pathToScan}");
            var infectedFiles = 0;
            var summary = new ScanSummary();
            var inSummarySection = false;
            var scannedFilesCount = 0;
            var detectedThreats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> quarantineBefore = new(StringComparer.OrdinalIgnoreCase);

            if (Options.MoveToQuarantine && !string.IsNullOrWhiteSpace(Options.QuarantinePath) && Directory.Exists(Options.QuarantinePath))
            {
                quarantineBefore = Directory.EnumerateFiles(Options.QuarantinePath, "*", SearchOption.AllDirectories)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            // This is the root node for the entire scan operation.
            ScanResult? scanRootNode = null;
            bool isFolder = Directory.Exists(pathToScan);

            if (isFolder)
            {
                scanRootNode = new ScanResult
                {
                    FilePath = pathToScan,
                    IsFolder = true,
                    Status = "Scanning..." // Initial status for the folder
                };
                ScanResults.Add(scanRootNode);
            }

            try
            {
                Action<string> processLineAction = line =>
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        return;
                    }

                    if (line.Contains("----------- SCAN SUMMARY -----------", StringComparison.OrdinalIgnoreCase))
                    {
                        inSummarySection = true;
                        return;
                    }

                    if (inSummarySection)
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length != 2) return;
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "Known viruses": summary.KnownViruses = value; break;
                            case "Engine version": summary.EngineVersion = value; break;
                            case "Scanned directories": summary.ScannedDirectories = value; break;
                            case "Scanned files": summary.ScannedFiles = value; break;
                            case "Infected files": summary.InfectedFiles = value; break;
                            case "Time": summary.TimeTaken = value; break;
                        }
                    }
                    else
                    {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var parsed = TryParseScanResultLine(line);
                                if (parsed == null)
                                {
                                    return;
                                }

                                var (filePath, status) = parsed.Value;

                                if (status.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
                                {
                                    infectedFiles++;
                                    var threatName = status[..^"FOUND".Length].Trim();
                                    detectedThreats[filePath] = string.IsNullOrWhiteSpace(threatName) ? "Unknown" : threatName;
                                }

                                var fileResult = new ScanResult { FilePath = filePath, Status = status, IsFolder = false };

                                if (scanRootNode != null)
                                {
                                    scanRootNode.Children.Add(fileResult);
                                }
                                else
                                {
                                    ScanResults.Add(fileResult);
                                }

                                if (status.EndsWith("OK", StringComparison.OrdinalIgnoreCase) ||
                                    status.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                                {
                                    scannedFilesCount++;
                                }
                            });
                    }
                };

                // Per user request, all manual scans will now use clamscan for reliability.
                // The daemon will only be used for On-Access (real-time) monitoring.
                await ClamAVService.RunClamscanAsync(_clamAVPath, pathToScan, Options, processLineAction, _scanCts.Token);

                // Update the status of the root folder node after the scan is complete
                if (scanRootNode != null)
                {
                    scanRootNode.Status = $"Scan Complete. Found {scanRootNode.Children.Count(c => c.Status.EndsWith("FOUND"))} infected file(s).";
                }

                if (IsClamDRunning)
                {
                    summary.ScannedFiles = isFolder ? scannedFilesCount.ToString() : "1";
                    summary.ScannedDirectories = isFolder ? "1" : "0";
                    summary.KnownViruses = _lastKnownVirusCount ?? "N/A";
                }

                CurrentScanSummary = summary;

                if (Options.MoveToQuarantine && !string.IsNullOrWhiteSpace(Options.QuarantinePath) && Directory.Exists(Options.QuarantinePath))
                {
                    var afterFiles = Directory.EnumerateFiles(Options.QuarantinePath, "*", SearchOption.AllDirectories)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var newQuarantineFiles = afterFiles.Except(quarantineBefore).ToList();

                    if (newQuarantineFiles.Any())
                    {
                        var newItems = newQuarantineFiles.Select(path => new QuarantineItem
                        {
                            QuarantinePath = path,
                            OriginalPath = detectedThreats.Keys.FirstOrDefault(k => string.Equals(Path.GetFileName(k), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) ?? "Unknown",
                            ThreatName = detectedThreats.Values.FirstOrDefault() ?? "Detected by clamscan",
                            QuarantinedAt = DateTime.Now,
                            Notes = "Moved automatically by clamscan --move"
                        }).ToList();

                        await _quarantineService.AddItemsAsync(newItems);
                        await LoadQuarantine();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await _historyService.LogEventAsync("Scan", $"Scan cancelled for: {pathToScan}");
            }
            finally
            {
                await _historyService.LogEventAsync("Scan", $"Scan finished for: {pathToScan}. Infected files: {infectedFiles}.");
                IsScanning = false;
                _scanCts?.Dispose();
                _scanCts = null;
                await UpdateDashboardAsync();
            }
        }

        private async Task LoadHistory()
        {
            HistoryEvents.Clear();
            var events = await _historyService.LoadHistoryAsync();
            foreach (var ev in events)
            {
                HistoryEvents.Add(ev);
            }
            await UpdateDashboardAsync();
        }

        private Task UpdateDashboardAsync()
        {
            var scanEvents = HistoryEvents.Where(e => e.EventType == "Scan").ToList();
            TotalScans = scanEvents.Count;
            TotalInfectedFiles = scanEvents.Select(e =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(e.Details, @"Infected files: (\d+)");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }).Sum();

            var lastUpdateEvent = HistoryEvents.FirstOrDefault(e => e.EventType == "Update");
            LastUpdateTime = lastUpdateEvent?.Timestamp;

            // This is a bit of a hack. We'll get the engine version from the last scan summary.
            // A better approach would be to get it directly, but that's more involved.
            if (CurrentScanSummary != null)
            {
                //VirusDefinitionsVersion = CurrentScanSummary.EngineVersion;
            }

            return Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ViewHistoryEvent(HistoryEvent? historyEvent)
        {
            if (historyEvent == null) return;

            var message = $"Timestamp: {historyEvent.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                          $"Event: {historyEvent.EventType}\n\n" +
                          $"Details:\n{historyEvent.Details}";

            MessageBox.Show(message, "History Event Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task DeleteHistoryEvent(HistoryEvent? historyEvent)
        {
            if (historyEvent == null) return;

            var result = MessageBox.Show("Are you sure you want to delete this history entry?",
                                         "Confirm Deletion",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            await _historyService.DeleteHistoryEventAsync(historyEvent);
            HistoryEvents.Remove(historyEvent);
        }

        private async Task ClearHistory()
        {
            var result = MessageBox.Show("Are you sure you want to delete all history entries? This action cannot be undone.",
                                         "Confirm Clear History",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            await _historyService.ClearHistoryAsync();
            HistoryEvents.Clear();
        }

        private void AddMonitoredPath()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Please select a folder to monitor.",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == true)
            {
                if (!MonitoredPaths.Contains(dialog.SelectedPath))
                {
                    MonitoredPaths.Add(dialog.SelectedPath);
                }
            }
        }

        private void RemoveMonitoredPath(string? path)
        {
            if (path != null)
            {
                MonitoredPaths.Remove(path);
            }
        }

        private void AddExcludedPath(string? type)
        {
            if (type == "File")
            {
                var dialog = new VistaOpenFileDialog { Title = "Select a file to exclude from monitoring." };
                if (dialog.ShowDialog() == true)
                {
                    if (!ExcludedPaths.Contains(dialog.FileName))
                    {
                        ExcludedPaths.Add(dialog.FileName);
                    }
                }
            }
            else // Folder
            {
                var dialog = new VistaFolderBrowserDialog { Description = "Select a folder to exclude from monitoring.", UseDescriptionForTitle = true };
                if (dialog.ShowDialog() == true)
                {
                    if (!ExcludedPaths.Contains(dialog.SelectedPath))
                    {
                        ExcludedPaths.Add(dialog.SelectedPath);
                    }
                }
            }
        }

        private void RemoveExcludedPath(string? path)
        {
            if (path != null)
            {
                ExcludedPaths.Remove(path);
            }
        }

        private void AddFilter()
        {
            var newFilter = NormalizeFilter(NewFilterText);
            if (!string.IsNullOrWhiteSpace(newFilter) && !FileTypeFilters.Contains(newFilter))
            {
                FileTypeFilters.Add(newFilter);
            }
            NewFilterText = string.Empty;
        }

        private void RemoveFilter(string? filter)
        {
            if (filter != null)
            {
                FileTypeFilters.Remove(filter);
            }
        }

        private void StartMonitoring()
        {
            if (_clamAVPath == null) return;
            if (!IsClamDRunning)
            {
                MessageBox.Show("The daemon is not running. Please start the daemon from the Settings tab before enabling monitoring.", "Daemon Not Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StopMonitoring(); // Stop any existing watchers first
            MonitoringLogEntries.Clear();

            foreach (var path in MonitoredPaths)
            {
                if (!Directory.Exists(path))
                {
                    MonitoringLogEntries.Add($"Skipped (not found): {path}");
                    continue;
                }

                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                watcher.Created += OnFileCreatedOrChanged;
                watcher.Changed += OnFileCreatedOrChanged;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;
                watcher.Error += OnWatcherError;
                watcher.EnableRaisingEvents = true;
                _monitoringWatchers.Add(watcher);
                MonitoringLogEntries.Add($"Monitoring started for: {path}");
            }

            IsMonitoringActive = true;
        }

        private bool IsWildcardMatch(string input, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            var normalizedPattern = NormalizeFilter(pattern);
            var regexPattern = "^" + Regex.Escape(normalizedPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }

        private void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return;
            ScheduleDebouncedMonitoringScan(e.FullPath);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MonitoringLogEntries.Add($"DELETED: {e.FullPath}");
            });
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MonitoringLogEntries.Add($"RENAMED: {e.OldFullPath} -> {e.FullPath}");
            });

            if (File.Exists(e.FullPath))
            {
                ScheduleDebouncedMonitoringScan(e.FullPath);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MonitoringLogEntries.Add($"WATCHER ERROR: {e.GetException().Message}");
            });
        }

        private void ScheduleDebouncedMonitoringScan(string fullPath)
        {
            if (!ShouldScanPath(fullPath))
            {
                return;
            }

            var key = NormalizePathSafe(fullPath);
            var cts = new CancellationTokenSource();

            if (_pendingMonitoringDebounce.TryGetValue(key, out var previous))
            {
                previous.Cancel();
                previous.Dispose();
            }

            _pendingMonitoringDebounce[key] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(700, cts.Token);
                    await ScanFileForMonitoringAsync(fullPath, cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _pendingMonitoringDebounce.TryRemove(key, out _);
                    cts.Dispose();
                }
            });
        }

        private async Task ScanFileForMonitoringAsync(string fullPath, CancellationToken cancellationToken)
        {
            var normalized = NormalizePathSafe(fullPath);
            if (!_activeMonitoringScans.TryAdd(normalized, 0))
            {
                return;
            }

            try
            {
                if (!await WaitForFileReadyAsync(fullPath, cancellationToken))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MonitoringLogEntries.Add($"SKIPPED (locked): {fullPath}");
                    });
                    return;
                }

                var result = await ClamAVService.ScanFileWithDaemonAsync(fullPath);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MonitoringLogEntries.Add(result);
                });
            }
            finally
            {
                _activeMonitoringScans.TryRemove(normalized, out _);
            }
        }

        private static async Task<bool> WaitForFileReadyAsync(string path, CancellationToken cancellationToken)
        {
            for (var i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(200, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }

            return false;
        }

        private bool ShouldScanPath(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                return false;
            }

            if (IsExcludedPath(fullPath))
            {
                return false;
            }

            if (!FileTypeFilters.Any())
            {
                return true;
            }

            var fileName = Path.GetFileName(fullPath);
            return FileTypeFilters.Any(filter => IsWildcardMatch(fileName, filter));
        }

        private bool IsExcludedPath(string fullPath)
        {
            var normalizedTarget = NormalizePathSafe(fullPath);

            foreach (var excluded in ExcludedPaths)
            {
                var normalizedExcluded = NormalizePathSafe(excluded);
                if (string.IsNullOrWhiteSpace(normalizedExcluded))
                {
                    continue;
                }

                if (string.Equals(normalizedTarget, normalizedExcluded, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (Directory.Exists(excluded))
                {
                    var folderPrefix = normalizedExcluded.EndsWith(Path.DirectorySeparatorChar)
                        ? normalizedExcluded
                        : normalizedExcluded + Path.DirectorySeparatorChar;

                    if (normalizedTarget.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizePathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private void StopMonitoring()
        {
            foreach (var pending in _pendingMonitoringDebounce.Values)
            {
                pending.Cancel();
                pending.Dispose();
            }
            _pendingMonitoringDebounce.Clear();

            foreach (var watcher in _monitoringWatchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
            }
            _monitoringWatchers.Clear();

            if (IsMonitoringActive) // Only log "stopped" if it was active
            {
                MonitoringLogEntries.Add("Monitoring stopped.");
            }
            IsMonitoringActive = false;
        }

        private async Task ExportLog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"ClamAV_Monitoring_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var logContent = string.Join(Environment.NewLine, MonitoringLogEntries);
                    await File.WriteAllTextAsync(dialog.FileName, logContent);
                    MessageBox.Show("Log exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export log: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void OpenContainingFolder(ScanResult? scanResult)
        {
            if (scanResult == null || string.IsNullOrWhiteSpace(scanResult.FilePath)) return;

            if (!System.IO.File.Exists(scanResult.FilePath))
            {
                MessageBox.Show("The file does not exist or may have been moved/deleted.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var argument = $"/select, \"{scanResult.FilePath}\"";
            System.Diagnostics.Process.Start("explorer.exe", argument);
        }

        private void SetStartup(bool enable)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key == null) return; // Should not happen

                if (enable)
                {
                    // Use Environment.ProcessPath to get the path of the running .exe
                    var executablePath = Environment.ProcessPath;
                    if (executablePath != null)
                    {
                        key.SetValue(AppName, $"\"{executablePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update startup settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeFilter(string filter)
        {
            var normalized = filter?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.StartsWith('.'))
            {
                return $"*{normalized}";
            }

            return normalized;
        }

        private static (string FilePath, string Status)? TryParseScanResultLine(string line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            var match = Regex.Match(trimmed, @"^(?<path>[A-Za-z]:\\.*?):\s*(?<status>.+)$");
            if (match.Success)
            {
                return (match.Groups["path"].Value.Trim(), match.Groups["status"].Value.Trim());
            }

            var idx = trimmed.LastIndexOf(':');
            if (idx > 0 && idx < trimmed.Length - 1)
            {
                return (trimmed[..idx].Trim(), trimmed[(idx + 1)..].Trim());
            }

            if (trimmed.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return ("Scanner", trimmed);
            }

            return null;
        }

        private void CancelScan()
        {
            _scanCts?.Cancel();
        }

        private bool FilterHistory(object item)
        {
            if (item is not HistoryEvent history)
            {
                return false;
            }

            var typeMatch = HistoryFilterType == "All" || string.Equals(history.EventType, HistoryFilterType, StringComparison.OrdinalIgnoreCase);
            var text = HistoryFilterText?.Trim();
            var textMatch = string.IsNullOrWhiteSpace(text) ||
                            history.Details.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                            history.EventType.Contains(text, StringComparison.OrdinalIgnoreCase);
            return typeMatch && textMatch;
        }

        private async Task ExportHistory()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json",
                FileName = $"ClamAV_History_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var ext = Path.GetExtension(dialog.FileName);
                if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                {
                    await _historyService.ExportAsJsonAsync(dialog.FileName);
                }
                else
                {
                    await _historyService.ExportAsCsvAsync(dialog.FileName);
                }

                MessageBox.Show("History exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export history: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunHealthCheck()
        {
            var result = await HealthCheckService.RunAsync(_clamAVPath);
            MessageBox.Show(result, "ClamAV Health Check", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadQuarantine()
        {
            QuarantineItems.Clear();
            await _quarantineService.ClearMissingFilesAsync();
            var items = await _quarantineService.LoadItemsAsync();
            foreach (var item in items.OrderByDescending(i => i.QuarantinedAt))
            {
                QuarantineItems.Add(item);
            }
        }

        private async Task RemoveQuarantineItem(QuarantineItem? item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                if (File.Exists(item.QuarantinePath))
                {
                    File.Delete(item.QuarantinePath);
                }

                await _quarantineService.RemoveItemAsync(item.Id);
                QuarantineItems.Remove(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete quarantined file: {ex.Message}", "Quarantine Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RestoreQuarantineItem(QuarantineItem? item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                if (!File.Exists(item.QuarantinePath))
                {
                    MessageBox.Show("Quarantined file no longer exists.", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var targetPath = item.OriginalPath;
                if (string.IsNullOrWhiteSpace(targetPath) || string.Equals(targetPath, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    var dialog = new SaveFileDialog
                    {
                        FileName = Path.GetFileName(item.QuarantinePath),
                        Title = "Select restore destination"
                    };

                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    targetPath = dialog.FileName;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Move(item.QuarantinePath, targetPath, overwrite: true);
                await _quarantineService.RemoveItemAsync(item.Id);
                QuarantineItems.Remove(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore file: {ex.Message}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ScanFolderUsingDaemon()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select a folder to scan using clamd daemon.",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var result = await ClamAVService.ScanFolderWithDaemonAsync(dialog.SelectedPath);
            MessageBox.Show(result, "Daemon Folder Scan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ScheduleDailyScan()
        {
            if (string.IsNullOrWhiteSpace(ScheduledScanPath))
            {
                MessageBox.Show("Set a scheduled scan path first.", "Schedule Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(ScheduledScanPath) && !File.Exists(ScheduledScanPath))
            {
                MessageBox.Show("Scheduled scan path does not exist.", "Schedule Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParseExact(ScheduledScanTime, "hh\\:mm", CultureInfo.InvariantCulture, out var scanTime))
            {
                MessageBox.Show("Time format must be HH:mm.", "Schedule Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = await _schedulerService.CreateOrUpdateDailyScanTaskAsync(ScheduledScanPath, scanTime);
            IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
            MessageBox.Show(result, "Scheduled Scan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseScheduledPath()
        {
            var folderDialog = new VistaFolderBrowserDialog
            {
                Description = "Select a folder to be scanned daily.",
                UseDescriptionForTitle = true
            };

            if (folderDialog.ShowDialog() == true)
            {
                ScheduledScanPath = folderDialog.SelectedPath;
            }
        }

        private async Task RemoveScheduledScan()
        {
            var result = await _schedulerService.RemoveDailyScanTaskAsync();
            IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
            MessageBox.Show(result, "Scheduled Scan", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task PingDaemon()
        {
            var result = await ClamAVService.PingDaemonAsync();
            MessageBox.Show(result, "Ping Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ReloadDatabase()
        {
            var result = await ClamAVService.ReloadDatabaseAsync();
            MessageBox.Show(result, "Reload Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task GetVersion()
        {
            var result = await ClamAVService.GetVersionAsync();
            MessageBox.Show(result, "Version Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ShutdownDaemon()
        {
            var confirmResult = MessageBox.Show(
                "Are you sure you want to shut down the ClamAV daemon? The service will need to be started again manually.",
                "Confirm Shutdown",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes) return;

            await ClamAVService.ShutdownDaemonAsync();
            await Task.Delay(1000); // Give the process a moment to exit
            await RefreshDaemonStatusAsync();
        }

        private async Task GetStats()
        {
            var result = await ClamAVService.GetStatsAsync();
            MessageBox.Show(result, "Daemon Stats", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task GetVersionCommands()
        {
            var result = await ClamAVService.GetVersionCommandsAsync();
            MessageBox.Show(result, "Supported Commands", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute((T?)parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object? parameter) => _execute((T?)parameter);
    }

    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Predicate<T?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute == null || _canExecute((T?)parameter));
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object? parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            await _execute((T?)parameter);
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object? parameter) => _execute();
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute == null || _canExecute());
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object? parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            await _execute();
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
