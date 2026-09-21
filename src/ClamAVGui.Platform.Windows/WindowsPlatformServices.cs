using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using Microsoft.Win32;

namespace ClamAVGui.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformService : IPlatformService
{
    public PlatformKind Platform => PlatformKind.Windows;
    public string Architecture => RuntimeInformation.OSArchitecture.ToString();

    public string UserDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClamAVGui");

    public string UserCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClamAVGui", "Cache");

    public string RuntimeDirectory =>
        Path.Combine(Path.GetTempPath(), "ClamAVGui");

    public PlatformCapabilities Capabilities => new()
    {
        SupportsClamScan = true,
        SupportsClamD = true,
        SupportsFreshClam = true,
        SupportsNativeOnAccess = false,
        SupportsReactiveMonitoring = true,
        SupportsScheduling = true,
        SupportsNotifications = true,
        SupportsTray = true,
        SupportsStartupRegistration = true
    };

    public void OpenContainingFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select, \"{filePath}\"",
            UseShellExecute = true
        });
    }
}

public sealed class WindowsClamAvBinaryLocator : IClamAvBinaryLocator
{
    public Task<ClamAvInstallation?> FindInstallationAsync(
        string? customPath = null,
        CancellationToken cancellationToken = default)
    {
        var searchRoots = new List<string>();

        if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
        {
            searchRoots.Add(customPath);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            searchRoots.AddRange(pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(progFiles))
        {
            searchRoots.Add(Path.Combine(progFiles, "ClamAV"));
            searchRoots.Add(Path.Combine(progFiles, "ClamAV-GUI"));
        }

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;

            var clamscan = Path.Combine(root, "clamscan.exe");
            if (File.Exists(clamscan))
            {
                var freshclam = Path.Combine(root, "freshclam.exe");
                var clamd = Path.Combine(root, "clamd.exe");
                var clamdscan = Path.Combine(root, "clamdscan.exe");
                var dbDir = Path.Combine(root, "database");

                return Task.FromResult<ClamAvInstallation?>(new ClamAvInstallation
                {
                    RootDirectory = root,
                    ClamScanPath = clamscan,
                    FreshClamPath = File.Exists(freshclam) ? freshclam : null,
                    ClamdPath = File.Exists(clamd) ? clamd : null,
                    ClamdScanPath = File.Exists(clamdscan) ? clamdscan : null,
                    ConfigDirectory = root,
                    DatabaseDirectory = Directory.Exists(dbDir) ? dbDir : root,
                    Version = "Windows Native",
                    Source = string.Equals(root, customPath, StringComparison.OrdinalIgnoreCase) ? ClamAvInstallationSource.Custom : ClamAvInstallationSource.System
                });
            }
        }

        return Task.FromResult<ClamAvInstallation?>(null);
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupService : IStartupService
{
    private const string AppName = "ClamAV-GUI";
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public Task<bool> IsStartOnLoginEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return Task.FromResult(key?.GetValue(AppName) != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task SetStartOnLoginAsync(bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return Task.CompletedTask;

            if (enable)
            {
                var execPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(execPath))
                {
                    key.SetValue(AppName, $"\"{execPath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }
}

public sealed class WindowsSchedulerService : ISchedulerService
{
    private const string TaskName = "ClamAV-GUI Daily Scan";

    public async Task<IReadOnlyList<ScheduledScan>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var configured = await IsScheduledScanConfiguredAsync(cancellationToken);
        if (!configured) return Array.Empty<ScheduledScan>();

        return new List<ScheduledScan>
        {
            new()
            {
                Id = TaskName,
                TargetPath = "Configured Path",
                TimeOfDay = new TimeSpan(2, 0, 0),
                IsEnabled = true
            }
        };
    }

    public async Task<bool> IsScheduledScanConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var res = await RunSchtasksAsync($"/Query /TN \"{TaskName}\"", cancellationToken);
        return res.ExitCode == 0;
    }

    public async Task<string> CreateOrUpdateDailyScanTaskAsync(string targetPath, TimeSpan timeOfDay, CancellationToken cancellationToken = default)
    {
        var exec = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exec)) return "Could not determine process path.";

        var formattedTime = $"{timeOfDay.Hours:00}:{timeOfDay.Minutes:00}";
        var tr = $"\"{exec}\" --scan \"{targetPath}\"";
        var args = $"/Create /F /SC DAILY /TN \"{TaskName}\" /TR \"{tr}\" /ST {formattedTime}";

        var res = await RunSchtasksAsync(args, cancellationToken);
        return res.ExitCode == 0 ? $"Scheduled daily scan at {formattedTime}" : $"Failed to schedule scan: {res.Output}";
    }

    public async Task<string> RemoveDailyScanTaskAsync(CancellationToken cancellationToken = default)
    {
        var res = await RunSchtasksAsync($"/Delete /F /TN \"{TaskName}\"", cancellationToken);
        return res.ExitCode == 0 ? "Scheduled scan removed." : $"Failed to remove schedule: {res.Output}";
    }

    private static async Task<(int ExitCode, string Output)> RunSchtasksAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
            return (process.ExitCode, combined.Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
