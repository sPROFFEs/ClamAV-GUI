using System.Collections.ObjectModel;
using ClamAVGui.App.Services;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed record NavItem(string Title, string IconResourceKey);

public sealed partial class MainViewModel : ViewModelBase
{
    public DashboardViewModel Dashboard { get; }
    public ScanViewModel Scan { get; }
    public UpdateViewModel Update { get; }
    public HistoryViewModel History { get; }
    public QuarantineViewModel Quarantine { get; }
    public DaemonViewModel Daemon { get; }
    public MonitoringViewModel Monitoring { get; }
    public SchedulerViewModel Scheduler { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; } = new()
    {
        new("Home", "IconShield"),
        new("Virus & threat protection", "IconScan"),
        new("Security intelligence", "IconUpdates"),
        new("Protection history", "IconHistory"),
        new("Quarantined items", "IconQuarantine"),
        new("ClamAV engine service", "IconDaemon"),
        new("Real-time protection", "IconMonitoring"),
        new("Scan scheduler", "IconScheduler"),
        new("System diagnostics", "IconDiagnostics"),
        new("Settings", "IconSettings")
    };

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private string _notificationBannerMessage = string.Empty;

    [ObservableProperty]
    private bool _isNotificationBannerVisible;

    public MainViewModel(
        DashboardViewModel dashboard,
        ScanViewModel scan,
        UpdateViewModel update,
        HistoryViewModel history,
        QuarantineViewModel quarantine,
        DaemonViewModel daemon,
        MonitoringViewModel monitoring,
        SchedulerViewModel scheduler,
        DiagnosticsViewModel diagnostics,
        SettingsViewModel settings,
        INotificationService notificationService)
    {
        Dashboard = dashboard;
        Scan = scan;
        Update = update;
        History = history;
        Quarantine = quarantine;
        Daemon = daemon;
        Monitoring = monitoring;
        Scheduler = scheduler;
        Diagnostics = diagnostics;
        Settings = settings;

        if (notificationService is AvaloniaNotificationService notify)
        {
            notify.NotificationReceived += (title, message, severity) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    NotificationBannerMessage = $"[{title}] {message}";
                    IsNotificationBannerVisible = true;
                });
            };
        }
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    public void DismissBanner()
    {
        IsNotificationBannerVisible = false;
    }

    public async Task InitializeAsync()
    {
        await Dashboard.RefreshAsync();
        await Settings.LoadSettingsAsync();
        await History.LoadHistoryAsync();
        await Quarantine.LoadAsync();
        await Monitoring.LoadSettingsAsync();
        await Scheduler.LoadAsync();
        await Diagnostics.RefreshReportAsync();
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            await Settings.CheckAppUpdatesAsync();
        });
    }
}
