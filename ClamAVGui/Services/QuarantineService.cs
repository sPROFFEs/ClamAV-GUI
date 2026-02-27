using ClamAVGui.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public class QuarantineService
    {
        private readonly string _quarantineDbPath;

        public QuarantineService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClamAV-GUI");
            Directory.CreateDirectory(appDataPath);
            _quarantineDbPath = Path.Combine(appDataPath, "quarantine.json");
        }

        public async Task<List<QuarantineItem>> LoadItemsAsync()
        {
            if (!File.Exists(_quarantineDbPath))
            {
                return new List<QuarantineItem>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_quarantineDbPath);
                return JsonSerializer.Deserialize<List<QuarantineItem>>(json) ?? new List<QuarantineItem>();
            }
            catch
            {
                return new List<QuarantineItem>();
            }
        }

        public async Task AddItemsAsync(IEnumerable<QuarantineItem> items)
        {
            var existing = await LoadItemsAsync();
            existing.InsertRange(0, items);
            await SaveAsync(existing);
        }

        public async Task RemoveItemAsync(Guid id)
        {
            var existing = await LoadItemsAsync();
            var item = existing.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                existing.Remove(item);
                await SaveAsync(existing);
            }
        }

        public async Task ClearMissingFilesAsync()
        {
            var existing = await LoadItemsAsync();
            existing = existing.Where(i => File.Exists(i.QuarantinePath)).ToList();
            await SaveAsync(existing);
        }

        private async Task SaveAsync(List<QuarantineItem> items)
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_quarantineDbPath, json);
        }
    }
}
