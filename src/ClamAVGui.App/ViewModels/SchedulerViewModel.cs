using System.Globalization;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class SchedulerViewModel : ViewModelBase
{
    private readonly ISchedulerService _schedulerService;
    private readonly ISettingsService _settingsService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _scheduledScanPath = string.Empty;

    [ObservableProperty]
    private string _scheduledScanTime = "02:00";

    [ObservableProperty]
    private bool _isScheduledScanEnabled;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SchedulerViewModel(
        ISchedulerService schedulerService,
        ISettingsService settingsService,
        IFileDialogService fileDialogService,
        INotificationService notificationService)
    {
        _schedulerService = schedulerService;
        _settingsService = settingsService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        ScheduledScanPath = settings.ScheduledScanPath ?? string.Empty;
        ScheduledScanTime = settings.ScheduledScanTime ?? "02:00";
        IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
    }

    [RelayCommand]
    public async Task BrowsePathAsync()
    {
        var path = await _fileDialogService.SelectFolderAsync("Select Target Folder to Schedule");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ScheduledScanPath = path;
        }
    }

    [RelayCommand]
    public async Task SaveScheduleAsync()
    {
        if (string.IsNullOrWhiteSpace(ScheduledScanPath))
        {
            await _notificationService.ShowAsync("Scheduler", "Please choose a path to scan first.", NotificationSeverity.Warning);
            return;
        }

        if (!TimeSpan.TryParseExact(ScheduledScanTime, "hh\\:mm", CultureInfo.InvariantCulture, out var time))
        {
            await _notificationService.ShowAsync("Scheduler", "Time must be in HH:mm format (e.g. 02:00).", NotificationSeverity.Warning);
            return;
        }

        var res = await _schedulerService.CreateOrUpdateDailyScanTaskAsync(ScheduledScanPath, time);
        IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
        StatusMessage = res;

        var s = await _settingsService.LoadSettingsAsync();
        s.ScheduledScanPath = ScheduledScanPath;
        s.ScheduledScanTime = ScheduledScanTime;
        s.IsScheduledScanEnabled = IsScheduledScanEnabled;
        await _settingsService.SaveSettingsAsync(s);

        await _notificationService.ShowAsync("Scheduled Scan", res);
    }

    [RelayCommand]
    public async Task RemoveScheduleAsync()
    {
        var res = await _schedulerService.RemoveDailyScanTaskAsync();
        IsScheduledScanEnabled = await _schedulerService.IsScheduledScanConfiguredAsync();
        StatusMessage = res;

        var s = await _settingsService.LoadSettingsAsync();
        s.IsScheduledScanEnabled = false;
        await _settingsService.SaveSettingsAsync(s);

        await _notificationService.ShowAsync("Scheduled Scan", res);
    }
}
