using System.Diagnostics;
using ClamAVGui.Core.Interfaces;

namespace ClamAVGui.Platform.Linux;

public sealed class LinuxClamAvInstallerService : IClamAvInstallerService
{
    public bool SupportsAutomaticInstallation => true;

    public string RecommendedCommandOrMethod
    {
        get
        {
            if (File.Exists("/usr/bin/apt-get") || File.Exists("/usr/bin/apt"))
                return "sudo apt-get update && sudo apt-get install -y clamav clamav-daemon clamav-freshclam";
            if (File.Exists("/usr/bin/dnf"))
                return "sudo dnf install -y clamav clamd clamav-update";
            if (File.Exists("/usr/bin/pacman"))
                return "sudo pacman -S --noconfirm clamav";
            if (File.Exists("/usr/bin/zypper"))
                return "sudo zypper install -y clamav";
            if (File.Exists("/sbin/apk"))
                return "sudo apk add clamav";
            return "sudo apt install clamav clamav-daemon";
        }
    }

    public async Task<bool> InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var cmd = RecommendedCommandOrMethod;
        progress?.Report($"Recommended command: {cmd}");

        // Attempt installation via pkexec or standard terminal if available
        if (File.Exists("/usr/bin/pkexec"))
        {
            progress?.Report("Requesting administrator authorization via pkexec...");

            string binary;
            string[] args;

            if (cmd.Contains("apt-get"))
            {
                binary = "/usr/bin/apt-get";
                args = new[] { "install", "-y", "clamav", "clamav-daemon", "clamav-freshclam" };
            }
            else if (cmd.Contains("dnf"))
            {
                binary = "/usr/bin/dnf";
                args = new[] { "install", "-y", "clamav", "clamd", "clamav-update" };
            }
            else if (cmd.Contains("pacman"))
            {
                binary = "/usr/bin/pacman";
                args = new[] { "-S", "--noconfirm", "clamav" };
            }
            else
            {
                progress?.Report($"Please run this command in your terminal:\n  {cmd}");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add(binary);
                foreach (var arg in args) startInfo.ArgumentList.Add(arg);

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                process.OutputDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode == 0)
                {
                    progress?.Report("ClamAV successfully installed via package manager!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Automated installation notice: {ex.Message}");
            }
        }

        progress?.Report($"Please execute the following command in terminal to install ClamAV:\n\n  {cmd}\n");
        return false;
    }
}
