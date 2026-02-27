using ClamAVGui.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public class HistoryService
    {
        private readonly string _logFilePath;

        public HistoryService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClamAV-GUI");
            Directory.CreateDirectory(appDataPath);
            _logFilePath = Path.Combine(appDataPath, "history.json");
        }

        public async Task<List<HistoryEvent>> LoadHistoryAsync()
        {
            if (!File.Exists(_logFilePath))
            {
                return new List<HistoryEvent>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_logFilePath);
                return JsonSerializer.Deserialize<List<HistoryEvent>>(json) ?? new List<HistoryEvent>();
            }
            catch
            {
                // Handle deserialization error, maybe return empty list or log it
                return new List<HistoryEvent>();
            }
        }

        public async Task LogEventAsync(string eventType, string details)
        {
            var newEvent = new HistoryEvent
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Details = details
            };

            var events = await LoadHistoryAsync();
            events.Insert(0, newEvent); // Add new events to the top

            await SaveHistoryAsync(events);
        }

        public async Task DeleteHistoryEventAsync(HistoryEvent eventToDelete)
        {
            var events = await LoadHistoryAsync();
            var eventFound = events.FirstOrDefault(e => e.Id == eventToDelete.Id);
            if (eventFound != null)
            {
                events.Remove(eventFound);
                await SaveHistoryAsync(events);
            }
        }

        private async Task SaveHistoryAsync(List<HistoryEvent> events)
        {
            var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_logFilePath, json);
        }

        public Task ClearHistoryAsync()
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
            return Task.CompletedTask;
        }

        public async Task ExportAsJsonAsync(string outputPath)
        {
            var events = await LoadHistoryAsync();
            var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }

        public async Task ExportAsCsvAsync(string outputPath)
        {
            var events = await LoadHistoryAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Id,Timestamp,EventType,Details");

            foreach (var item in events)
            {
                sb.Append('"').Append(item.Id).Append("\",");
                sb.Append('"').Append(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",");
                sb.Append('"').Append(EscapeCsv(item.EventType)).Append("\",");
                sb.Append('"').Append(EscapeCsv(item.Details)).Append('"').AppendLine();
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString());
        }

        private static string EscapeCsv(string value)
        {
            return value.Replace("\"", "\"\"");
        }
    }
}
