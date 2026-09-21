using System.Diagnostics;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Scanning;

public sealed class ClamScanBackend : IScanBackend
{
    private readonly IProcessRunner _processRunner;
    private readonly IClamAvOutputParser _outputParser;
    private readonly Func<Task<ClamAvInstallation?>> _installationProvider;

    public ScanBackendKind Kind => ScanBackendKind.ClamScan;

    public ScanBackendCapabilities Capabilities => new()
    {
        SupportsHeuristics = true,
        SupportsEncryptedArchiveDetection = true,
        SupportsStreaming = false,
        SupportsPathScanning = true,
        SupportsCancellation = true
    };

    public ClamScanBackend(
        IProcessRunner processRunner,
        IClamAvOutputParser outputParser,
        Func<Task<ClamAvInstallation?>> installationProvider)
    {
        _processRunner = processRunner;
        _outputParser = outputParser;
        _installationProvider = installationProvider;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var installation = await _installationProvider();
        return installation != null && File.Exists(installation.ClamScanPath);
    }

    public async Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installation = await _installationProvider();
        if (installation == null || !File.Exists(installation.ClamScanPath))
        {
            throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "clamscan executable was not found on this system.");
        }

        var arguments = new List<string> { "--stdout" };

        if (!string.IsNullOrWhiteSpace(installation.DatabaseDirectory) && Directory.Exists(installation.DatabaseDirectory))
        {
            arguments.Add("--database");
            arguments.Add(installation.DatabaseDirectory);
        }

        if (request.Profile.Recursive)
        {
            arguments.Add("-r");
        }

        if (request.Profile.HeuristicAlerts) arguments.Add("--heuristic-alerts=yes");
        if (request.Profile.ScanEncryptedArchives) arguments.Add("--alert-encrypted=yes");
        if (request.Profile.LeaveTemps) arguments.Add("--leave-temps=yes");
        if (request.Profile.DetectPua) arguments.Add("--detect-pua=yes");
        if (!request.Profile.ScanArchives) arguments.Add("--scan-archive=no");
        if (!request.Profile.ScanMail) arguments.Add("--scan-mail=no");
        if (!request.Profile.ScanPdf) arguments.Add("--scan-pdf=no");
        if (!request.Profile.ScanOle2) arguments.Add("--scan-ole2=no");
        if (!request.Profile.ScanHtml) arguments.Add("--scan-html=no");

        if (request.MoveToQuarantine && !string.IsNullOrWhiteSpace(request.QuarantineDirectory))
        {
            Directory.CreateDirectory(request.QuarantineDirectory);
            arguments.Add("--move");
            arguments.Add(request.QuarantineDirectory);
        }

        foreach (var target in request.Targets)
        {
            arguments.Add(target.Path);
        }

        int scannedFilesCount = 0;
        var stopwatch = Stopwatch.StartNew();

        var processRequest = new ProcessRequest
        {
            FileName = installation.ClamScanPath,
            Arguments = arguments,
            WorkingDirectory = installation.RootDirectory
        };

        var processResult = await _processRunner.RunAsync(
            processRequest,
            onStdOutLine: line =>
            {
                var parsed = _outputParser.TryParseLine(line);
                if (parsed.HasValue)
                {
                    scannedFilesCount++;
                    progress?.Report(new ScanProgress
                    {
                        CurrentFile = parsed.Value.FilePath,
                        ScannedFiles = scannedFilesCount,
                        StatusMessage = parsed.Value.Status
                    });
                }
            },
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        if (processResult.WasCancelled)
        {
            return new ScanExecutionResult
            {
                Verdict = ScanVerdict.Cancelled,
                ExitCode = processResult.ExitCode,
                Backend = Kind,
                RawOutput = processResult.StandardOutput,
                RawError = processResult.StandardError,
                Duration = stopwatch.Elapsed
            };
        }

        return _outputParser.Parse(
            processResult.StandardOutput,
            processResult.StandardError,
            processResult.ExitCode,
            Kind,
            stopwatch.Elapsed);
    }
}
