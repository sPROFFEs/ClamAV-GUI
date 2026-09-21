using System.Diagnostics;
using ClamAVGui.Core.Interfaces;

namespace ClamAVGui.Platform.MacOS;

public sealed class MacOsClamAvInstallerService : IClamAvInstallerService
{
    public bool SupportsAutomaticInstallation => true;

    public string RecommendedCommandOrMethod
    {
        get
        {
            if (File.Exists("/opt/homebrew/bin/brew") || File.Exists("/usr/local/bin/brew"))
            {
                return "brew install clamav";
            }
            return "brew install clamav (or download official ClamAV package from clamav.net)";
        }
    }

    public async Task<bool> InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var brewPath = File.Exists("/opt/homebrew/bin/brew") ? "/opt/homebrew/bin/brew" :
                       (File.Exists("/usr/local/bin/brew") ? "/usr/local/bin/brew" : null);

        if (brewPath != null)
        {
            progress?.Report("Found Homebrew. Running 'brew install clamav'...");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = brewPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("install");
                startInfo.ArgumentList.Add("clamav");

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                process.OutputDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode == 0)
                {
                    progress?.Report("ClamAV successfully installed via Homebrew!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Brew install error: {ex.Message}");
            }
        }

        progress?.Report($"Run the following command in Terminal or install Homebrew:\n  brew install clamav\n");
        return false;
    }
}
