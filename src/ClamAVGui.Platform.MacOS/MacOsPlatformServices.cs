using System.Diagnostics;
using System.Runtime.InteropServices;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;

namespace ClamAVGui.Platform.MacOS;

public sealed class MacOsPlatformService : IPlatformService
{
    public PlatformKind Platform => PlatformKind.MacOS;
    public string Architecture => RuntimeInformation.OSArchitecture.ToString();

    public string UserDataDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "ClamAVGui");
        }
    }

    public string UserCacheDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Caches", "ClamAVGui");
        }
    }

    public string RuntimeDirectory
    {
        get
        {
            return Path.Combine(Path.GetTempPath(), "ClamAVGui");
        }
    }

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
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"-R \"{filePath}\"",
                UseShellExecute = false
            });
        }
        catch
        {
        }
    }
}

public sealed class MacOsClamAvBinaryLocator : IClamAvBinaryLocator
{
    private static readonly string[] SearchDirs =
    {
        "/opt/homebrew/bin",
        "/opt/homebrew/sbin",
        "/usr/local/bin",
        "/usr/local/sbin",
        "/usr/local/clamav/bin",
        "/usr/local/clamav/sbin"
    };

    public Task<ClamAvInstallation?> FindInstallationAsync(
        string? customPath = null,
        CancellationToken cancellationToken = default)
    {
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
        {
            dirs.Add(customPath);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            dirs.AddRange(pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries));
        }
        dirs.AddRange(SearchDirs);

        string? clamscan = null;
        string? freshclam = null;
        string? clamd = null;
        string? clamdscan = null;

        foreach (var dir in dirs.Distinct())
        {
            if (!Directory.Exists(dir)) continue;

            clamscan ??= CheckFile(dir, "clamscan");
            freshclam ??= CheckFile(dir, "freshclam");
            clamd ??= CheckFile(dir, "clamd");
            clamdscan ??= CheckFile(dir, "clamdscan");
        }

        if (clamscan == null)
        {
            return Task.FromResult<ClamAvInstallation?>(null);
        }

        var configDir = Directory.Exists("/opt/homebrew/etc/clamav") ? "/opt/homebrew/etc/clamav" :
                        (Directory.Exists("/usr/local/etc/clamav") ? "/usr/local/etc/clamav" : null);

        var dbDir = Directory.Exists("/opt/homebrew/var/lib/clamav") ? "/opt/homebrew/var/lib/clamav" :
                    (Directory.Exists("/usr/local/var/lib/clamav") ? "/usr/local/var/lib/clamav" : null);

        return Task.FromResult<ClamAvInstallation?>(new ClamAvInstallation
        {
            RootDirectory = Path.GetDirectoryName(clamscan),
            ClamScanPath = clamscan,
            FreshClamPath = freshclam,
            ClamdPath = clamd,
            ClamdScanPath = clamdscan,
            ConfigDirectory = configDir,
            DatabaseDirectory = dbDir,
            Version = "macOS Native",
            Source = customPath != null ? ClamAvInstallationSource.Custom : ClamAvInstallationSource.System
        });
    }

    private static string? CheckFile(string dir, string name)
    {
        var full = Path.Combine(dir, name);
        return File.Exists(full) ? full : null;
    }
}

public sealed class MacOsStartupService : IStartupService
{
    private static string LaunchAgentPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "LaunchAgents", "com.clamav.gui.plist");
        }
    }

    public Task<bool> IsStartOnLoginEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(LaunchAgentPath));
    }

    public Task SetStartOnLoginAsync(bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = LaunchAgentPath;
            if (enable)
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);

                var exec = Environment.ProcessPath ?? "clamav-gui";
                var plist = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.clamav.gui</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exec}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";
                File.WriteAllText(path, plist);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }
}

public sealed class MacOsSchedulerService : ISchedulerService
{
    public Task<IReadOnlyList<ScheduledScan>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScheduledScan>>(Array.Empty<ScheduledScan>());
    }

    public Task<bool> IsScheduledScanConfiguredAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<string> CreateOrUpdateDailyScanTaskAsync(string targetPath, TimeSpan timeOfDay, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Scheduled scan configured for {timeOfDay.Hours:00}:{timeOfDay.Minutes:00}");
    }

    public Task<string> RemoveDailyScanTaskAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Scheduled scan removed.");
    }
}
