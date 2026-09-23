using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using ClamAVGui.Platform.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.App.Services;

public sealed record AppReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;
}

public interface IAppUpdateService
{
    string CurrentVersion { get; }
    Task<AppReleaseInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<bool> ApplyUpdateAndRestartAsync(AppReleaseInfo release, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class AppUpdateService : IAppUpdateService
{
    private const string Repo = "sPROFFEs/ClamAV-GUI";
    private readonly IPlatformService _platformService;
    private readonly ILogger<AppUpdateService> _logger;

    public string CurrentVersion
    {
        get
        {
            var infoVer = typeof(AppUpdateService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                var plusIdx = infoVer.IndexOf('+');
                var ver = plusIdx > 0 ? infoVer[..plusIdx] : infoVer;
                return ver.StartsWith('v') ? ver : $"v{ver}";
            }

            var verObj = Assembly.GetExecutingAssembly().GetName().Version;
            return verObj != null ? $"v{verObj.Major}.{verObj.Minor}.{verObj.Build}" : "v2.0.0-beta.1";
        }
    }

    public AppUpdateService(
        IPlatformService platformService,
        ILogger<AppUpdateService>? logger = null)
    {
        _platformService = platformService;
        _logger = logger ?? NullLogger<AppUpdateService>.Instance;
    }

    public async Task<AppReleaseInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ClamAV-GUI-Updater");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.Timeout = TimeSpan.FromSeconds(10);

            var releases = await client.GetFromJsonAsync<List<AppReleaseInfo>>(
                $"https://api.github.com/repos/{Repo}/releases",
                cancellationToken);

            if (releases != null && releases.Count > 0)
            {
                var latest = releases[0];
                var cur = CurrentVersion.Trim().TrimStart('v').ToLowerInvariant();
                var tag = latest.TagName.Trim().TrimStart('v').ToLowerInvariant();

                if (!string.Equals(cur, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return latest;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for GitHub application updates");
        }

        return null;
    }

    public async Task<bool> ApplyUpdateAndRestartAsync(
        AppReleaseInfo release,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Downloading and applying update script...");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var scriptUrl = $"https://raw.githubusercontent.com/{Repo}/migration/avalonia/install.ps1";
                var psCmd = $"irm {scriptUrl} | iex; Start-Process (Join-Path $env:LOCALAPPDATA 'ClamAV-GUI\\ClamAVGui.App.exe')";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                    UseShellExecute = true
                });

                Environment.Exit(0);
                return true;
            }
            else
            {
                var scriptUrl = $"https://raw.githubusercontent.com/{Repo}/migration/avalonia/install.sh";
                var tempScript = Path.Combine(Path.GetTempPath(), "clamav-update.sh");

                using (var client = new HttpClient())
                {
                    var scriptContent = await client.GetStringAsync(scriptUrl, cancellationToken);
                    await File.WriteAllTextAsync(tempScript, scriptContent, cancellationToken);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"\"{tempScript}\"",
                    UseShellExecute = false
                });

                Environment.Exit(0);
                return true;
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Update failed: {ex.Message}");
            return false;
        }
    }
}
