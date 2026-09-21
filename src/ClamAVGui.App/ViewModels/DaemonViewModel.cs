using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class DaemonViewModel : ViewModelBase
{
    private readonly IClamAvDaemon _daemon;
    private readonly IClamAvBinaryLocator _binaryLocator;
    private readonly IClamAvConfigurationProvider _configProvider;
    private readonly ISettingsService _settingsService;
    private readonly IPlatformService _platformService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private bool _isDaemonRunning;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Unknown";

    [ObservableProperty]
    private string _version = "N/A";

    [ObservableProperty]
    private string _stats = "N/A";

    [ObservableProperty]
    private string _commandOutput = string.Empty;

    public DaemonViewModel(
        IClamAvDaemon daemon,
        IClamAvBinaryLocator binaryLocator,
        IClamAvConfigurationProvider configProvider,
        ISettingsService settingsService,
        IPlatformService platformService,
        INotificationService notificationService)
    {
        _daemon = daemon;
        _binaryLocator = binaryLocator;
        _configProvider = configProvider;
        _settingsService = settingsService;
        _platformService = platformService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        IsBusy = true;
        try
        {
            var health = await _daemon.CheckHealthAsync();
            IsDaemonRunning = health.EndpointReachable && health.ProtocolHealthy;
            Version = health.Version ?? "N/A";
            Stats = health.Stats ?? "N/A";
            StatusMessage = IsDaemonRunning ? "Daemon is online and responsive." : (health.EndpointReachable ? $"Protocol error: {health.Error}" : $"Unreachable: {health.Error}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StartDaemonAsync()
    {
        if (IsDaemonRunning || IsBusy) return;

        IsBusy = true;
        StatusMessage = "Starting ClamAV daemon...";
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            var installation = await _binaryLocator.FindInstallationAsync(settings.CustomClamAvPath);
            if (installation == null)
            {
                StatusMessage = "ClamAV is not installed or configured.";
                return;
            }

            var config = await _configProvider.LoadAsync(installation);
            var endpoint = config.DaemonEndpoint ?? (_platformService.Platform == PlatformKind.Windows
                ? new TcpClamdEndpoint("127.0.0.1", 3310)
                : new UnixClamdEndpoint(Path.Combine(_platformService.RuntimeDirectory, "clamd.ctl")));

            var configPath = config.ConfigPath;
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                // Create managed config
                configPath = await _configProvider.InitializeManagedConfigAsync(installation, _platformService.UserDataDirectory, endpoint);
            }

            await _daemon.StartManagedAsync(configPath, endpoint);
            StatusMessage = "Daemon started successfully.";
            await RefreshStatusAsync();
            await _notificationService.ShowAsync("ClamAV Daemon", "Daemon started successfully.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start daemon: {ex.Message}";
            await _notificationService.ShowAsync("Daemon Error", ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StopDaemonAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = "Stopping daemon...";
        try
        {
            await _daemon.StopAsync();
            StatusMessage = "Daemon stopped.";
            await RefreshStatusAsync();
            await _notificationService.ShowAsync("ClamAV Daemon", "Daemon stopped.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error stopping daemon: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ReloadDatabaseAsync()
    {
        try
        {
            var result = await _daemon.ReloadDatabaseAsync();
            CommandOutput = $"Reload Database: {result}";
            await _notificationService.ShowAsync("ClamAV Daemon", result);
        }
        catch (Exception ex)
        {
            CommandOutput = $"Reload failed: {ex.Message}";
        }
    }
}
