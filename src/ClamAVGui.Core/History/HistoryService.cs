using System.Text;
using System.Text.Json;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.History;

public sealed class HistoryService : IHistoryService
{
    private readonly string _storageDirectory;
    private readonly string _historyFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public HistoryService(string storageDirectory)
    {
        _storageDirectory = storageDirectory;
        Directory.CreateDirectory(_storageDirectory);
        _historyFilePath = Path.Combine(_storageDirectory, "history.json");
    }

    public async Task<IReadOnlyList<HistoryEvent>> LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return Array.Empty<HistoryEvent>();
            }

            var json = await File.ReadAllTextAsync(_historyFilePath, cancellationToken);
            return JsonSerializer.Deserialize<List<HistoryEvent>>(json) ?? new List<HistoryEvent>();
        }
        catch
        {
            return Array.Empty<HistoryEvent>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task LogEventAsync(string eventType, string details, CancellationToken cancellationToken = default)
    {
        var ev = new HistoryEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Details = details
        };

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var events = new List<HistoryEvent>();
            if (File.Exists(_historyFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_historyFilePath, cancellationToken);
                    events = JsonSerializer.Deserialize<List<HistoryEvent>>(json) ?? new List<HistoryEvent>();
                }
                catch
                {
                    events = new List<HistoryEvent>();
                }
            }

            events.Insert(0, ev);
            await SaveInternalAsync(events, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteHistoryEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_historyFilePath)) return;

            var json = await File.ReadAllTextAsync(_historyFilePath, cancellationToken);
            var events = JsonSerializer.Deserialize<List<HistoryEvent>>(json) ?? new List<HistoryEvent>();
            events.RemoveAll(e => e.Id == eventId);

            await SaveInternalAsync(events, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_historyFilePath))
            {
                File.Delete(_historyFilePath);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ExportAsJsonAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var events = await LoadHistoryAsync(cancellationToken);
        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8, cancellationToken);
    }

    public async Task ExportAsCsvAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var events = await LoadHistoryAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,EventType,Details");

        foreach (var item in events)
        {
            sb.Append('"').Append(item.Id).Append("\",");
            sb.Append('"').Append(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",");
            sb.Append('"').Append(EscapeCsv(item.EventType)).Append("\",");
            sb.Append('"').Append(EscapeCsv(item.Details)).Append('"').AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, cancellationToken);
    }

    private async Task SaveInternalAsync(List<HistoryEvent> events, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
        var temp = _historyFilePath + ".tmp." + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, json, Encoding.UTF8, cancellationToken);
        File.Move(temp, _historyFilePath, overwrite: true);
    }

    private static string EscapeCsv(string value) => (value ?? string.Empty).Replace("\"", "\"\"");
}
