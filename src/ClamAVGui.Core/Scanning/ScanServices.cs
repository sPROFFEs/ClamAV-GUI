using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Scanning;

public sealed class ScanCoordinator : IScanCoordinator
{
    private readonly SemaphoreSlim _gate;
    private readonly IClamAvScanner _scanner;
    private readonly ILogger<ScanCoordinator> _logger;

    public ScanCoordinator(
        IClamAvScanner scanner,
        int maxConcurrency = 1,
        ILogger<ScanCoordinator>? logger = null)
    {
        _scanner = scanner;
        _gate = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        _logger = logger ?? NullLogger<ScanCoordinator>.Instance;
    }

    public async Task<ScanExecutionResult> QueueAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Queuing scan request for {TargetCount} targets", request.Targets.Count);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await _scanner.ScanAsync(request, progress, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class ClamAvScanner : IClamAvScanner
{
    private readonly IEnumerable<IScanBackend> _backends;
    private readonly IQuarantineService? _quarantineService;
    private readonly ILogger<ClamAvScanner> _logger;

    public ClamAvScanner(
        IEnumerable<IScanBackend> backends,
        IQuarantineService? quarantineService = null,
        ILogger<ClamAvScanner>? logger = null)
    {
        _backends = backends;
        _quarantineService = quarantineService;
        _logger = logger ?? NullLogger<ClamAvScanner>.Instance;
    }

    public async Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Targets.Count == 0)
        {
            throw new ArgumentException("Scan request contains no targets.", nameof(request));
        }

        IScanBackend? selectedBackend = null;

        if (request.PreferredBackend.HasValue)
        {
            selectedBackend = _backends.FirstOrDefault(b => b.Kind == request.PreferredBackend.Value);
        }

        if (selectedBackend == null || !await selectedBackend.IsAvailableAsync(cancellationToken))
        {
            // Default selection hierarchy: ClamScan (most capable for local full option set) -> ClamDScan -> ClamD
            foreach (var backend in _backends.OrderBy(b => b.Kind switch
            {
                ScanBackendKind.ClamScan => 1,
                ScanBackendKind.ClamDScan => 2,
                ScanBackendKind.ClamD => 3,
                _ => 4
            }))
            {
                if (await backend.IsAvailableAsync(cancellationToken))
                {
                    selectedBackend = backend;
                    break;
                }
            }
        }

        if (selectedBackend == null)
        {
            throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "No available ClamAV scanner backend found.");
        }

        _logger.LogInformation("Starting scan with backend {BackendKind}", selectedBackend.Kind);
        var result = await selectedBackend.ScanAsync(request, progress, cancellationToken);

        // If quarantine was requested and backend is daemon/streaming (which doesn't move files directly), perform safe quarantine
        if (request.MoveToQuarantine && _quarantineService != null && result.Detections.Count > 0 && selectedBackend.Kind == ScanBackendKind.ClamD)
        {
            foreach (var threat in result.Detections)
            {
                try
                {
                    if (File.Exists(threat.FilePath))
                    {
                        await _quarantineService.QuarantineFileAsync(threat.FilePath, threat.ThreatName, "Quarantined after ClamD detection", cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to quarantine infected file: {FilePath}", threat.FilePath);
                }
            }
        }

        return result;
    }
}
