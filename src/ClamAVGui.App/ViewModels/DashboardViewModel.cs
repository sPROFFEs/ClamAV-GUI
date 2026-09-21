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
        Action navigateToUpdate)
    {
        _binaryLocator = binaryLocator;
        _daemon = daemon;
        _historyService = historyService;
        _settingsService = settingsService;
        _navigateToScan = navigateToScan;
        _navigateToUpdate = navigateToUpdate;
    }

    [RelayCommand]
    public void StartScan() => _navigateToScan();

    [RelayCommand]
    public void UpdateDefinitions() => _navigateToUpdate();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        var installation = await _binaryLocator.FindInstallationAsync(settings.CustomClamAvPath);

        if (installation != null)
        {
            IsClamAvConfigured = true;
            StatusText = $"ClamAV detected ({installation.Version})";
        }
        else
        {
            IsClamAvConfigured = false;
            StatusText = "ClamAV not detected. Please configure path in Settings.";
        }

        var health = await _daemon.CheckHealthAsync();
        IsClamDRunning = health.EndpointReachable && health.ProtocolHealthy;
        VirusDefinitionsVersion = health.Version ?? (installation != null ? "Ready" : "N/A");
        DaemonStats = health.Stats ?? (IsClamDRunning ? "Running" : "Stopped");

        var events = await _historyService.LoadHistoryAsync();
        var scanEvents = events.Where(e => e.EventType == "Scan").ToList();
        TotalScans = scanEvents.Count;

        var lastUpdate = events.FirstOrDefault(e => e.EventType == "Update");
        LastUpdateTime = lastUpdate != null ? lastUpdate.Timestamp.ToLocalTime().ToString("g") : "Never";
    }
}
