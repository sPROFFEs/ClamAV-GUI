using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Services;

public sealed class ClamAvUpdater : IClamAvUpdater
{
    private readonly IProcessRunner _processRunner;
    private readonly IClamAvOutputParser _outputParser;
    private readonly Func<Task<ClamAvInstallation?>> _installationProvider;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly ILogger<ClamAvUpdater> _logger;

    public ClamAvUpdater(
        IProcessRunner processRunner,
        IClamAvOutputParser outputParser,
        Func<Task<ClamAvInstallation?>> installationProvider,
        ILogger<ClamAvUpdater>? logger = null)
    {
        _processRunner = processRunner;
        _outputParser = outputParser;
        _installationProvider = installationProvider;
        _logger = logger ?? NullLogger<ClamAvUpdater>.Instance;
    }

    public async Task<UpdateResult> UpdateDefinitionsAsync(
        string? configFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _updateLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A definition update is already in progress.");
        }

        try
        {
            var installation = await _installationProvider();
            if (installation == null || string.IsNullOrWhiteSpace(installation.FreshClamPath) || !File.Exists(installation.FreshClamPath))
            {
                throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "freshclam executable was not found on this system.");
            }

            // Debian's freshclam configuration writes to /var/lib/clamav and
            // /var/log/clamav as the clamav user. Run its service through the
            // desktop authorization prompt instead of launching it as the GUI user.
            if (OperatingSystem.IsLinux() && configFilePath == null &&
                string.Equals(installation.ConfigDirectory, "/etc/clamav", StringComparison.Ordinal) &&
                string.Equals(installation.DatabaseDirectory, "/var/lib/clamav", StringComparison.Ordinal) &&
                File.Exists("/usr/bin/systemctl"))
            {
                if (!File.Exists("/usr/bin/pkexec"))
                {
                    return new UpdateResult
                    {
                        Success = false,
                        Output = string.Empty,
                        Error = "System definitions are managed by clamav-freshclam. Run: sudo systemctl restart clamav-freshclam",
                        ExitCode = -1
                    };
                }

                var serviceResult = await _processRunner.RunAsync(new ProcessRequest
                {
                    FileName = "/usr/bin/pkexec",
                    Arguments = new[] { "/usr/bin/systemctl", "restart", "clamav-freshclam" }
                }, cancellationToken: cancellationToken);

                return new UpdateResult
                {
                    Success = serviceResult.ExitCode == 0 && !serviceResult.WasCancelled,
                    IsManagedBySystem = true,
                    WasCancelled = serviceResult.WasCancelled,
                    Output = serviceResult.StandardOutput,
                    Error = serviceResult.ExitCode == 0 ? string.Empty :
                        $"{(string.IsNullOrWhiteSpace(serviceResult.StandardError) ? serviceResult.StandardOutput : serviceResult.StandardError).Trim()} Restart clamav-freshclam from a terminal if authorization is unavailable.".Trim(),
                    ExitCode = serviceResult.ExitCode
                };
            }

            var arguments = new List<string> { "--stdout" };
            var configFile = configFilePath ?? (installation.ConfigDirectory != null ? Path.Combine(installation.ConfigDirectory, "freshclam.conf") : null);

            if (!string.IsNullOrWhiteSpace(configFile) && File.Exists(configFile))
            {
                arguments.Add("--config-file");
                arguments.Add(configFile);
            }

            var request = new ProcessRequest
            {
                FileName = installation.FreshClamPath,
                Arguments = arguments,
                WorkingDirectory = installation.RootDirectory
            };

            _logger.LogInformation("Starting freshclam definitions update...");
            var processResult = await _processRunner.RunAsync(request, cancellationToken: cancellationToken);

            return _outputParser.ParseUpdateOutput(
                processResult.StandardOutput,
                processResult.StandardError,
                processResult.ExitCode,
                processResult.WasCancelled);
        }
        finally
        {
            _updateLock.Release();
        }
    }
}
