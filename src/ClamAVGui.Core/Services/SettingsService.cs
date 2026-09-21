using System.Text.Json;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(string storageDirectory)
    {
        Directory.CreateDirectory(storageDirectory);
        _settingsFilePath = Path.Combine(storageDirectory, "settings.json");
    }

    public async Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new ApplicationSettings();
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            return JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
        }
        catch
        {
            return new ApplicationSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var temp = _settingsFilePath + ".tmp." + Guid.NewGuid().ToString("N");
            await File.WriteAllTextAsync(temp, json, cancellationToken);
            File.Move(temp, _settingsFilePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}
