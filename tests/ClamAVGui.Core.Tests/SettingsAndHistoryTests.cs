using ClamAVGui.Core.History;
using ClamAVGui.Core.Models;
using ClamAVGui.Core.Services;
using Xunit;

namespace ClamAVGui.Core.Tests;

public class SettingsAndHistoryTests : IDisposable
{
    private readonly string _testDir;

    public SettingsAndHistoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "clamav_settings_hist_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task SettingsService_SavesAndLoadsCorrectly()
    {
        var settingsService = new SettingsService(_testDir);
        var original = new ApplicationSettings
        {
            CustomClamAvPath = "/usr/bin",
            AutoUpdateDefinitions = true,
            HeuristicAlerts = true,
            MonitoredPaths = new List<string> { "/home/user/downloads" }
        };

        await settingsService.SaveSettingsAsync(original);
        var loaded = await settingsService.LoadSettingsAsync();

        Assert.Equal("/usr/bin", loaded.CustomClamAvPath);
        Assert.True(loaded.AutoUpdateDefinitions);
        Assert.True(loaded.HeuristicAlerts);
        Assert.Single(loaded.MonitoredPaths);
    }

    [Fact]
    public async Task HistoryService_LogsEventsAndExportsJsonAndCsv()
    {
        var historyService = new HistoryService(_testDir);
        await historyService.LogEventAsync("Scan", "Scanned 1 file, 0 threats");
        await historyService.LogEventAsync("Update", "Signature database updated");

        var events = await historyService.LoadHistoryAsync();
        Assert.Equal(2, events.Count);

        var jsonPath = Path.Combine(_testDir, "export.json");
        var csvPath = Path.Combine(_testDir, "export.csv");

        await historyService.ExportAsJsonAsync(jsonPath);
        await historyService.ExportAsCsvAsync(csvPath);

        Assert.True(File.Exists(jsonPath));
        Assert.True(File.Exists(csvPath));
        var csvContent = await File.ReadAllTextAsync(csvPath);
        Assert.Contains("Scanned 1 file", csvContent);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
        }
    }
}
