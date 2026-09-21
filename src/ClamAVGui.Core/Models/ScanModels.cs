namespace ClamAVGui.Core.Models;

public sealed record ThreatDetection
{
    public required string FilePath { get; init; }
    public required string ThreatName { get; init; }
    public string? Details { get; init; }
}

public sealed record ScanStatistics
{
    public string KnownViruses { get; init; } = "0";
    public string EngineVersion { get; init; } = string.Empty;
    public string ScannedDirectories { get; init; } = "0";
    public string ScannedFiles { get; init; } = "0";
    public string InfectedFiles { get; init; } = "0";
    public string DataScanned { get; init; } = string.Empty;
    public string TimeTaken { get; init; } = string.Empty;
}

public sealed record ScanExecutionResult
{
    public required ScanVerdict Verdict { get; init; }
    public required int ExitCode { get; init; }
    public IReadOnlyList<ThreatDetection> Detections { get; init; } = Array.Empty<ThreatDetection>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public ScanStatistics? Statistics { get; init; }
    public required ScanBackendKind Backend { get; init; }
    public string RawOutput { get; init; } = string.Empty;
    public string RawError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}

public sealed record ScanTarget
{
    public required string Path { get; init; }
    public required ScanTargetType Type { get; init; }
}

public sealed record ScanProfile
{
    public string Name { get; init; } = "Default";
    public bool Recursive { get; init; } = true;
    public bool HeuristicAlerts { get; init; }
    public bool ScanEncryptedArchives { get; init; }
    public bool DetectPua { get; init; }
    public bool ScanArchives { get; init; } = true;
    public bool ScanMail { get; init; } = true;
    public bool ScanPdf { get; init; } = true;
    public bool ScanOle2 { get; init; } = true;
    public bool ScanHtml { get; init; } = true;
    public bool LeaveTemps { get; init; }
    public SymbolicLinkPolicy SymlinkPolicy { get; init; } = SymbolicLinkPolicy.DoNotFollow;
}

public sealed record ScanBackendCapabilities
{
    public bool SupportsHeuristics { get; init; } = true;
    public bool SupportsEncryptedArchiveDetection { get; init; } = true;
    public bool SupportsStreaming { get; init; }
    public bool SupportsPathScanning { get; init; } = true;
    public bool SupportsCancellation { get; init; } = true;
}

public sealed record ScanProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public int ScannedFiles { get; init; }
    public int? TotalFiles { get; init; }
    public double? PercentComplete { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed record ScanRequest
{
    public IReadOnlyList<ScanTarget> Targets { get; init; } = Array.Empty<ScanTarget>();
    public ScanProfile Profile { get; init; } = new();
    public bool MoveToQuarantine { get; init; }
    public string? QuarantineDirectory { get; init; }
    public ScanBackendKind? PreferredBackend { get; init; }
}
