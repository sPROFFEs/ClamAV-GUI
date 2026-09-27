using System.Collections.ObjectModel;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IClamAvBinaryLocator _binaryLocator;
    private readonly IClamAvDaemon _daemon;
    private readonly IHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly Action _navigateToScan;
    private readonly Action _navigateToUpdate;
    private readonly Action _navigateToSettings;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private string _virusDefinitionsVersion = "N/A";

    [ObservableProperty]
    private string _daemonStats = "N/A";

    [ObservableProperty]
    private string _lastUpdateTime = "Never";

    [ObservableProperty]
    private int _totalScans;

    [ObservableProperty]
    private int _totalInfectedFiles;

    [ObservableProperty]
    private bool _isClamAvConfigured;

    [ObservableProperty]
    private bool _isClamDRunning;

    public DashboardViewModel(
        IClamAvBinaryLocator binaryLocator,
        IClamAvDaemon daemon,
        IHistoryService historyService,
        ISettingsService settingsService,
        Action navigateToScan,
        Action navigateToUpdate,
        Action navigateToSettings)
    {
        _binaryLocator = binaryLocator;
        _daemon = daemon;
        _historyService = historyService;
        _settingsService = settingsService;
        _navigateToScan = navigateToScan;
        _navigateToUpdate = navigateToUpdate;
        _navigateToSettings = navigateToSettings;
    }

    [RelayCommand]
    public void StartScan() => _navigateToScan();

    [RelayCommand]
    public void UpdateDefinitions() => _navigateToUpdate();

    [RelayCommand]
    public void OpenSettings() => _navigateToSettings();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        var installation = await _binaryLocator.FindInstallationAsync(settings.CustomClamAvPath);

        if (installation != null)
        {
            IsClamAvConfigured = true;
            var hasDefinitions = false;
            try
            {
                hasDefinitions = !string.IsNullOrWhiteSpace(installation.DatabaseDirectory) &&
                    Directory.Exists(installation.DatabaseDirectory) &&
                    Directory.EnumerateFiles(installation.DatabaseDirectory, "*.c?d").Any();
            }
            catch (UnauthorizedAccessException)
            {
                // A system database can exist without being readable by this user.
            }
            StatusText = hasDefinitions ? "ClamAV ready for on-demand scans" : "ClamAV found; virus definitions are missing";
        }
        else
        {
            IsClamAvConfigured = false;
            StatusText = "ClamAV engine is not installed or detected.";
        }

        var health = await _daemon.CheckHealthAsync();
        IsClamDRunning = health.EndpointReachable && health.ProtocolHealthy;
        VirusDefinitionsVersion = health.Version ?? (installation != null ? "Installed" : "N/A");
        DaemonStats = health.Stats ?? (IsClamDRunning ? "Running" : "Stopped");

        var events = await _historyService.LoadHistoryAsync();
        var scanEvents = events.Where(e => e.EventType == "Scan").ToList();
        TotalScans = scanEvents.Count;

        var lastUpdate = events.FirstOrDefault(e => e.EventType == "Update");
        LastUpdateTime = lastUpdate != null ? lastUpdate.Timestamp.ToLocalTime().ToString("g") : "Never";
    }
}
