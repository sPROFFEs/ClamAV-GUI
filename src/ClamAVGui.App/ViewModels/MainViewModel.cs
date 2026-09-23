using System.Collections.ObjectModel;
using Avalonia.Media;
using ClamAVGui.App.Services;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed record NavItem(string Title, StreamGeometry IconGeometry);

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
        new("Home", StreamGeometry.Parse("M12 2L4 5v6.09c0 5.05 3.41 9.76 8 10.91 4.59-1.15 8-5.86 8-10.91V5l-8-3zm-1.06 13.54L7.4 12l1.41-1.41 2.13 2.12 4.24-4.24 1.41 1.42-5.65 5.65z")),
        new("Virus & threat protection", StreamGeometry.Parse("M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z")),
        new("Security intelligence", StreamGeometry.Parse("M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0 1 12 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z")),
        new("Protection history", StreamGeometry.Parse("M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z")),
        new("Quarantined items", StreamGeometry.Parse("M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z")),
        new("ClamAV engine service", StreamGeometry.Parse("M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z")),
        new("Real-time protection", StreamGeometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z")),
        new("Scan scheduler", StreamGeometry.Parse("M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20a2 2 0 0 0 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V10h14v10zm0-12H5V6h14v2zm-7 5h5v5h-5v-5z")),
        new("System diagnostics", StreamGeometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-2 10h-4v4h-2v-4H7v-2h4V7h2v4h4v2z")),
        new("Settings", StreamGeometry.Parse("M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65A.488.488 0 0 0 14 2h-4c-.25 0-.46.18-.49.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1c.23.09.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z"))
    };

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private double _sidebarWidth = 260;

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
        SidebarWidth = IsSidebarExpanded ? 260 : 68;
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
