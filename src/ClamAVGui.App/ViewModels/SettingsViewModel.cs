using ClamAVGui.App.Services;
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
    private readonly IClamAvInstallerService _installerService;
    private readonly IAppUpdateService _appUpdateService;

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

    [ObservableProperty]
    private bool _isInstallingEngine;

    [ObservableProperty]
    private string _engineInstallStatus = string.Empty;

    [ObservableProperty]
    private bool _isCheckingAppUpdates;

    [ObservableProperty]
    private string _appUpdateStatusMessage = string.Empty;

    [ObservableProperty]
    private AppReleaseInfo? _availableAppRelease;

    public string CurrentAppVersion => _appUpdateService.CurrentVersion;
    public string RecommendedInstallCommand => _installerService.RecommendedCommandOrMethod;

    public SettingsViewModel(
        ISettingsService settingsService,
        IClamAvBinaryLocator binaryLocator,
        IStartupService startupService,
        IFileDialogService fileDialogService,
        INotificationService notificationService,
        IClamAvInstallerService installerService,
        IAppUpdateService appUpdateService)
    {
        _settingsService = settingsService;
        _binaryLocator = binaryLocator;
        _startupService = startupService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;
        _installerService = installerService;
        _appUpdateService = appUpdateService;
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
    public async Task CheckAppUpdatesAsync()
    {
        IsCheckingAppUpdates = true;
        AppUpdateStatusMessage = "Checking GitHub for new releases...";
        AvailableAppRelease = null;

        try
        {
            var release = await _appUpdateService.CheckForUpdatesAsync();
            if (release != null && !string.Equals(release.TagName, CurrentAppVersion, StringComparison.OrdinalIgnoreCase))
            {
                AvailableAppRelease = release;
                AppUpdateStatusMessage = $"New version {release.TagName} is available!";
                await _notificationService.ShowAsync("App Update Available", $"Version {release.TagName} is ready to install.");
            }
            else
            {
                AppUpdateStatusMessage = $"ClamAV GUI is up to date ({CurrentAppVersion}).";
            }
        }
        catch (Exception ex)
        {
            AppUpdateStatusMessage = $"Could not check updates: {ex.Message}";
        }
        finally
        {
            IsCheckingAppUpdates = false;
        }
    }

    [RelayCommand]
    public async Task ApplyAppUpdateAsync()
    {
        if (AvailableAppRelease == null) return;
        AppUpdateStatusMessage = "Applying update...";

        var progress = new Progress<string>(msg =>
        {
            AppUpdateStatusMessage = msg;
        });

        await _appUpdateService.ApplyUpdateAndRestartAsync(AvailableAppRelease, progress);
    }

    [RelayCommand]
    public async Task InstallEngineAsync()
    {
        if (IsInstallingEngine) return;

        IsInstallingEngine = true;
        EngineInstallStatus = "Initiating ClamAV installation...";

        var progress = new Progress<string>(msg =>
        {
            EngineInstallStatus = msg;
        });

        try
        {
            var success = await _installerService.InstallAsync(progress);
            if (success)
            {
                await LoadSettingsAsync();
                await _notificationService.ShowAsync("ClamAV Engine", "ClamAV engine has been installed and configured successfully!");
            }
            else
            {
                await _notificationService.ShowAsync("ClamAV Engine", EngineInstallStatus, NotificationSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            EngineInstallStatus = $"Installation failed: {ex.Message}";
            await _notificationService.ShowAsync("Engine Install Error", ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            IsInstallingEngine = false;
        }
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
            StatusMessage = "Could not find ClamAV in standard paths. Please specify directory manually or click 'Install ClamAV Engine'.";
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
