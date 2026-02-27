using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public static class HealthCheckService
    {
        public static async Task<string> RunAsync(string? clamavPath)
        {
            var checks = new List<string>();

            if (string.IsNullOrWhiteSpace(clamavPath) || !Directory.Exists(clamavPath))
            {
                checks.Add("FAIL: ClamAV path is not configured or does not exist.");
                return string.Join(Environment.NewLine, checks);
            }

            checks.Add(File.Exists(Path.Combine(clamavPath, "clamscan.exe"))
                ? "OK: clamscan.exe found."
                : "FAIL: clamscan.exe missing.");

            checks.Add(File.Exists(Path.Combine(clamavPath, "freshclam.exe"))
                ? "OK: freshclam.exe found."
                : "FAIL: freshclam.exe missing.");

            checks.Add(File.Exists(Path.Combine(clamavPath, "clamd.exe"))
                ? "OK: clamd.exe found."
                : "WARN: clamd.exe missing (daemon features unavailable).");

            checks.Add(File.Exists(Path.Combine(clamavPath, "clamd.conf"))
                ? "OK: clamd.conf exists."
                : "WARN: clamd.conf not found. Initialize configuration files.");

            checks.Add(File.Exists(Path.Combine(clamavPath, "freshclam.conf"))
                ? "OK: freshclam.conf exists."
                : "WARN: freshclam.conf not found. Initialize configuration files.");

            var dbPath = Path.Combine(clamavPath, "database");
            if (Directory.Exists(dbPath))
            {
                var signatures = Directory.GetFiles(dbPath, "*.cvd").Concat(Directory.GetFiles(dbPath, "*.cld")).ToList();
                checks.Add(signatures.Any()
                    ? $"OK: signature database files found ({signatures.Count})."
                    : "WARN: database folder exists but no .cvd/.cld signatures found.");
            }
            else
            {
                checks.Add("WARN: database folder is missing.");
            }

            var ping = await ClamAVService.PingDaemonAsync();
            checks.Add(ping.Contains("PONG", StringComparison.OrdinalIgnoreCase)
                ? "OK: daemon responds to PING."
                : $"INFO: daemon ping result: {ping}");

            return string.Join(Environment.NewLine, checks);
        }
    }
}
