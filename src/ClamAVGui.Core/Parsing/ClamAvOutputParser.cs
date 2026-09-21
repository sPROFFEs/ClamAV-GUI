using System.Text.RegularExpressions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Parsing;

public sealed partial class ClamAvOutputParser : IClamAvOutputParser
{
    [GeneratedRegex(@"^(?:(?<win>[A-Za-z]:[\\/][^:]+)|(?<posix>/[^:]+)|(?<gen>[^:]+)):\s*(?<status>.+)$")]
    private static partial Regex ScanLineRegex();

    [GeneratedRegex(@"sigs:\s*(\d+)")]
    private static partial Regex SigsCountRegex();

    [GeneratedRegex(@"ClamAV\s+([0-9\.]+)")]
    private static partial Regex VersionRegex();

    public ScanExecutionResult Parse(
        string stdout,
        string stderr,
        int exitCode,
        ScanBackendKind backend,
        TimeSpan duration = default)
    {
        var detections = new List<ThreatDetection>();
        var errors = new List<string>();
        var statistics = new ScanStatistics();

        var lines = (stdout + Environment.NewLine + stderr).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var inSummarySection = false;

        string knownViruses = "0";
        string engineVersion = string.Empty;
        string scannedDirectories = "0";
        string scannedFiles = "0";
        string infectedFiles = "0";
        string dataScanned = string.Empty;
        string timeTaken = string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.Contains("SCAN SUMMARY", StringComparison.OrdinalIgnoreCase))
            {
                inSummarySection = true;
                continue;
            }

            if (inSummarySection)
            {
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0 && colonIndex < trimmed.Length - 1)
                {
                    var key = trimmed[..colonIndex].Trim();
                    var val = trimmed[(colonIndex + 1)..].Trim();

                    if (key.Equals("Known viruses", StringComparison.OrdinalIgnoreCase)) knownViruses = val;
                    else if (key.Equals("Engine version", StringComparison.OrdinalIgnoreCase)) engineVersion = val;
                    else if (key.Equals("Scanned directories", StringComparison.OrdinalIgnoreCase)) scannedDirectories = val;
                    else if (key.Equals("Scanned files", StringComparison.OrdinalIgnoreCase)) scannedFiles = val;
                    else if (key.Equals("Infected files", StringComparison.OrdinalIgnoreCase)) infectedFiles = val;
                    else if (key.Equals("Data scanned", StringComparison.OrdinalIgnoreCase)) dataScanned = val;
                    else if (key.Equals("Time", StringComparison.OrdinalIgnoreCase)) timeTaken = val;
                }
                continue;
            }

            if (trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(trimmed);
                continue;
            }

            var parseResult = TryParseLine(trimmed);
            if (parseResult.HasValue)
            {
                var (filePath, status) = parseResult.Value;

                if (status.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    var threatName = status[..^"FOUND".Length].Trim();
                    detections.Add(new ThreatDetection
                    {
                        FilePath = filePath,
                        ThreatName = string.IsNullOrWhiteSpace(threatName) ? "UnknownThreat" : threatName,
                        Details = status
                    });
                }
                else if (status.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                         status.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(filePath, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(filePath, "WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{filePath}: {status}");
                }
            }
        }

        statistics = new ScanStatistics
        {
            KnownViruses = knownViruses,
            EngineVersion = engineVersion,
            ScannedDirectories = scannedDirectories,
            ScannedFiles = scannedFiles,
            InfectedFiles = detections.Count > 0 ? detections.Count.ToString() : infectedFiles,
            DataScanned = dataScanned,
            TimeTaken = string.IsNullOrWhiteSpace(timeTaken) && duration > TimeSpan.Zero ? $"{duration.TotalSeconds:F2} sec" : timeTaken
        };

        ScanVerdict verdict;
        if (exitCode == 0)
        {
            verdict = detections.Count > 0 ? ScanVerdict.Infected : ScanVerdict.Clean;
        }
        else if (exitCode == 1)
        {
            verdict = ScanVerdict.Infected;
        }
        else
        {
            verdict = ScanVerdict.Error;
        }

        return new ScanExecutionResult
        {
            Verdict = verdict,
            ExitCode = exitCode,
            Detections = detections,
            Errors = errors,
            Statistics = statistics,
            Backend = backend,
            RawOutput = stdout,
            RawError = stderr,
            Duration = duration
        };
    }

    public (string FilePath, string Status)? TryParseLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        var match = ScanLineRegex().Match(trimmed);
        if (match.Success)
        {
            string path = match.Groups["win"].Success ? match.Groups["win"].Value :
                          match.Groups["posix"].Success ? match.Groups["posix"].Value :
                          match.Groups["gen"].Value;
            var status = match.Groups["status"].Value.Trim();
            return (path.Trim(), status);
        }

        var idx = trimmed.LastIndexOf(':');
        if (idx > 0 && idx < trimmed.Length - 1)
        {
            return (trimmed[..idx].Trim(), trimmed[(idx + 1)..].Trim());
        }

        return null;
    }

    public UpdateResult ParseUpdateOutput(
        string stdout,
        string stderr,
        int exitCode,
        bool wasCancelled)
    {
        if (wasCancelled)
        {
            return new UpdateResult
            {
                Success = false,
                WasCancelled = true,
                Output = stdout,
                Error = stderr,
                ExitCode = exitCode
            };
        }

        var combined = stdout + Environment.NewLine + stderr;
        var isAlreadyUpToDate = combined.Contains("up-to-date", StringComparison.OrdinalIgnoreCase) ||
                                combined.Contains("is up to date", StringComparison.OrdinalIgnoreCase);

        int? sigs = null;
        var sigsMatch = SigsCountRegex().Match(combined);
        if (sigsMatch.Success && int.TryParse(sigsMatch.Groups[1].Value, out var s))
        {
            sigs = s;
        }

        string? ver = null;
        var verMatch = VersionRegex().Match(combined);
        if (verMatch.Success)
        {
            ver = verMatch.Groups[1].Value;
        }

        bool hasError = exitCode != 0 ||
                        combined.Contains("ERROR:", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("Can't download", StringComparison.OrdinalIgnoreCase);

        return new UpdateResult
        {
            Success = !hasError,
            IsAlreadyUpToDate = isAlreadyUpToDate,
            SignaturesCount = sigs,
            Version = ver,
            Output = stdout,
            Error = stderr,
            ExitCode = exitCode,
            WasCancelled = false
        };
    }
}
