using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Platform.Linux;

public static class LinuxFreshClamService
{
    public static async Task<UpdateResult?> TryUpdateAsync(
        IProcessRunner processRunner, ClamAvInstallation installation, CancellationToken cancellationToken)
    {
        // Debian's freshclam configuration writes to /var/lib/clamav and
        // /var/log/clamav as the clamav user. Run its service through the
        // desktop authorization prompt instead of launching it as the GUI user.
        if (string.Equals(installation.ConfigDirectory, "/etc/clamav", StringComparison.Ordinal) &&
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

            var serviceResult = await processRunner.RunAsync(new ProcessRequest
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

        return null;
    }
}
