namespace ClamAVGui.Core.Models;

public abstract record ClamdEndpoint;

public sealed record TcpClamdEndpoint(string Host, int Port) : ClamdEndpoint
{
    public override string ToString() => $"{Host}:{Port}";
}

public sealed record UnixClamdEndpoint(string SocketPath) : ClamdEndpoint
{
    public override string ToString() => $"unix:{SocketPath}";
}

public sealed record ClamAvDaemonInstance
{
    public required DaemonOwnership Ownership { get; init; }
    public int? ProcessId { get; init; }
    public required ClamdEndpoint Endpoint { get; init; }
    public string? ConfigPath { get; init; }
}

public sealed record ClamdHealth
{
    public bool ProcessExists { get; init; }
    public bool EndpointReachable { get; init; }
    public bool ProtocolHealthy { get; init; }
    public bool DatabaseLoaded { get; init; }
    public bool OwnedByApplication { get; init; }
    public string? Version { get; init; }
    public string? Error { get; init; }
    public string? Stats { get; init; }
}

public sealed record ClamAvConfigDirective(string Name, string Value);

public sealed record ClamAvInstallation
{
    public string? RootDirectory { get; init; }
    public required string ClamScanPath { get; init; }
    public string? FreshClamPath { get; init; }
    public string? ClamdPath { get; init; }
    public string? ClamdScanPath { get; init; }
    public string? ClamOnAccPath { get; init; }
    public string? ClamConfPath { get; init; }
    public string? ConfigDirectory { get; init; }
    public string? DatabaseDirectory { get; init; }
    public string? LogDirectory { get; init; }
    public required string Version { get; init; }
    public required ClamAvInstallationSource Source { get; init; }
}

public sealed record ClamAvEffectiveConfiguration
{
    public string? FreshClamDatabaseDirectory { get; init; }
    public string? ClamdDatabaseDirectory { get; init; }
    public string? ClamScanDatabaseDirectory { get; init; }
    public string? LogFile { get; init; }
    public ClamdEndpoint? DaemonEndpoint { get; init; }
    public ConfigurationOwnership Ownership { get; init; } = ConfigurationOwnership.External;
    public string? ConfigPath { get; init; }
    public IReadOnlyList<ClamAvConfigDirective> Directives { get; init; } = Array.Empty<ClamAvConfigDirective>();
}

public sealed record ProcessRequest
{
    public required string FileName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}

public sealed record ProcessResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public bool WasCancelled { get; init; }
}

public sealed record UpdateResult
{
    public required bool Success { get; init; }
    public bool IsAlreadyUpToDate { get; init; }
    public int? SignaturesCount { get; init; }
    public string? Version { get; init; }
    public required string Output { get; init; }
    public required string Error { get; init; }
    public bool WasCancelled { get; init; }
    public int ExitCode { get; init; }
}

public sealed record HistoryEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string EventType { get; init; }
    public required string Details { get; init; }
}

public sealed record QuarantineItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string QuarantinePath { get; init; }
    public required string OriginalPath { get; init; }
    public required string ThreatName { get; init; }
    public DateTime QuarantinedAt { get; init; } = DateTime.UtcNow;
    public string FileHashSha256 { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string EngineVersion { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class ApplicationSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string? CustomClamAvPath { get; set; }
    public bool AutoUpdateDefinitions { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool StartOnLogin { get; set; }
    public bool MoveToQuarantine { get; set; }
    public string? CustomQuarantinePath { get; set; }
    public bool HeuristicAlerts { get; set; }
    public bool ScanEncrypted { get; set; }
    public bool LeaveTemps { get; set; }
    public List<string> MonitoredPaths { get; set; } = new();
    public List<string> MonitoringFilters { get; set; } = new();
    public List<string> MonitoringExclusions { get; set; } = new();
    public string? ScheduledScanPath { get; set; }
    public string ScheduledScanTime { get; set; } = "02:00";
    public bool IsScheduledScanEnabled { get; set; }
}

public sealed record PlatformCapabilities
{
    public bool SupportsClamScan { get; init; }
    public bool SupportsClamD { get; init; }
    public bool SupportsFreshClam { get; init; }
    public bool SupportsNativeOnAccess { get; init; }
    public bool SupportsReactiveMonitoring { get; init; }
    public bool SupportsScheduling { get; init; }
    public bool SupportsNotifications { get; init; }
    public bool SupportsTray { get; init; }
    public bool SupportsStartupRegistration { get; init; }
}
