using System.Diagnostics;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Scanning;

public sealed class ClamdBackend : IScanBackend
{
    private readonly IClamdProtocol _protocol;
    private readonly Func<ClamdEndpoint?> _endpointProvider;
    private readonly IClamAvOutputParser _outputParser;

    public ScanBackendKind Kind => ScanBackendKind.ClamD;

    public ScanBackendCapabilities Capabilities => new()
    {
        SupportsHeuristics = false,
        SupportsEncryptedArchiveDetection = false,
        SupportsStreaming = true,
        SupportsPathScanning = true,
        SupportsCancellation = true
    };

    public ClamdBackend(
        IClamdProtocol protocol,
        Func<ClamdEndpoint?> endpointProvider,
        IClamAvOutputParser outputParser)
    {
        _protocol = protocol;
        _endpointProvider = endpointProvider;
        _outputParser = outputParser;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = _endpointProvider();
        if (endpoint == null) return false;

        try
        {
            var ping = await _protocol.PingAsync(endpoint, TimeSpan.FromSeconds(2), cancellationToken);
            return ping.Contains("PONG", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _endpointProvider();
        if (endpoint == null)
        {
            throw new ClamAvException(ClamAvErrorCode.DaemonUnavailable, "No clamd endpoint is currently configured or available.");
        }

        var stopwatch = Stopwatch.StartNew();
        var detections = new List<ThreatDetection>();
        var errors = new List<string>();
        var scannedFilesCount = 0;
        var fullOutput = new List<string>();

        foreach (var target in request.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await _protocol.ScanPathAsync(endpoint, target.Path, contScan: true, cancellationToken: cancellationToken);
                fullOutput.Add(response);

                var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parsed = _outputParser.TryParseLine(line);
                    if (parsed.HasValue)
                    {
                        var (filePath, status) = parsed.Value;
                        scannedFilesCount++;
                        progress?.Report(new ScanProgress
                        {
                            CurrentFile = filePath,
                            ScannedFiles = scannedFilesCount,
                            StatusMessage = status
                        });

                        if (status.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
                        {
                            var threatName = status[..^"FOUND".Length].Trim();
                            detections.Add(new ThreatDetection
                            {
                                FilePath = filePath,
                                ThreatName = string.IsNullOrWhiteSpace(threatName) ? "UnknownThreat" : threatName,
                                Details = status
                            });
                        }
                        else if (status.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"{filePath}: {status}");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"Daemon scan error for {target.Path}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        var verdict = errors.Count > 0 && detections.Count == 0 && scannedFilesCount == 0
            ? ScanVerdict.Error
            : (detections.Count > 0 ? ScanVerdict.Infected : ScanVerdict.Clean);

        return new ScanExecutionResult
        {
            Verdict = verdict,
            ExitCode = verdict == ScanVerdict.Infected ? 1 : (verdict == ScanVerdict.Clean ? 0 : 2),
            Detections = detections,
            Errors = errors,
            Statistics = new ScanStatistics
            {
                ScannedFiles = scannedFilesCount.ToString(),
                InfectedFiles = detections.Count.ToString(),
                TimeTaken = $"{stopwatch.Elapsed.TotalSeconds:F2} sec"
            },
            Backend = Kind,
            RawOutput = string.Join(Environment.NewLine, fullOutput),
            Duration = stopwatch.Elapsed
        };
    }
}
