using System.Diagnostics;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Scanning;

public sealed class ClamdScanBackend : IScanBackend
{
    private readonly IProcessRunner _processRunner;
    private readonly IClamAvOutputParser _outputParser;
    private readonly Func<Task<ClamAvInstallation?>> _installationProvider;

    public ScanBackendKind Kind => ScanBackendKind.ClamDScan;

    public ScanBackendCapabilities Capabilities => new()
    {
        SupportsHeuristics = false,
        SupportsEncryptedArchiveDetection = false,
        SupportsStreaming = false,
        SupportsPathScanning = true,
        SupportsCancellation = true
    };

    public ClamdScanBackend(
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
        return installation != null && !string.IsNullOrWhiteSpace(installation.ClamdScanPath) && File.Exists(installation.ClamdScanPath);
    }

    public async Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installation = await _installationProvider();
        if (installation == null || string.IsNullOrWhiteSpace(installation.ClamdScanPath) || !File.Exists(installation.ClamdScanPath))
        {
            throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "clamdscan executable was not found on this system.");
        }

        var arguments = new List<string> { "--stdout", "--multiscan" };

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
            FileName = installation.ClamdScanPath,
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
