using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private DiagnosticsReport? _report;

    [ObservableProperty]
    private string _formattedReport = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public DiagnosticsViewModel(
        IDiagnosticsService diagnosticsService,
        INotificationService notificationService)
    {
        _diagnosticsService = diagnosticsService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task RefreshReportAsync()
    {
        IsBusy = true;
        try
        {
            Report = await _diagnosticsService.CollectReportAsync();
            FormattedReport = _diagnosticsService.FormatReport(Report, redactSensitivePaths: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CopyRedactedReportAsync()
    {
        if (Report == null) return;
        var text = _diagnosticsService.FormatReport(Report, redactSensitivePaths: true);
        await SetClipboardTextAsync(text);
        await _notificationService.ShowAsync("Copied", "Redacted diagnostics copied to clipboard.");
    }

    [RelayCommand]
    public async Task CopyFullReportAsync()
    {
        if (Report == null) return;
        var text = _diagnosticsService.FormatReport(Report, redactSensitivePaths: false);
        await SetClipboardTextAsync(text);
        await _notificationService.ShowAsync("Copied", "Full diagnostics report copied to clipboard.");
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
    }
}
