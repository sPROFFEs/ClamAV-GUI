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
    private readonly Func<ClamAvInstallation, CancellationToken, Task<UpdateResult?>>? _systemUpdate;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly ILogger<ClamAvUpdater> _logger;

    public ClamAvUpdater(
        IProcessRunner processRunner,
        IClamAvOutputParser outputParser,
        Func<Task<ClamAvInstallation?>> installationProvider,
        ILogger<ClamAvUpdater>? logger = null,
        Func<ClamAvInstallation, CancellationToken, Task<UpdateResult?>>? systemUpdate = null)
    {
        _processRunner = processRunner;
        _outputParser = outputParser;
        _installationProvider = installationProvider;
        _systemUpdate = systemUpdate;
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
            if (installation?.UsesCustomDatabaseDirectory == true && configFilePath == null)
            {
                return new UpdateResult
                {
                    Success = false,
                    Output = string.Empty,
                    Error = $"The custom signature folder is managed manually: {installation.DatabaseDirectory}. Copy your updated definitions there or clear the folder in Settings to use FreshClam.",
                    ExitCode = -1
                };
            }
            if (installation == null || string.IsNullOrWhiteSpace(installation.FreshClamPath) || !File.Exists(installation.FreshClamPath))
            {
                throw new ClamAvException(ClamAvErrorCode.ClamAvNotFound, "freshclam executable was not found on this system.");
            }

            if (configFilePath == null && _systemUpdate != null)
            {
                var systemResult = await _systemUpdate(installation, cancellationToken);
                if (systemResult != null) return systemResult;
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
