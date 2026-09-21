using System.Text;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Configuration;

public sealed class ClamAvConfigurationProvider : IClamAvConfigurationProvider
{
    private readonly IClamAvConfigParser _parser;
    private readonly ILogger<ClamAvConfigurationProvider> _logger;

    public ClamAvConfigurationProvider(
        IClamAvConfigParser parser,
        ILogger<ClamAvConfigurationProvider>? logger = null)
    {
        _parser = parser;
        _logger = logger ?? NullLogger<ClamAvConfigurationProvider>.Instance;
    }

    public async Task<ClamAvEffectiveConfiguration> LoadAsync(
        ClamAvInstallation? installation,
        CancellationToken cancellationToken = default)
    {
        if (installation == null || string.IsNullOrWhiteSpace(installation.ConfigDirectory))
        {
            return new ClamAvEffectiveConfiguration();
        }

        var clamdConf = Path.Combine(installation.ConfigDirectory, "clamd.conf");
        var freshclamConf = Path.Combine(installation.ConfigDirectory, "freshclam.conf");

        string? clamdDb = null;
        string? freshclamDb = null;
        string? logFile = null;
        ClamdEndpoint? endpoint = null;
        var directives = new List<ClamAvConfigDirective>();

        if (File.Exists(clamdConf))
        {
            var content = await File.ReadAllTextAsync(clamdConf, cancellationToken);
            var parsed = _parser.Parse(content);
            directives.AddRange(parsed);

            clamdDb = _parser.GetDirectiveValue(parsed, "DatabaseDirectory");
            logFile = _parser.GetDirectiveValue(parsed, "LogFile");

            var localSocket = _parser.GetDirectiveValue(parsed, "LocalSocket");
            var tcpSocket = _parser.GetDirectiveValue(parsed, "TCPSocket");
            var tcpAddr = _parser.GetDirectiveValue(parsed, "TCPAddr") ?? "127.0.0.1";

            if (!string.IsNullOrWhiteSpace(localSocket))
            {
                endpoint = new UnixClamdEndpoint(localSocket);
            }
            else if (!string.IsNullOrWhiteSpace(tcpSocket) && int.TryParse(tcpSocket, out var port))
            {
                endpoint = new TcpClamdEndpoint(tcpAddr, port);
            }
        }

        if (File.Exists(freshclamConf))
        {
            var content = await File.ReadAllTextAsync(freshclamConf, cancellationToken);
            var parsed = _parser.Parse(content);
            freshclamDb = _parser.GetDirectiveValue(parsed, "DatabaseDirectory");
        }

        return new ClamAvEffectiveConfiguration
        {
            FreshClamDatabaseDirectory = freshclamDb ?? installation.DatabaseDirectory,
            ClamdDatabaseDirectory = clamdDb ?? installation.DatabaseDirectory,
            ClamScanDatabaseDirectory = installation.DatabaseDirectory,
            LogFile = logFile,
            DaemonEndpoint = endpoint,
            Ownership = ConfigurationOwnership.External,
            ConfigPath = File.Exists(clamdConf) ? clamdConf : null,
            Directives = directives
        };
    }

    public async Task<string> InitializeManagedConfigAsync(
        ClamAvInstallation installation,
        string targetDirectory,
        ClamdEndpoint defaultEndpoint,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var dbDir = installation.DatabaseDirectory ?? Path.Combine(targetDirectory, "database");
        Directory.CreateDirectory(dbDir);
        var logDir = installation.LogDirectory ?? targetDirectory;
        Directory.CreateDirectory(logDir);

        var clamdConfPath = Path.Combine(targetDirectory, "clamd.conf");
        var freshclamConfPath = Path.Combine(targetDirectory, "freshclam.conf");

        var sbClamd = new StringBuilder();
        sbClamd.AppendLine($"DatabaseDirectory \"{dbDir}\"");
        sbClamd.AppendLine($"LogFile \"{Path.Combine(logDir, "clamd.log")}\"");
        sbClamd.AppendLine("LogTime yes");
        sbClamd.AppendLine("LogClean no");
        sbClamd.AppendLine("LogVerbose no");

        if (defaultEndpoint is UnixClamdEndpoint unix)
        {
            sbClamd.AppendLine($"LocalSocket \"{unix.SocketPath}\"");
            sbClamd.AppendLine("FixStaleSocket yes");
        }
        else if (defaultEndpoint is TcpClamdEndpoint tcp)
        {
            sbClamd.AppendLine($"TCPSocket {tcp.Port}");
            sbClamd.AppendLine($"TCPAddr {tcp.Host}"); // Secure loopback bind
        }

        // Atomic file writes: write temp file then rename
        await WriteFileAtomicallyAsync(clamdConfPath, sbClamd.ToString(), cancellationToken);

        var sbFresh = new StringBuilder();
        sbFresh.AppendLine($"DatabaseDirectory \"{dbDir}\"");
        sbFresh.AppendLine($"UpdateLogFile \"{Path.Combine(logDir, "freshclam.log")}\"");
        sbFresh.AppendLine("LogTime yes");
        sbFresh.AppendLine("LogVerbose no");
        sbFresh.AppendLine("DatabaseMirror database.clamav.net");

        await WriteFileAtomicallyAsync(freshclamConfPath, sbFresh.ToString(), cancellationToken);

        return clamdConfPath;
    }

    private static async Task WriteFileAtomicallyAsync(string destinationPath, string content, CancellationToken cancellationToken)
    {
        var tempFile = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(tempFile, content, Encoding.UTF8, cancellationToken);
        File.Move(tempFile, destinationPath, overwrite: true);
    }
}
