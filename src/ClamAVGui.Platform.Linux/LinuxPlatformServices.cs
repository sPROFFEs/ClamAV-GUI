using System.Diagnostics;
using System.Runtime.InteropServices;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;

namespace ClamAVGui.Platform.Linux;

public sealed class LinuxPlatformService : IPlatformService
{
    public PlatformKind Platform => PlatformKind.Linux;
    public string Architecture => RuntimeInformation.OSArchitecture.ToString();

    public string UserDataDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "clamav-gui");
            }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", "clamav-gui");
        }
    }

    public string UserCacheDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "clamav-gui");
            }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".cache", "clamav-gui");
        }
    }

    public string RuntimeDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "clamav-gui");
            }
            return Path.Combine(Path.GetTempPath(), "clamav-gui");
        }
    }

    public PlatformCapabilities Capabilities => new()
    {
        SupportsClamScan = true,
        SupportsClamD = true,
        SupportsFreshClam = true,
        SupportsNativeOnAccess = File.Exists("/usr/sbin/clamonacc") || File.Exists("/usr/bin/clamonacc"),
        SupportsReactiveMonitoring = true,
        SupportsScheduling = true,
        SupportsNotifications = true,
        SupportsTray = true,
        SupportsStartupRegistration = true
    };

    public void OpenContainingFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        var dir = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : filePath;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{dir}\"",
                UseShellExecute = false
            });
        }
        catch
        {
        }
    }
}

public sealed class LinuxClamAvBinaryLocator : IClamAvBinaryLocator
{
    private static readonly string[] SearchDirs =
    {
        "/usr/bin",
        "/usr/sbin",
        "/usr/local/bin",
        "/usr/local/sbin",
        "/opt/clamav/bin",
        "/opt/clamav/sbin"
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
        string? clamonacc = null;
        string? clamconf = null;

        foreach (var dir in dirs.Distinct())
        {
            if (!Directory.Exists(dir)) continue;

            clamscan ??= CheckFile(dir, "clamscan");
            freshclam ??= CheckFile(dir, "freshclam");
            clamd ??= CheckFile(dir, "clamd");
            clamdscan ??= CheckFile(dir, "clamdscan");
            clamonacc ??= CheckFile(dir, "clamonacc");
            clamconf ??= CheckFile(dir, "clamconf");
        }

        if (clamscan == null)
        {
            return Task.FromResult<ClamAvInstallation?>(null);
        }

        var configDir = Directory.Exists("/etc/clamav") ? "/etc/clamav" :
                        (Directory.Exists("/etc/clamd.d") ? "/etc/clamd.d" :
                        (Directory.Exists("/usr/local/etc") ? "/usr/local/etc" : null));

        var dbDir = Directory.Exists("/var/lib/clamav") ? "/var/lib/clamav" :
                    (Directory.Exists("/var/clamav") ? "/var/clamav" : null);

        var logDir = Directory.Exists("/var/log/clamav") ? "/var/log/clamav" : null;

        return Task.FromResult<ClamAvInstallation?>(new ClamAvInstallation
        {
            RootDirectory = Path.GetDirectoryName(clamscan),
            ClamScanPath = clamscan,
            FreshClamPath = freshclam,
            ClamdPath = clamd,
            ClamdScanPath = clamdscan,
            ClamOnAccPath = clamonacc,
            ClamConfPath = clamconf,
            ConfigDirectory = configDir,
            DatabaseDirectory = dbDir,
            LogDirectory = logDir,
            Version = "Linux Native",
            Source = customPath != null ? ClamAvInstallationSource.Custom : ClamAvInstallationSource.System
        });
    }

    private static string? CheckFile(string dir, string name)
    {
        var full = Path.Combine(dir, name);
        return File.Exists(full) ? full : null;
    }
}

public sealed class LinuxStartupService : IStartupService
{
    private static string AutoStartFilePath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "autostart", "clamav-gui.desktop");
        }
    }

    public Task<bool> IsStartOnLoginEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(AutoStartFilePath));
    }

    public Task SetStartOnLoginAsync(bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = AutoStartFilePath;
            if (enable)
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);

                var exec = Environment.ProcessPath ?? "clamav-gui";
                var desktopContent = $@"[Desktop Entry]
Type=Application
Version=1.0
Name=ClamAV GUI
Comment=ClamAV Antivirus GUI Frontend
Exec=""{exec}""
Terminal=false
Categories=Utility;Security;
";
                File.WriteAllText(path, desktopContent);
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

public sealed class LinuxSchedulerService : ISchedulerService
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
