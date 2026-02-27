using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public class SchedulerService
    {
        private const string TaskName = "ClamAV-GUI Daily Scan";

        public async Task<string> CreateOrUpdateDailyScanTaskAsync(string targetPath, TimeSpan timeOfDay)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return "Could not determine executable path for task scheduler.";
            }

            var formattedTime = $"{timeOfDay.Hours:00}:{timeOfDay.Minutes:00}";
            var taskRun = $"\"{executablePath}\" -scan \"{targetPath}\"";

            var arguments = $"/Create /F /SC DAILY /TN \"{TaskName}\" /TR \"{taskRun}\" /ST {formattedTime}";
            var result = await RunSchtasksAsync(arguments);
            return result.ExitCode == 0
                ? $"Scheduled daily scan at {formattedTime}."
                : $"Failed to schedule scan: {result.Output}";
        }

        public async Task<string> RemoveDailyScanTaskAsync()
        {
            var result = await RunSchtasksAsync($"/Delete /F /TN \"{TaskName}\"");
            return result.ExitCode == 0
                ? "Scheduled scan removed."
                : $"Failed to remove scheduled scan: {result.Output}";
        }

        public async Task<bool> IsScheduledScanConfiguredAsync()
        {
            var result = await RunSchtasksAsync($"/Query /TN \"{TaskName}\"");
            return result.ExitCode == 0;
        }

        private static async Task<(int ExitCode, string Output)> RunSchtasksAsync(string arguments)
        {
            var process = new Process
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
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
            return (process.ExitCode, output.Trim());
        }
    }
}
