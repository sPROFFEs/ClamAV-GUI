using System.Collections.ObjectModel;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed record DisplayScanResult
{
    public required string FilePath { get; init; }
    public required string Status { get; init; }
    public bool IsInfected { get; init; }
    public string ThreatName { get; init; } = string.Empty;
}

public sealed partial class ScanViewModel : ViewModelBase
{
    private readonly IScanCoordinator _scanCoordinator;
    private readonly IFileDialogService _fileDialogService;
    private readonly IPlatformService _platformService;
    private readonly IHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly IQuarantineService _quarantineService;
    private readonly INotificationService _notificationService;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _selectedTargetPath = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _currentScanningFile = string.Empty;

    [ObservableProperty]
    private int _scannedFilesCount;

    [ObservableProperty]
    private ScanStatistics? _currentSummary;

    [ObservableProperty]
    private string _scanVerdictText = string.Empty;

    [ObservableProperty]
    private bool _heuristicAlerts;

    [ObservableProperty]
    private bool _scanEncrypted;

    [ObservableProperty]
    private bool _moveToQuarantine;

    public ObservableCollection<DisplayScanResult> Results { get; } = new();

    public ScanViewModel(
        IScanCoordinator scanCoordinator,
        IFileDialogService fileDialogService,
        IPlatformService platformService,
        IHistoryService historyService,
        ISettingsService settingsService,
        IQuarantineService quarantineService,
        INotificationService notificationService)
    {
        _scanCoordinator = scanCoordinator;
        _fileDialogService = fileDialogService;
        _platformService = platformService;
        _historyService = historyService;
        _settingsService = settingsService;
        _quarantineService = quarantineService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task BrowseFileAsync()
    {
        var file = await _fileDialogService.SelectFileAsync("Select File to Scan");
        if (!string.IsNullOrWhiteSpace(file))
        {
            SelectedTargetPath = file;
            await StartScanAsync(file, ScanTargetType.File);
        }
    }

    [RelayCommand]
    public async Task BrowseFolderAsync()
    {
        var folder = await _fileDialogService.SelectFolderAsync("Select Folder to Scan");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SelectedTargetPath = folder;
            await StartScanAsync(folder, ScanTargetType.Directory);
        }
    }

    [RelayCommand]
    public async Task QuickScanHomeAsync()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(home, "Downloads");
        var target = Directory.Exists(downloads) ? downloads : home;
        SelectedTargetPath = target;
        await StartScanAsync(target, ScanTargetType.Directory);
    }

    [RelayCommand]
    public void CancelScan()
    {
        _scanCts?.Cancel();
    }

    [RelayCommand]
    public void OpenContainingFolder(DisplayScanResult? item)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.FilePath))
        {
            _platformService.OpenContainingFolder(item.FilePath);
        }
    }

    [RelayCommand]
    public async Task QuarantineItemAsync(DisplayScanResult? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
        {
            await _notificationService.ShowAsync("Quarantine", "File not found.", NotificationSeverity.Warning);
            return;
        }

        try
        {
            await _quarantineService.QuarantineFileAsync(item.FilePath, item.ThreatName);
            await _notificationService.ShowAsync("Quarantined", $"Moved {Path.GetFileName(item.FilePath)} to quarantine.");
        }
        catch (Exception ex)
        {
            await _notificationService.ShowAsync("Quarantine Error", ex.Message, NotificationSeverity.Error);
        }
    }

    public async Task StartScanAsync(string targetPath, ScanTargetType targetType)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        IsScanning = true;
        Results.Clear();
        CurrentSummary = null;
        ScanVerdictText = "Scanning in progress...";
        ScannedFilesCount = 0;
        CurrentScanningFile = targetPath;

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        var settings = await _settingsService.LoadSettingsAsync();
        var profile = new ScanProfile
        {
            Recursive = true,
            HeuristicAlerts = HeuristicAlerts || settings.HeuristicAlerts,
            ScanEncryptedArchives = ScanEncrypted || settings.ScanEncrypted,
            LeaveTemps = settings.LeaveTemps
        };

        var request = new ScanRequest
        {
            Targets = new[] { new ScanTarget { Path = targetPath, Type = targetType } },
            Profile = profile,
            MoveToQuarantine = MoveToQuarantine || settings.MoveToQuarantine,
            QuarantineDirectory = settings.CustomQuarantinePath ?? Path.Combine(_platformService.UserDataDirectory, "Quarantine")
        };

        var progress = new Progress<ScanProgress>(p =>
        {
            CurrentScanningFile = p.CurrentFile;
            ScannedFilesCount = p.ScannedFiles;
            var isInfected = p.StatusMessage.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase);
            var threat = isInfected ? p.StatusMessage[..^"FOUND".Length].Trim() : string.Empty;

            Results.Insert(0, new DisplayScanResult
            {
                FilePath = p.CurrentFile,
                Status = p.StatusMessage,
                IsInfected = isInfected,
                ThreatName = threat
            });
        });

        try
        {
            await _historyService.LogEventAsync("Scan", $"Scan started for: {targetPath}");
            var result = await _scanCoordinator.QueueAsync(request, progress, _scanCts.Token);

            CurrentSummary = result.Statistics;
            ScanVerdictText = result.Verdict switch
            {
                ScanVerdict.Clean => "Scan Complete: No threats found.",
                ScanVerdict.Infected => $"Threats Detected! Found {result.Detections.Count} infected item(s).",
                ScanVerdict.Cancelled => "Scan Cancelled.",
                _ => $"Scan finished with issues: {string.Join("; ", result.Errors)}"
            };

            await _historyService.LogEventAsync("Scan", $"Scan completed for {targetPath}. Verdict: {result.Verdict}. Threats: {result.Detections.Count}.");
            if (result.Verdict == ScanVerdict.Infected)
            {
                await _notificationService.ShowAsync("Threat Detected!", $"Found {result.Detections.Count} threat(s) during scan.", NotificationSeverity.ThreatDetected);
            }
            else if (result.Verdict == ScanVerdict.Clean)
            {
                await _notificationService.ShowAsync("Scan Finished", "No threats found. Your files are clean.");
            }
        }
        catch (OperationCanceledException)
        {
            ScanVerdictText = "Scan Cancelled.";
            await _historyService.LogEventAsync("Scan", $"Scan cancelled for: {targetPath}");
        }
        catch (Exception ex)
        {
            ScanVerdictText = $"Scan failed: {ex.Message}";
            await _historyService.LogEventAsync("Scan", $"Scan error for {targetPath}: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }
}
