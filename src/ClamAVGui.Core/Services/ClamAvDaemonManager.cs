using System.Diagnostics;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Services;

public sealed class ClamAvDaemonManager : IClamAvDaemon
{
    private readonly IClamdProtocol _protocol;
    private readonly Func<Task<ClamAvInstallation?>> _installationProvider;
    private readonly Func<ClamdEndpoint> _endpointResolver;
    private readonly ILogger<ClamAvDaemonManager> _logger;
    private readonly object _lock = new();

    private ManagedDaemonProcess? _managedProcess;
    private ClamAvDaemonInstance? _currentInstance;

    public ClamAvDaemonInstance? CurrentInstance
    {
        get
        {
            lock (_lock)
            {
                return _currentInstance;
            }
        }
    }

    public ClamAvDaemonManager(
        IClamdProtocol protocol,
        Func<Task<ClamAvInstallation?>> installationProvider,
        Func<ClamdEndpoint> endpointResolver,
        ILogger<ClamAvDaemonManager>? logger = null)
    {
        _protocol = protocol;
        _installationProvider = installationProvider;
        _endpointResolver = endpointResolver;
        _logger = logger ?? NullLogger<ClamAvDaemonManager>.Instance;
    }

    public async Task<ClamdHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = _endpointResolver();
        bool isOwned;
        int? pid;

        lock (_lock)
        {
            isOwned = _managedProcess != null && !_managedProcess.Completion.IsCompleted;
            pid = isOwned ? _managedProcess!.ProcessId : null;
        }

        try
        {
            var ping = await _protocol.PingAsync(endpoint, TimeSpan.FromSeconds(2), cancellationToken);
            var isPong = ping.Contains("PONG", StringComparison.OrdinalIgnoreCase);

            if (isPong)
            {
                string? version = null;
                string? stats = null;
                try
                {
                    version = await _protocol.GetVersionAsync(endpoint, TimeSpan.FromSeconds(2), cancellationToken);
                    stats = await _protocol.GetStatsAsync(endpoint, TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to retrieve daemon version or stats");
                }

                return new ClamdHealth
                {
                    ProcessExists = pid.HasValue || true,
                    EndpointReachable = true,
                    ProtocolHealthy = true,
                    DatabaseLoaded = !string.IsNullOrWhiteSpace(version),
                    OwnedByApplication = isOwned,
                    Version = version,
                    Stats = stats,
                    Error = null
                };
            }

            return new ClamdHealth
            {
                ProcessExists = isOwned,
                EndpointReachable = true,
                ProtocolHealthy = false,
                DatabaseLoaded = false,
                OwnedByApplication = isOwned,
                Error = $"Unexpected response: {ping}"
            };
        }
        catch (Exception ex)
        {
            return new ClamdHealth
            {
                ProcessExists = isOwned,
                EndpointReachable = false,
                ProtocolHealthy = false,
                DatabaseLoaded = false,
                OwnedByApplication = isOwned,
                Error = ex.Message
            };
        }
    }

    public async Task<IManagedDaemonProcess> StartManagedAsync(
        string configPath,
        ClamdEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var installation = await _installationProvider();
        if (installation == null || string.IsNullOrWhiteSpace(installation.ClamdPath) || !File.Exists(installation.ClamdPath))
        {
            throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "clamd executable was not found on this system.");
        }

        lock (_lock)
        {
            if (_managedProcess != null && !_managedProcess.Completion.IsCompleted)
            {
                throw new InvalidOperationException("A managed ClamAV daemon is already running.");
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = installation.ClamdPath,
            WorkingDirectory = installation.RootDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
        {
            startInfo.ArgumentList.Add("--config-file");
            startInfo.ArgumentList.Add(configPath);
        }
        startInfo.ArgumentList.Add("--foreground");

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new ClamAvException(ClamAvErrorCode.ClamAvExecutionFailed, "Failed to launch clamd process.");
        }

        var managed = new ManagedDaemonProcess(process, _logger);

        lock (_lock)
        {
            _managedProcess = managed;
            _currentInstance = new ClamAvDaemonInstance
            {
                Ownership = DaemonOwnership.ManagedByApplication,
                ProcessId = managed.ProcessId,
                Endpoint = endpoint,
                ConfigPath = configPath
            };
        }

        // Wait for daemon to become responsive
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var isHealthy = false;

        while (stopwatch.Elapsed < timeout && !process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var pong = await _protocol.PingAsync(endpoint, TimeSpan.FromMilliseconds(400), cancellationToken);
                if (pong.Contains("PONG", StringComparison.OrdinalIgnoreCase))
                {
                    isHealthy = true;
                    break;
                }
            }
            catch
            {
                // Daemon initializing, wait and retry
            }

            await Task.Delay(300, cancellationToken);
        }

        if (!isHealthy)
        {
            await managed.StopAsync(cancellationToken);
            lock (_lock)
            {
                _managedProcess = null;
                _currentInstance = null;
            }

            if (process.HasExited)
            {
                throw new ClamAvException(ClamAvErrorCode.DaemonUnavailable, $"clamd exited unexpectedly with code {process.ExitCode}.");
            }
            throw new ClamAvException(ClamAvErrorCode.DaemonUnavailable, "Timed out waiting for clamd to respond to PING.");
        }

        return managed;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ManagedDaemonProcess? toStop;
        ClamAvDaemonInstance? instance;

        lock (_lock)
        {
            toStop = _managedProcess;
            instance = _currentInstance;
        }

        // Rule P0: ONLY stop if ownership is ManagedByApplication and PID matches
        if (instance != null && instance.Ownership == DaemonOwnership.ManagedByApplication && toStop != null)
        {
            _logger.LogInformation("Stopping managed ClamAV daemon (PID: {Pid})...", toStop.ProcessId);
            try
            {
                await _protocol.ShutdownAsync(instance.Endpoint, TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch
            {
                // Transport might close immediately
            }

            await toStop.StopAsync(cancellationToken);

            lock (_lock)
            {
                _managedProcess = null;
                _currentInstance = null;
            }
        }
        else
        {
            _logger.LogWarning("Refusing to terminate unmanaged or external ClamAV daemon.");
        }
    }

    public async Task<string> ReloadDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = _endpointResolver();
        return await _protocol.ReloadAsync(endpoint, TimeSpan.FromSeconds(5), cancellationToken);
    }
}
