using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ClamAVGui.Platform.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Platform.Services;

public sealed partial class FileSystemMonitor : IFileSystemMonitor
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _filters = new();
    private readonly List<string> _exclusions = new();
    private readonly ILogger<FileSystemMonitor> _logger;
    private bool _isMonitoring;

    public bool IsMonitoring => _isMonitoring;
    public event Action<string>? FileDetected;
    public event Action<string>? LogMessage;

    public FileSystemMonitor(ILogger<FileSystemMonitor>? logger = null)
    {
        _logger = logger ?? NullLogger<FileSystemMonitor>.Instance;
    }

    public Task StartAsync(IEnumerable<string> paths, IEnumerable<string> filters, IEnumerable<string> exclusions, CancellationToken cancellationToken = default)
    {
        StopInternal();

        _filters.Clear();
        _filters.AddRange(filters.Select(NormalizeFilter).Where(f => !string.IsNullOrWhiteSpace(f)));

        _exclusions.Clear();
        _exclusions.AddRange(exclusions.Select(NormalizePathSafe).Where(e => !string.IsNullOrWhiteSpace(e)));

        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
            {
                LogMessage?.Invoke($"Skipped monitoring (not found): {path}");
                continue;
            }

            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                watcher.Created += OnFileChanged;
                watcher.Changed += OnFileChanged;
                watcher.Renamed += OnFileRenamed;
                watcher.Deleted += OnFileDeleted;
                watcher.Error += OnWatcherError;

                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
                LogMessage?.Invoke($"Monitoring started for: {path}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to start watcher on {path}: {ex.Message}");
                _logger.LogError(ex, "Failed to start watcher on {Path}", path);
            }
        }

        _isMonitoring = _watchers.Count > 0;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopInternal();
        return Task.CompletedTask;
    }

    private void StopInternal()
    {
        foreach (var pending in _debounceTokens.Values)
        {
            pending.Cancel();
            pending.Dispose();
        }
        _debounceTokens.Clear();

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        if (_isMonitoring)
        {
            LogMessage?.Invoke("Monitoring stopped.");
        }
        _isMonitoring = false;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (File.Exists(e.FullPath))
        {
            ScheduleDebounce(e.FullPath);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        LogMessage?.Invoke($"RENAMED: {e.OldFullPath} -> {e.FullPath}");
        if (File.Exists(e.FullPath))
        {
            ScheduleDebounce(e.FullPath);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        LogMessage?.Invoke($"DELETED: {e.FullPath}");
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        LogMessage?.Invoke($"WATCHER ERROR: {e.GetException().Message}");
    }

    private void ScheduleDebounce(string fullPath)
    {
        if (!ShouldScan(fullPath)) return;

        var key = NormalizePathSafe(fullPath);
        var cts = new CancellationTokenSource();

        if (_debounceTokens.TryGetValue(key, out var prev))
        {
            prev.Cancel();
            prev.Dispose();
        }

        _debounceTokens[key] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, cts.Token);
                FileDetected?.Invoke(fullPath);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _debounceTokens.TryRemove(key, out _);
                cts.Dispose();
            }
        });
    }

    private bool ShouldScan(string fullPath)
    {
        if (!File.Exists(fullPath)) return false;

        var normTarget = NormalizePathSafe(fullPath);
        foreach (var exc in _exclusions)
        {
            if (string.Equals(normTarget, exc, StringComparison.OrdinalIgnoreCase)) return false;
            if (normTarget.StartsWith(exc.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (_filters.Count == 0) return true;

        var fileName = Path.GetFileName(fullPath);
        return _filters.Any(f => IsWildcardMatch(fileName, f));
    }

    private static bool IsWildcardMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
    }

    private static string NormalizeFilter(string filter)
    {
        var trimmed = filter?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;
        return trimmed.StartsWith('.') ? $"*{trimmed}" : trimmed;
    }

    private static string NormalizePathSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
