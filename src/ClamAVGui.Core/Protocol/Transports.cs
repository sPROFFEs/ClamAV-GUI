using System.Net.Sockets;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Protocol;

public sealed class TcpClamdTransport : IClamdTransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpClamdTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<Stream> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, cancellationToken);
        _stream = _client.GetStream();
        return _stream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        _client?.Dispose();
        _client = null;
    }
}

public sealed class UnixSocketClamdTransport : IClamdTransport
{
    private readonly string _socketPath;
    private Socket? _socket;
    private NetworkStream? _stream;

    public UnixSocketClamdTransport(string socketPath)
    {
        _socketPath = socketPath;
    }

    public async Task<Stream> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);
        await _socket.ConnectAsync(endpoint, cancellationToken);
        _stream = new NetworkStream(_socket, ownsSocket: true);
        return _stream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        _socket?.Dispose();
        _socket = null;
    }
}

public sealed class ClamdTransportFactory : IClamdTransportFactory
{
    public IClamdTransport CreateTransport(ClamdEndpoint endpoint)
    {
        return endpoint switch
        {
            TcpClamdEndpoint tcp => new TcpClamdTransport(tcp.Host, tcp.Port),
            UnixClamdEndpoint unix => new UnixSocketClamdTransport(unix.SocketPath),
            _ => throw new NotSupportedException($"Unsupported endpoint type: {endpoint?.GetType().Name}")
        };
    }
}
