using CliWrap;
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public static class ClamAVService
    {
        public static bool IsClamAVInstalled(string clamavPath)
        {
            if (string.IsNullOrEmpty(clamavPath) || !Directory.Exists(clamavPath))
            {
                return false;
            }

            var clamScanPath = Path.Combine(clamavPath, "clamscan.exe");
            var freshClamPath = Path.Combine(clamavPath, "freshclam.exe");

            return File.Exists(clamScanPath) && File.Exists(freshClamPath);
        }

        private static async Task CreateDefaultConfigFilesAsync(string clamavPath)
        {
            var dbPath = Path.Combine(clamavPath, "database");
            Directory.CreateDirectory(dbPath);

            // Create freshclam.conf if it doesn't exist
            var freshclamConfPath = Path.Combine(clamavPath, "freshclam.conf");
            if (!File.Exists(freshclamConfPath))
            {
                await File.WriteAllLinesAsync(freshclamConfPath, new[]
                {
                    $"DatabaseDirectory \"{dbPath}\""
                });
            }

            // Create a default clamd.conf if it doesn't exist
            var clamdConfPath = Path.Combine(clamavPath, "clamd.conf");
            if (!File.Exists(clamdConfPath))
            {
                await UpdateClamdConfigAsync(clamavPath, new Models.ScanOptions());
            }
        }

        public static async Task<(string Output, string Error)> RunFreshclamAsync(string clamavPath)
        {
            await CreateDefaultConfigFilesAsync(clamavPath);

            var freshClamExePath = Path.Combine(clamavPath, "freshclam.exe");
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            await Cli.Wrap(freshClamExePath)
                .WithArguments(new[] { "--config-file", Path.Combine(clamavPath, "freshclam.conf") })
                .WithWorkingDirectory(clamavPath)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .ExecuteAsync();

            return (stdOutBuffer.ToString(), stdErrBuffer.ToString());
        }

        public static async Task RunClamscanAsync(string clamavPath, string scanPath, Models.ScanOptions options, Action<string> onLineReceived)
        {
            if (!Directory.Exists(scanPath) && !File.Exists(scanPath))
            {
                onLineReceived?.Invoke("Error: Scan path does not exist.");
                return;
            }

            var clamScanExePath = Path.Combine(clamavPath, "clamscan.exe");
            var dbPath = Path.Combine(clamavPath, "database");

            var arguments = new System.Collections.Generic.List<string>
            {
                "--stdout",
                "-r", // recursive
                "--database",
                dbPath
            };

            // Add arguments from options
            if (options.HeuristicAlerts) arguments.Add("--heuristic-alerts=yes");
            if (options.ScanEncrypted) arguments.Add("--alert-encrypted=yes");
            if (options.LeaveTemps) arguments.Add("--leave-temps=yes");
            if (options.MoveToQuarantine && !string.IsNullOrWhiteSpace(options.QuarantinePath))
            {
                Directory.CreateDirectory(options.QuarantinePath);
                arguments.Add("--move");
                arguments.Add(options.QuarantinePath);
            }

            arguments.Add(scanPath);

            var cmd = Cli.Wrap(clamScanExePath)
                .WithArguments(arguments)
                .WithWorkingDirectory(clamavPath)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(onLineReceived))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(onLineReceived));

            await cmd.ExecuteAsync();
        }

        public static async Task<string> InitializeConfigurationAsync(string clamavPath)
        {
            try
            {
                var confExamplesPath = Path.Combine(clamavPath, "conf_examples");
                if (!Directory.Exists(confExamplesPath))
                {
                    return "Error: 'conf_examples' directory not found. The selected folder is not a valid ClamAV installation.";
                }

                var freshclamSample = Path.Combine(confExamplesPath, "freshclam.conf.sample");
                var clamdSample = Path.Combine(confExamplesPath, "clamd.conf.sample");
                var freshclamConf = Path.Combine(clamavPath, "freshclam.conf");

                if (!File.Exists(freshclamSample) || !File.Exists(clamdSample))
                {
                    return "Error: Sample .conf files not found in 'conf_examples'.";
                }

                // 1. Copy freshclam.conf from sample
                var lines = await File.ReadAllLinesAsync(freshclamSample);
                await File.WriteAllLinesAsync(freshclamConf, lines.Where(l => !l.Trim().Equals("Example", StringComparison.OrdinalIgnoreCase)));

                // 2. Generate a new, clean clamd.conf using our centralized method
                await UpdateClamdConfigAsync(clamavPath, new Models.ScanOptions());

                // 3. Create database directory
                var dbPath = Path.Combine(clamavPath, "database");
                Directory.CreateDirectory(dbPath);

                return "Configuration files initialized successfully. You can now run an update.";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred during initialization: {ex.Message}";
            }
        }

        public static bool IsClamdRunning()
        {
            return Process.GetProcessesByName("clamd").Length > 0;
        }

        public static async Task StartClamdAsync(string clamavPath, Models.ScanOptions options)
        {
            if (IsClamdRunning()) return;

            // Ensure the config file is up-to-date before starting the daemon.
            await UpdateClamdConfigAsync(clamavPath, options);

            var clamdExePath = Path.Combine(clamavPath, "clamd.exe");
            var clamdConfPath = Path.Combine(clamavPath, "clamd.conf");

            if (!File.Exists(clamdExePath))
            {
                throw new FileNotFoundException("clamd.exe not found.", clamdExePath);
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = clamdExePath,
                    Arguments = $"--config-file=\"{clamdConfPath}\" --foreground",
                    WorkingDirectory = clamavPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null) outputBuilder.AppendLine(args.Data);
            };

            var errorOutputBuilder = new StringBuilder();
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    errorOutputBuilder.AppendLine(args.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start clamd.exe process. {ex.Message}", ex);
            }

            const int port = 3310;
            var timeout = TimeSpan.FromSeconds(45); // Increased timeout
            var stopwatch = Stopwatch.StartNew();
            var connected = false;

            while (stopwatch.Elapsed < timeout && !process.HasExited)
            {
                try
                {
                    using var client = new System.Net.Sockets.TcpClient();
                    await client.ConnectAsync("127.0.0.1", port);
                    if (client.Connected)
                    {
                        connected = true;
                        break;
                    }
                }
                catch
                {
                    // Connection refused, wait and retry
                }
                await Task.Delay(250); // Reduced delay for quicker checks
            }

            if (connected)
            {
                process.CancelOutputRead();
                process.CancelErrorRead();
                return;
            }

            // If we're here, the connection failed or the process exited.
            // Wait a brief moment for any final error output to be captured.
            await Task.Delay(100);
            process.CancelOutputRead();
            process.CancelErrorRead();

            var standardOutput = outputBuilder.ToString().Trim();
            var errorOutput = errorOutputBuilder.ToString().Trim();
            var combinedOutput = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                combinedOutput.AppendLine("--- Standard Output ---");
                combinedOutput.AppendLine(standardOutput);
            }
            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                combinedOutput.AppendLine("--- Error Output ---");
                combinedOutput.AppendLine(errorOutput);
            }
            if (combinedOutput.Length == 0)
            {
                combinedOutput.AppendLine("No output was captured from clamd.exe. It may have failed silently.");
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000); // Wait up to 1 second for graceful exit after kill
                }
            }
            catch (Exception ex)
            {
                // Process might already be gone, or we might not have access.
                // The primary goal is to report the error we captured.
                Debug.WriteLine($"Error while trying to kill clamd.exe: {ex.Message}");
            }

            var finalMessage = combinedOutput.ToString();

            if (stopwatch.Elapsed >= timeout)
            {
                throw new TimeoutException($"Timed out waiting for the ClamAV daemon to become responsive. The process was terminated.\n\n{finalMessage}");
            }
            else // The process exited on its own.
            {
                throw new InvalidOperationException($"The ClamAV daemon (clamd.exe) failed to start. It exited unexpectedly with code {process.ExitCode}.\n\nThis may be due to a configuration error in clamd.conf or a missing dependency.\n\n{finalMessage}");
            }
        }

        public static Task StopClamdAsync()
        {
            foreach (var process in Process.GetProcessesByName("clamd"))
            {
                process.Kill();
            }
            return Task.CompletedTask;
        }

        public static async Task<string?> GetLogFilePathAsync(string clamavPath)
        {
            var confPath = Path.Combine(clamavPath, "clamd.conf");
            if (!File.Exists(confPath)) return null;

            var lines = await File.ReadAllLinesAsync(confPath);
            var logFileDirective = lines.FirstOrDefault(l => l.Trim().StartsWith("LogFile", StringComparison.OrdinalIgnoreCase));

            if (logFileDirective == null) return null;

            // Extract the path, which might be quoted
            var path = logFileDirective.Trim()
                                       .Substring("LogFile".Length)
                                       .Trim()
                                       .Trim('"');
            return path;
        }

        private static async Task UpdateClamdConfigAsync(string clamavPath, Models.ScanOptions options)
        {
            var clamdConfPath = Path.Combine(clamavPath, "clamd.conf");
            var dbPath = Path.Combine(clamavPath, "database");
            Directory.CreateDirectory(dbPath);
            var logPath = Path.Combine(clamavPath, "clamd.log");

            var configLines = new System.Collections.Generic.List<string>
            {
                // --- Essential Settings ---
                $"DatabaseDirectory \"{dbPath}\"",
                $"LogFile \"{logPath}\"",
                "LogTime yes",
                "LogVerbose yes", // Added for detailed logging, crucial for monitoring
                "TCPSocket 3310",

                // --- Scan Options ---
                // All scan options are being removed to ensure compatibility with minimal
                // clamd builds. The raw SCAN command will be used instead, which has
                // sensible defaults.
            };

            await File.WriteAllLinesAsync(clamdConfPath, configLines);
        }

        public static async Task<string> PingDaemonAsync()
        {
            if (!IsClamdRunning()) return "Daemon not running.";
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await writer.WriteAsync("nPING\n");
                return await reader.ReadLineAsync() ?? "No response.";
            }
            catch (Exception ex)
            {
                return $"Error pinging daemon: {ex.Message}";
            }
        }

        public static async Task<string> ReloadDatabaseAsync()
        {
            if (!IsClamdRunning()) return "Daemon not running.";
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await writer.WriteAsync("nRELOAD\n");
                return await reader.ReadLineAsync() ?? "No response.";
            }
            catch (Exception ex)
            {
                return $"Error reloading database: {ex.Message}";
            }
        }

        public static async Task<string> GetVersionAsync()
        {
            if (!IsClamdRunning()) return "Daemon not running.";
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await writer.WriteAsync("nVERSION\n");
                return await reader.ReadLineAsync() ?? "No response.";
            }
            catch (Exception ex)
            {
                return $"Error getting version: {ex.Message}";
            }
        }

        public static async Task ShutdownDaemonAsync()
        {
            if (!IsClamdRunning()) return;
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                await writer.WriteAsync("nSHUTDOWN\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending SHUTDOWN command: {ex.Message}");
            }
        }

        public static async Task<string> GetStatsAsync()
        {
            if (!IsClamdRunning()) return "Daemon not running.";
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await writer.WriteAsync("nSTATS\n");
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                return $"Error getting stats: {ex.Message}";
            }
        }

        public static async Task<string> GetVersionCommandsAsync()
        {
            if (!IsClamdRunning()) return "Daemon not running.";
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await writer.WriteAsync("nVERSIONCOMMANDS\n");
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                return $"Error getting version commands: {ex.Message}";
            }
        }

        public static async Task<string> ScanFileWithDaemonAsync(string filePath)
        {
            if (!IsClamdRunning()) return $"{filePath}: Daemon not running.";
            if (!File.Exists(filePath)) return $"{filePath}: File not found.";

            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 3310);
                await using var stream = client.GetStream();
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);

                var command = $"nCONTSCAN {filePath}\n";
                await writer.WriteAsync(command);

                var result = await reader.ReadLineAsync();
                return result ?? $"{filePath}: No response from daemon.";
            }
            catch (Exception ex)
            {
                return $"{filePath}: Error scanning file: {ex.Message}";
            }
        }
    }
}
