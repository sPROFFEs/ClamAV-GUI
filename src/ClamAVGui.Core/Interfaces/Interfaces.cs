using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Interfaces;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        Action<string>? onStdOutLine = null,
        Action<string>? onStdErrLine = null,
        CancellationToken cancellationToken = default);
}

public interface IClamAvOutputParser
{
    ScanExecutionResult Parse(
        string stdout,
        string stderr,
        int exitCode,
        ScanBackendKind backend,
        TimeSpan duration = default);

    (string FilePath, string Status)? TryParseLine(string line);

    UpdateResult ParseUpdateOutput(
        string stdout,
        string stderr,
        int exitCode,
        bool wasCancelled);
}

public interface IClamAvConfigParser
{
    IReadOnlyList<ClamAvConfigDirective> Parse(string content);
    string? GetDirectiveValue(IEnumerable<ClamAvConfigDirective> directives, string directiveName);
    string SetOrUpdateDirective(string originalContent, string directiveName, string newValue);
}

public interface IClamdTransport : IAsyncDisposable
{
    Task<Stream> ConnectAsync(CancellationToken cancellationToken = default);
}

public interface IClamdTransportFactory
{
    IClamdTransport CreateTransport(ClamdEndpoint endpoint);
}

public interface IClamdProtocol
{
    Task<string> PingAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task<string> GetVersionAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task<string> GetStatsAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task<string> ReloadAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task ShutdownAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task<string> ScanPathAsync(ClamdEndpoint endpoint, string path, bool contScan = true, TimeSpan timeout = default, CancellationToken cancellationToken = default);
    Task<string> ScanStreamAsync(ClamdEndpoint endpoint, Stream stream, TimeSpan timeout = default, CancellationToken cancellationToken = default);
}

public interface IScanBackend
{
    ScanBackendKind Kind { get; }
    ScanBackendCapabilities Capabilities { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IScanCoordinator
{
    Task<ScanExecutionResult> QueueAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IClamAvScanner
{
    Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IClamAvUpdater
{
    Task<UpdateResult> UpdateDefinitionsAsync(
        string? configFilePath = null,
        CancellationToken cancellationToken = default);
}

public interface IManagedDaemonProcess : IAsyncDisposable
{
    int ProcessId { get; }
    Task<int> Completion { get; }
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IClamAvDaemon
{
    ClamAvDaemonInstance? CurrentInstance { get; }
    Task<ClamdHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<IManagedDaemonProcess> StartManagedAsync(
        string configPath,
        ClamdEndpoint endpoint,
        CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<string> ReloadDatabaseAsync(CancellationToken cancellationToken = default);
}

public interface IClamAvBinaryLocator
{
    Task<ClamAvInstallation?> FindInstallationAsync(
        string? customPath = null,
        CancellationToken cancellationToken = default);
}

public interface IClamAvConfigurationProvider
{
    Task<ClamAvEffectiveConfiguration> LoadAsync(
        ClamAvInstallation? installation,
        CancellationToken cancellationToken = default);
    Task<string> InitializeManagedConfigAsync(
        ClamAvInstallation installation,
        string targetDirectory,
        ClamdEndpoint defaultEndpoint,
        CancellationToken cancellationToken = default);
}

public interface IHistoryService
{
    Task<IReadOnlyList<HistoryEvent>> LoadHistoryAsync(CancellationToken cancellationToken = default);
    Task LogEventAsync(string eventType, string details, CancellationToken cancellationToken = default);
    Task DeleteHistoryEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
    Task ExportAsJsonAsync(string outputPath, CancellationToken cancellationToken = default);
    Task ExportAsCsvAsync(string outputPath, CancellationToken cancellationToken = default);
}

public interface IQuarantineService
{
    Task<IReadOnlyList<QuarantineItem>> LoadItemsAsync(CancellationToken cancellationToken = default);
    Task<QuarantineItem> QuarantineFileAsync(string sourcePath, string threatName, string? notes = null, CancellationToken cancellationToken = default);
    Task RestoreItemAsync(Guid id, string? targetPath = null, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearMissingFilesAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
