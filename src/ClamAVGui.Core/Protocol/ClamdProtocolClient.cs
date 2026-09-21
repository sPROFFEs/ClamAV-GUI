using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Protocol;

public sealed class ClamdProtocolClient : IClamdProtocol
{
    private readonly IClamdTransportFactory _transportFactory;
    private readonly ILogger<ClamdProtocolClient> _logger;

    public ClamdProtocolClient(
        IClamdTransportFactory transportFactory,
        ILogger<ClamdProtocolClient>? logger = null)
    {
        _transportFactory = transportFactory;
        _logger = logger ?? NullLogger<ClamdProtocolClient>.Instance;
    }

    private static TimeSpan DefaultTimeout(TimeSpan provided) =>
        provided > TimeSpan.Zero ? provided : TimeSpan.FromSeconds(15);

    public async Task<string> PingAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync(endpoint, "PING", timeout, cancellationToken);
    }

    public async Task<string> GetVersionAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync(endpoint, "VERSION", timeout, cancellationToken);
    }

    public async Task<string> GetStatsAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync(endpoint, "STATS", timeout, cancellationToken);
    }

    public async Task<string> ReloadAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync(endpoint, "RELOAD", timeout, cancellationToken);
    }

    public async Task ShutdownAsync(ClamdEndpoint endpoint, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendCommandAsync(endpoint, "SHUTDOWN", timeout, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Shutdown command closed transport as expected");
        }
    }

    public async Task<string> ScanPathAsync(ClamdEndpoint endpoint, string path, bool contScan = true, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var cmd = contScan ? $"CONTSCAN {path}" : $"SCAN {path}";
        return await SendCommandAsync(endpoint, cmd, timeout > TimeSpan.Zero ? timeout : TimeSpan.FromMinutes(10), cancellationToken);
    }

    public async Task<string> ScanStreamAsync(ClamdEndpoint endpoint, Stream stream, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromMinutes(5));

        await using var transport = _transportFactory.CreateTransport(endpoint);
        var netStream = await transport.ConnectAsync(cts.Token);

        // Send zINSTREAM\0
        var commandBytes = Encoding.UTF8.GetBytes("zINSTREAM\0");
        await netStream.WriteAsync(commandBytes, cts.Token);
        await netStream.FlushAsync(cts.Token);

        // Stream chunks: [4 bytes size in big endian][data bytes]
        var buffer = new byte[8192];
        var lengthHeader = new byte[4];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cts.Token)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthHeader, (uint)bytesRead);
            await netStream.WriteAsync(lengthHeader, cts.Token);
            await netStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
        }

        // Terminal chunk (0 length)
        BinaryPrimitives.WriteUInt32BigEndian(lengthHeader, 0);
        await netStream.WriteAsync(lengthHeader, cts.Token);
        await netStream.FlushAsync(cts.Token);

        // Read response
        using var memoryStream = new MemoryStream();
        while ((bytesRead = await netStream.ReadAsync(buffer, cts.Token)) > 0)
        {
            memoryStream.Write(buffer, 0, bytesRead);
        }

        var response = Encoding.UTF8.GetString(memoryStream.ToArray()).TrimEnd('\0', '\r', '\n');
        return response;
    }

    private async Task<string> SendCommandAsync(
        ClamdEndpoint endpoint,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeout(timeout));

        try
        {
            await using var transport = _transportFactory.CreateTransport(endpoint);
            var stream = await transport.ConnectAsync(cts.Token);

            // NUL-delimited command framing: zCOMMAND\0
            var payload = $"z{command}\0";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await stream.WriteAsync(bytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            using var memoryStream = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
            {
                memoryStream.Write(buffer, 0, read);
            }

            var response = Encoding.UTF8.GetString(memoryStream.ToArray()).TrimEnd('\0', '\r', '\n');
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ClamAvException(ClamAvErrorCode.DaemonUnavailable, $"Timed out waiting for response to command '{command}' from {endpoint}");
        }
        catch (SocketException ex)
        {
            throw new ClamAvException(ClamAvErrorCode.DaemonUnavailable, $"Failed to connect to ClamAV daemon at {endpoint}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ClamAvException)
        {
            throw new ClamAvException(ClamAvErrorCode.DaemonProtocolError, $"Error communicating with ClamAV daemon: {ex.Message}", ex);
        }
    }
}
