using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IClamAvBinaryLocator _binaryLocator;
    private readonly IStartupService _startupService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _customClamAvPath = string.Empty;

    [ObservableProperty]
    private string _customQuarantinePath = string.Empty;

    [ObservableProperty]
    private bool _autoUpdateDefinitions;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startOnLogin;

    [ObservableProperty]
    private bool _moveToQuarantine;

    [ObservableProperty]
    private bool _heuristicAlerts;

    [ObservableProperty]
    private bool _scanEncrypted;

    [ObservableProperty]
    private bool _leaveTemps;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IClamAvBinaryLocator binaryLocator,
        IStartupService startupService,
        IFileDialogService fileDialogService,
        INotificationService notificationService)
    {
        _settingsService = settingsService;
        _binaryLocator = binaryLocator;
        _startupService = startupService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        var s = await _settingsService.LoadSettingsAsync();
        CustomClamAvPath = s.CustomClamAvPath ?? string.Empty;
        CustomQuarantinePath = s.CustomQuarantinePath ?? string.Empty;
        AutoUpdateDefinitions = s.AutoUpdateDefinitions;
        MinimizeToTray = s.MinimizeToTray;
        MoveToQuarantine = s.MoveToQuarantine;
        HeuristicAlerts = s.HeuristicAlerts;
        ScanEncrypted = s.ScanEncrypted;
        LeaveTemps = s.LeaveTemps;

        StartOnLogin = await _startupService.IsStartOnLoginEnabledAsync();
    }

    [RelayCommand]
    public async Task AutoDetectClamAvAsync()
    {
        var installation = await _binaryLocator.FindInstallationAsync();
        if (installation != null && !string.IsNullOrWhiteSpace(installation.RootDirectory))
        {
            CustomClamAvPath = installation.RootDirectory;
            StatusMessage = $"ClamAV auto-detected: {installation.ClamScanPath} ({installation.Version})";
            await SaveSettingsAsync();
            await _notificationService.ShowAsync("Auto-Detect", StatusMessage);
        }
        else
        {
            StatusMessage = "Could not find ClamAV in standard paths. Please specify directory manually.";
            await _notificationService.ShowAsync("Auto-Detect", StatusMessage, NotificationSeverity.Warning);
        }
    }

    [RelayCommand]
    public async Task BrowseClamAvPathAsync()
    {
        var folder = await _fileDialogService.SelectFolderAsync("Select ClamAV Binary Directory");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            CustomClamAvPath = folder;
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task BrowseQuarantinePathAsync()
    {
        var folder = await _fileDialogService.SelectFolderAsync("Select Quarantine Directory");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            CustomQuarantinePath = folder;
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var s = await _settingsService.LoadSettingsAsync();
        s.CustomClamAvPath = string.IsNullOrWhiteSpace(CustomClamAvPath) ? null : CustomClamAvPath;
        s.CustomQuarantinePath = string.IsNullOrWhiteSpace(CustomQuarantinePath) ? null : CustomQuarantinePath;
        s.AutoUpdateDefinitions = AutoUpdateDefinitions;
        s.MinimizeToTray = MinimizeToTray;
        s.MoveToQuarantine = MoveToQuarantine;
        s.HeuristicAlerts = HeuristicAlerts;
        s.ScanEncrypted = ScanEncrypted;
        s.LeaveTemps = LeaveTemps;

        await _settingsService.SaveSettingsAsync(s);
        await _startupService.SetStartOnLoginAsync(StartOnLogin);

        var installation = await _binaryLocator.FindInstallationAsync(s.CustomClamAvPath);
        StatusMessage = installation != null ? $"Settings saved. ClamAV found at {installation.ClamScanPath}" : "Settings saved (ClamAV path not verified).";

        await _notificationService.ShowAsync("Settings", StatusMessage);
    }
}
