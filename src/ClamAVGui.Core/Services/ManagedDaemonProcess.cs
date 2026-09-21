using System.Diagnostics;
using ClamAVGui.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Services;

public sealed class ManagedDaemonProcess : IManagedDaemonProcess
{
    private readonly Process _process;
    private readonly TaskCompletionSource<int> _completionTcs = new();
    private readonly ILogger _logger;
    private bool _disposed;

    public int ProcessId => _process.Id;
    public Task<int> Completion => _completionTcs.Task;

    public ManagedDaemonProcess(Process process, ILogger? logger = null)
    {
        _process = process;
        _logger = logger ?? NullLogger.Instance;

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            _completionTcs.TrySetResult(_process.ExitCode);
            _logger.LogInformation("Managed clamd process {Pid} exited with code {ExitCode}", _process.Id, _process.ExitCode);
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process.HasExited)
        {
            return;
        }

        try
        {
            _process.Kill(entireProcessTree: true);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await _process.WaitForExitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception while stopping managed clamd process {Pid}", _process.Id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!_process.HasExited)
            {
                await StopAsync();
            }
            _process.Dispose();
        }
    }
}
