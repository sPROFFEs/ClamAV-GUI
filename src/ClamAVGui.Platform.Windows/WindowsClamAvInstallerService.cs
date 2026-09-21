using System.IO.Compression;
using System.Net.Http;
using System.Text;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;

namespace ClamAVGui.Platform.Windows;

public sealed class WindowsClamAvInstallerService : IClamAvInstallerService
{
    private readonly IPlatformService _platformService;
    private readonly ISettingsService _settingsService;

    public bool SupportsAutomaticInstallation => true;
    public string RecommendedCommandOrMethod => "Download and set up standalone official ClamAV Windows binaries (automatic).";

    public WindowsClamAvInstallerService(
        IPlatformService platformService,
        ISettingsService settingsService)
    {
        _platformService = platformService;
        _settingsService = settingsService;
    }

    public async Task<bool> InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var targetEngineDir = Path.Combine(_platformService.UserDataDirectory, "clamav-engine");
        Directory.CreateDirectory(targetEngineDir);

        progress?.Report("Downloading official ClamAV Windows binaries...");

        // Official standalone 64-bit ClamAV release
        var downloadUrl = "https://www.clamav.net/downloads/production/clamav-1.4.2.win.x64.zip";
        var tempZip = Path.Combine(Path.GetTempPath(), $"clamav-win-engine-{Guid.NewGuid():N}.zip");

        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            progress?.Report("Extracting ClamAV binaries...");
            var extractTemp = Path.Combine(Path.GetTempPath(), $"clamav-extracted-{Guid.NewGuid():N}");
            Directory.CreateDirectory(extractTemp);

            ZipFile.ExtractToDirectory(tempZip, extractTemp, overwriteFiles: true);

            // Locate bin directory inside archive (sometimes root, sometimes folder named clamav-*)
            var clamScanFound = Directory.GetFiles(extractTemp, "clamscan.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (clamScanFound != null)
            {
                var sourceBinDir = Path.GetDirectoryName(clamScanFound)!;
                foreach (var file in Directory.GetFiles(sourceBinDir, "*.*"))
                {
                    var destFile = Path.Combine(targetEngineDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }
            }

            progress?.Report("Initializing database and configuration files...");
            var dbDir = Path.Combine(targetEngineDir, "database");
            Directory.CreateDirectory(dbDir);

            var freshclamConf = Path.Combine(targetEngineDir, "freshclam.conf");
            if (!File.Exists(freshclamConf))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"DatabaseDirectory \"{dbDir}\"");
                sb.AppendLine($"UpdateLogFile \"{Path.Combine(targetEngineDir, "freshclam.log")}\"");
                sb.AppendLine("DatabaseMirror database.clamav.net");
                await File.WriteAllTextAsync(freshclamConf, sb.ToString(), cancellationToken);
            }

            var clamdConf = Path.Combine(targetEngineDir, "clamd.conf");
            if (!File.Exists(clamdConf))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"DatabaseDirectory \"{dbDir}\"");
                sb.AppendLine($"LogFile \"{Path.Combine(targetEngineDir, "clamd.log")}\"");
                sb.AppendLine("TCPSocket 3310");
                sb.AppendLine("TCPAddr 127.0.0.1");
                sb.AppendLine("LogTime yes");
                await File.WriteAllTextAsync(clamdConf, sb.ToString(), cancellationToken);
            }

            var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
            settings.CustomClamAvPath = targetEngineDir;
            await _settingsService.SaveSettingsAsync(settings, cancellationToken);

            progress?.Report("ClamAV engine installed and configured successfully!");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"Installation failed: {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
            catch
            {
            }
        }
    }
}
