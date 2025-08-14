using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClamAVGui.Services
{
    public class SettingsService
    {
        private readonly string _clamavPathSettingsFilePath;
        private readonly string _monitoredPathsFilePath;
        private readonly string _monitoringFiltersFilePath;
        private readonly string _monitoringExclusionsFilePath;

        public SettingsService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClamAV-GUI");
            Directory.CreateDirectory(appDataPath);
            _clamavPathSettingsFilePath = Path.Combine(appDataPath, "clamav_path.txt");
            _monitoredPathsFilePath = Path.Combine(appDataPath, "monitored_paths.txt");
            _monitoringFiltersFilePath = Path.Combine(appDataPath, "monitoring_filters.txt");
            _monitoringExclusionsFilePath = Path.Combine(appDataPath, "monitoring_exclusions.txt");
        }

        public async Task<string?> LoadPathAsync()
        {
            if (!File.Exists(_clamavPathSettingsFilePath))
            {
                return null;
            }
            return await File.ReadAllTextAsync(_clamavPathSettingsFilePath);
        }

        public async Task SavePathAsync(string path)
        {
            await File.WriteAllTextAsync(_clamavPathSettingsFilePath, path);
        }

        public async Task<List<string>> LoadMonitoredPathsAsync()
        {
            if (!File.Exists(_monitoredPathsFilePath))
            {
                return new List<string>();
            }
            var lines = await File.ReadAllLinesAsync(_monitoredPathsFilePath);
            return lines.ToList();
        }

        public async Task SaveMonitoredPathsAsync(IEnumerable<string> paths)
        {
            await File.WriteAllLinesAsync(_monitoredPathsFilePath, paths);
        }

        public async Task<List<string>> LoadMonitoringFiltersAsync()
        {
            if (!File.Exists(_monitoringFiltersFilePath))
            {
                return new List<string>(); // Default to empty list (scan all)
            }
            var lines = await File.ReadAllLinesAsync(_monitoringFiltersFilePath);
            return lines.ToList();
        }

        public async Task SaveMonitoringFiltersAsync(IEnumerable<string> filters)
        {
            await File.WriteAllLinesAsync(_monitoringFiltersFilePath, filters);
        }

        public async Task<List<string>> LoadMonitoringExclusionsAsync()
        {
            if (!File.Exists(_monitoringExclusionsFilePath))
            {
                return new List<string>();
            }
            var lines = await File.ReadAllLinesAsync(_monitoringExclusionsFilePath);
            return lines.ToList();
        }

        public async Task SaveMonitoringExclusionsAsync(IEnumerable<string> paths)
        {
            await File.WriteAllLinesAsync(_monitoringExclusionsFilePath, paths);
        }
    }
}
