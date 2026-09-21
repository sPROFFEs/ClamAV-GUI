using System.Diagnostics;
using System.Text;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Services;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner>? logger = null)
    {
        _logger = logger ?? NullLogger<ProcessRunner>.Instance;
    }

    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        Action<string>? onStdOutLine = null,
        Action<string>? onStdErrLine = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in request.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (request.EnvironmentVariables != null)
        {
            foreach (var kvp in request.EnvironmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();
        var outputLock = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (outputLock)
            {
                stdOutBuffer.AppendLine(e.Data);
            }
            onStdOutLine?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (outputLock)
            {
                stdErrBuffer.AppendLine(e.Data);
            }
            onStdErrLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {request.FileName}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var processCompletion = process.WaitForExitAsync(cancellationToken);
            await processCompletion;

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdOutBuffer.ToString(),
                StandardError = stdErrBuffer.ToString(),
                WasCancelled = false
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Process execution cancelled: {FileName}", request.FileName);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception while terminating cancelled process");
            }

            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = stdOutBuffer.ToString(),
                StandardError = stdErrBuffer.ToString(),
                WasCancelled = true
            };
        }
    }
}
