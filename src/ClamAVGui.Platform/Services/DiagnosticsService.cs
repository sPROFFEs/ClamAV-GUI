using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;

namespace ClamAVGui.Platform.Services;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly IPlatformService _platformService;
    private readonly IClamAvBinaryLocator _binaryLocator;
    private readonly IClamAvDaemon _daemon;
    private readonly IClamAvConfigurationProvider _configProvider;
    private readonly ISettingsService _settingsService;

    public DiagnosticsService(
        IPlatformService platformService,
        IClamAvBinaryLocator binaryLocator,
        IClamAvDaemon daemon,
        IClamAvConfigurationProvider configProvider,
        ISettingsService settingsService)
    {
        _platformService = platformService;
        _binaryLocator = binaryLocator;
        _daemon = daemon;
        _configProvider = configProvider;
        _settingsService = settingsService;
    }

    public async Task<DiagnosticsReport> CollectReportAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
        var installation = await _binaryLocator.FindInstallationAsync(settings.CustomClamAvPath, cancellationToken);
        var config = await _configProvider.LoadAsync(installation, cancellationToken);
        var health = await _daemon.CheckHealthAsync(cancellationToken);

        var healthLines = new List<string>();
        if (installation == null)
        {
            healthLines.Add("FAIL: ClamAV installation was not found.");
        }
        else
        {
            healthLines.Add($"OK: clamscan found at '{installation.ClamScanPath}'");
            healthLines.Add(installation.FreshClamPath != null ? $"OK: freshclam found at '{installation.FreshClamPath}'" : "WARN: freshclam missing.");
            healthLines.Add(installation.ClamdPath != null ? $"OK: clamd found at '{installation.ClamdPath}'" : "INFO: clamd missing (daemon features unavailable).");
            healthLines.Add(installation.DatabaseDirectory != null && Directory.Exists(installation.DatabaseDirectory) ? $"OK: Database directory exists at '{installation.DatabaseDirectory}'" : "WARN: Database directory missing.");
            healthLines.Add(health.ProtocolHealthy ? $"OK: ClamAV daemon is healthy ({health.Version})" : (health.EndpointReachable ? $"WARN: Daemon reachable but protocol unhealthy: {health.Error}" : $"INFO: Daemon endpoint unreachable: {health.Error}"));
        }

        return new DiagnosticsReport
        {
            ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.0.0",
            DotNetRuntime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            ClamScanPath = installation?.ClamScanPath,
            FreshClamPath = installation?.FreshClamPath,
            ClamdPath = installation?.ClamdPath,
            ClamdScanPath = installation?.ClamdScanPath,
            ClamOnAccPath = installation?.ClamOnAccPath,
            ConfigDirectory = installation?.ConfigDirectory,
            DatabaseDirectory = installation?.DatabaseDirectory,
            DaemonEndpoint = config.DaemonEndpoint?.ToString(),
            DaemonRunning = health.EndpointReachable,
            DaemonVersion = health.Version,
            DaemonStats = health.Stats,
            RealTimeMode = _platformService.Capabilities.SupportsNativeOnAccess ? "Native (clamonacc/fanotify)" : (_platformService.Capabilities.SupportsReactiveMonitoring ? "Reactive (file watcher)" : "Unsupported"),
            SchedulerBackend = _platformService.Capabilities.SupportsScheduling ? _platformService.Platform.ToString() : "None",
            QuarantineDirectory = settings.CustomQuarantinePath ?? Path.Combine(_platformService.UserDataDirectory, "Quarantine"),
            HealthCheckOutput = healthLines
        };
    }

    public string FormatReport(DiagnosticsReport report, bool redactSensitivePaths = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ClamAV GUI Diagnostics Report ===");
        sb.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"App Version:     {report.ApplicationVersion}");
        sb.AppendLine($"OS / Platform:   {report.OperatingSystem} ({report.Architecture})");
        sb.AppendLine($".NET Runtime:    {report.DotNetRuntime}");
        sb.AppendLine();
        sb.AppendLine("--- ClamAV Components ---");
        sb.AppendLine($"clamscan:        {Redact(report.ClamScanPath, redactSensitivePaths)}");
        sb.AppendLine($"freshclam:       {Redact(report.FreshClamPath, redactSensitivePaths)}");
        sb.AppendLine($"clamd:           {Redact(report.ClamdPath, redactSensitivePaths)}");
        sb.AppendLine($"clamdscan:       {Redact(report.ClamdScanPath, redactSensitivePaths)}");
        sb.AppendLine($"clamonacc:       {Redact(report.ClamOnAccPath, redactSensitivePaths)}");
        sb.AppendLine($"Config Dir:      {Redact(report.ConfigDirectory, redactSensitivePaths)}");
        sb.AppendLine($"Database Dir:    {Redact(report.DatabaseDirectory, redactSensitivePaths)}");
        sb.AppendLine();
        sb.AppendLine("--- Daemon Status ---");
        sb.AppendLine($"Daemon Endpoint: {report.DaemonEndpoint ?? "None"}");
        sb.AppendLine($"Daemon Running:  {(report.DaemonRunning ? "Yes" : "No")}");
        sb.AppendLine($"Daemon Version:  {report.DaemonVersion ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("--- Health Check Summary ---");
        foreach (var line in report.HealthCheckOutput)
        {
            sb.AppendLine(Redact(line, redactSensitivePaths));
        }

        return sb.ToString();
    }

    private static string? Redact(string? path, bool redact)
    {
        if (path == null || !redact) return path;
        var user = Environment.UserName;
        if (!string.IsNullOrEmpty(user) && path.Contains(user, StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace(user, "<REDACTED_USER>", StringComparison.OrdinalIgnoreCase);
        }
        return path;
    }
}
