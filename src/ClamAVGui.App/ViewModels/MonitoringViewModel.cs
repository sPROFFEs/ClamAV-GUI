using System.Collections.ObjectModel;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class MonitoringViewModel : ViewModelBase
{
    private readonly IFileSystemMonitor _monitor;
    private readonly IScanCoordinator _scanCoordinator;
    private readonly ISettingsService _settingsService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private bool _isMonitoringActive;

    [ObservableProperty]
    private string _newFilterText = string.Empty;

    public ObservableCollection<string> MonitoredPaths { get; } = new();
    public ObservableCollection<string> ExcludedPaths { get; } = new();
    public ObservableCollection<string> Filters { get; } = new();
    public ObservableCollection<string> LogEntries { get; } = new();

    public MonitoringViewModel(
        IFileSystemMonitor monitor,
        IScanCoordinator scanCoordinator,
        ISettingsService settingsService,
        IFileDialogService fileDialogService,
        INotificationService notificationService)
    {
        _monitor = monitor;
        _scanCoordinator = scanCoordinator;
        _settingsService = settingsService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;

        _monitor.LogMessage += msg =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
            });
        };

        _monitor.FileDetected += async file =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] File event: {file}");
            });

            try
            {
                var req = new ScanRequest
                {
                    Targets = new[] { new ScanTarget { Path = file, Type = ScanTargetType.File } },
                    Profile = new ScanProfile { Recursive = false }
                };
                var result = await _scanCoordinator.QueueAsync(req);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (result.Verdict == ScanVerdict.Infected)
                    {
                        LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] THREAT FOUND: {file} - {result.Detections.FirstOrDefault()?.ThreatName}");
                        _notificationService.ShowAsync("Threat Detected!", $"Threat found in {file}", NotificationSeverity.ThreatDetected);
                    }
                    else
                    {
                        LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Clean: {file}");
                    }
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Scan error for {file}: {ex.Message}");
                });
            }
        };
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        var s = await _settingsService.LoadSettingsAsync();
        MonitoredPaths.Clear();
        foreach (var p in s.MonitoredPaths) MonitoredPaths.Add(p);

        ExcludedPaths.Clear();
        foreach (var e in s.MonitoringExclusions) ExcludedPaths.Add(e);

        Filters.Clear();
        foreach (var f in s.MonitoringFilters) Filters.Add(f);
    }

    [RelayCommand]
    public async Task ToggleMonitoringAsync()
    {
        if (IsMonitoringActive)
        {
            await _monitor.StopAsync();
            IsMonitoringActive = false;
        }
        else
        {
            if (MonitoredPaths.Count == 0)
            {
                await _notificationService.ShowAsync("Monitoring", "Add at least one folder to monitor.", NotificationSeverity.Warning);
                return;
            }

            await _monitor.StartAsync(MonitoredPaths, Filters, ExcludedPaths);
            IsMonitoringActive = _monitor.IsMonitoring;
            if (IsMonitoringActive)
            {
                await _notificationService.ShowAsync("Monitoring Active", "Real-time folder monitoring is enabled.");
            }
        }
    }

    [RelayCommand]
    public async Task AddMonitoredFolderAsync()
    {
        var folder = await _fileDialogService.SelectFolderAsync("Select Folder to Monitor");
        if (!string.IsNullOrWhiteSpace(folder) && !MonitoredPaths.Contains(folder))
        {
            MonitoredPaths.Add(folder);
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task RemoveMonitoredFolderAsync(string? folder)
    {
        if (folder != null)
        {
            MonitoredPaths.Remove(folder);
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task AddExcludedFolderAsync()
    {
        var folder = await _fileDialogService.SelectFolderAsync("Select Folder to Exclude");
        if (!string.IsNullOrWhiteSpace(folder) && !ExcludedPaths.Contains(folder))
        {
            ExcludedPaths.Add(folder);
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task RemoveExcludedFolderAsync(string? folder)
    {
        if (folder != null)
        {
            ExcludedPaths.Remove(folder);
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task AddFilterAsync()
    {
        var filter = NewFilterText?.Trim();
        if (!string.IsNullOrWhiteSpace(filter) && !Filters.Contains(filter))
        {
            Filters.Add(filter);
            NewFilterText = string.Empty;
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task RemoveFilterAsync(string? filter)
    {
        if (filter != null)
        {
            Filters.Remove(filter);
            await SaveSettingsAsync();
        }
    }

    [RelayCommand]
    public async Task ExportLogAsync()
    {
        var path = await _fileDialogService.SaveFileAsync("Export Monitoring Log", $"ClamAV_Monitoring_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await File.WriteAllLinesAsync(path, LogEntries);
            await _notificationService.ShowAsync("Export Complete", "Monitoring log exported.");
        }
    }

    private async Task SaveSettingsAsync()
    {
        var s = await _settingsService.LoadSettingsAsync();
        s.MonitoredPaths = MonitoredPaths.ToList();
        s.MonitoringExclusions = ExcludedPaths.ToList();
        s.MonitoringFilters = Filters.ToList();
        await _settingsService.SaveSettingsAsync(s);
    }
}
