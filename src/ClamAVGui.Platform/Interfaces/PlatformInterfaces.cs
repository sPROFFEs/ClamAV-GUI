using ClamAVGui.Core.Models;

namespace ClamAVGui.Platform.Interfaces;

public enum NotificationSeverity
{
    Information,
    Warning,
    Error,
    ThreatDetected
}

public enum TrayStatus
{
    Idle,
    Scanning,
    Updating,
    ThreatDetected,
    Error
}

public sealed record ScheduledScan
{
    public required string Id { get; init; }
    public required string TargetPath { get; init; }
    public required TimeSpan TimeOfDay { get; init; }
    public bool IsEnabled { get; init; } = true;
    public DateTime? LastRunTime { get; init; }
}

public sealed record DiagnosticsReport
{
    public required string ApplicationVersion { get; init; }
    public required string DotNetRuntime { get; init; }
    public required string OperatingSystem { get; init; }
    public required string Architecture { get; init; }
    public string? ClamScanPath { get; init; }
    public string? FreshClamPath { get; init; }
    public string? ClamdPath { get; init; }
    public string? ClamdScanPath { get; init; }
    public string? ClamOnAccPath { get; init; }
    public string? ConfigDirectory { get; init; }
    public string? DatabaseDirectory { get; init; }
    public string? DaemonEndpoint { get; init; }
    public bool DaemonRunning { get; init; }
    public string? DaemonVersion { get; init; }
    public string? DaemonStats { get; init; }
    public string? RealTimeMode { get; init; }
    public string? SchedulerBackend { get; init; }
    public string? QuarantineDirectory { get; init; }
    public IReadOnlyList<string> HealthCheckOutput { get; init; } = Array.Empty<string>();
}

public interface IPlatformService
{
    PlatformKind Platform { get; }
    string Architecture { get; }
    string UserDataDirectory { get; }
    string UserCacheDirectory { get; }
    string RuntimeDirectory { get; }
    PlatformCapabilities Capabilities { get; }
    void OpenContainingFolder(string filePath);
}

public interface IFileSystemMonitor : IAsyncDisposable
{
    bool IsMonitoring { get; }
    event Action<string>? FileDetected;
    event Action<string>? LogMessage;
    Task StartAsync(IEnumerable<string> paths, IEnumerable<string> filters, IEnumerable<string> exclusions, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IStartupService
{
    Task<bool> IsStartOnLoginEnabledAsync(CancellationToken cancellationToken = default);
    Task SetStartOnLoginAsync(bool enable, CancellationToken cancellationToken = default);
}

public interface ISchedulerService
{
    Task<IReadOnlyList<ScheduledScan>> GetSchedulesAsync(CancellationToken cancellationToken = default);
    Task<bool> IsScheduledScanConfiguredAsync(CancellationToken cancellationToken = default);
    Task<string> CreateOrUpdateDailyScanTaskAsync(string targetPath, TimeSpan timeOfDay, CancellationToken cancellationToken = default);
    Task<string> RemoveDailyScanTaskAsync(CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task ShowAsync(string title, string message, NotificationSeverity severity = NotificationSeverity.Information, CancellationToken cancellationToken = default);
}

public interface ITrayService
{
    void Initialize();
    void SetStatus(TrayStatus status);
    void Show();
    void Hide();
}

public interface IFileDialogService
{
    Task<string?> SelectFileAsync(string title = "Select File", string? filter = null);
    Task<IReadOnlyList<string>> SelectFilesAsync(string title = "Select Files", string? filter = null);
    Task<string?> SelectFolderAsync(string title = "Select Folder");
    Task<string?> SaveFileAsync(string title = "Save File", string? defaultName = null, string? filter = null);
}

public interface IDiagnosticsService
{
    Task<DiagnosticsReport> CollectReportAsync(CancellationToken cancellationToken = default);
    string FormatReport(DiagnosticsReport report, bool redactSensitivePaths = true);
}
