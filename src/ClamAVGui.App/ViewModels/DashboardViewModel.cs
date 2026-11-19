using ClamAVGui.Core.Configuration;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IClamAvBinaryLocator _binaryLocator;
    private readonly IHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly Action _navigateToScan;
    private readonly Action _navigateToUpdate;
    private readonly Action _navigateToSettings;
    private readonly Action _navigateToMonitoring;
    private readonly Action _navigateToDaemon;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private string _databaseStatusText = "Checking…";

    [ObservableProperty]
    private string? _databaseDirectory;

    [ObservableProperty]
    private bool _isDatabaseReady;

    [ObservableProperty]
    private string _lastUpdateTime = "Never";

    [ObservableProperty]
    private int _totalScans;

    public DashboardViewModel(
        IClamAvBinaryLocator binaryLocator,
        IHistoryService historyService,
        ISettingsService settingsService,
        Action navigateToScan,
        Action navigateToUpdate,
        Action navigateToSettings,
        Action navigateToMonitoring,
        Action navigateToDaemon)
    {
        _binaryLocator = binaryLocator;
        _historyService = historyService;
        _settingsService = settingsService;
        _navigateToScan = navigateToScan;
        _navigateToUpdate = navigateToUpdate;
        _navigateToSettings = navigateToSettings;
        _navigateToMonitoring = navigateToMonitoring;
        _navigateToDaemon = navigateToDaemon;
    }

    [RelayCommand]
    public void StartScan() => _navigateToScan();

    [RelayCommand]
    public void UpdateDefinitions() => _navigateToUpdate();

    [RelayCommand]
    public void OpenSettings() => _navigateToSettings();

    [RelayCommand]
    public void OpenMonitoring() => _navigateToMonitoring();

    [RelayCommand]
    public void OpenDaemon() => _navigateToDaemon();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        var installation = await _binaryLocator.FindInstallationAsync(settings);

        DatabaseDirectory = installation?.DatabaseDirectory ?? settings.CustomDatabaseDirectory;
        var databaseStatus = ClamAvDatabase.Inspect(DatabaseDirectory);
        IsDatabaseReady = installation != null && databaseStatus == DatabaseStatus.Available;
        DatabaseStatusText = ClamAvDatabase.Describe(databaseStatus);
        StatusText = installation != null ? DatabaseStatusText : "ClamAV engine not found";

        var events = await _historyService.LoadHistoryAsync();
        TotalScans = events.Count(e => e.EventType == "Scan" &&
            e.Details.StartsWith("Scan completed for ", StringComparison.Ordinal));

        var lastUpdate = events.FirstOrDefault(e => e.EventType == "Definitions Checked" ||
            e.EventType == "Update" && (e.Details == "Database already up to date." ||
                e.Details.StartsWith("Database updated successfully.", StringComparison.Ordinal)));
        LastUpdateTime = lastUpdate != null ? lastUpdate.Timestamp.ToLocalTime().ToString("g") : "Never";
    }
}
