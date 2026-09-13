package services

import (
	"os"
	"path/filepath"
	"strings"
	"sync"
)

type SettingsService struct {
	mu                       sync.Mutex
	clamavPathFile           string
	monitoredPathsFile       string
	monitoringFiltersFile    string
	monitoringExclusionsFile string
}

func NewSettingsService() *SettingsService {
	appDataPath := getAppDataPath()
	os.MkdirAll(appDataPath, 0755)
	return &SettingsService{
		clamavPathFile:           filepath.Join(appDataPath, "clamav_path.txt"),
		monitoredPathsFile:       filepath.Join(appDataPath, "monitored_paths.txt"),
		monitoringFiltersFile:    filepath.Join(appDataPath, "monitoring_filters.txt"),
		monitoringExclusionsFile: filepath.Join(appDataPath, "monitoring_exclusions.txt"),
	}
}

func (s *SettingsService) LoadPath() string {
	s.mu.Lock()
	defer s.mu.Unlock()

	data, err := os.ReadFile(s.clamavPathFile)
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(data))
}

func (s *SettingsService) SavePath(path string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	return os.WriteFile(s.clamavPathFile, []byte(path), 0644)
}

func (s *SettingsService) LoadMonitoredPaths() []string {
	return s.loadLines(s.monitoredPathsFile)
}

func (s *SettingsService) SaveMonitoredPaths(paths []string) error {
	return s.saveLines(s.monitoredPathsFile, paths)
}

func (s *SettingsService) LoadMonitoringFilters() []string {
	return s.loadLines(s.monitoringFiltersFile)
}

func (s *SettingsService) SaveMonitoringFilters(filters []string) error {
	return s.saveLines(s.monitoringFiltersFile, filters)
}

func (s *SettingsService) LoadMonitoringExclusions() []string {
	return s.loadLines(s.monitoringExclusionsFile)
}

func (s *SettingsService) SaveMonitoringExclusions(paths []string) error {
	return s.saveLines(s.monitoringExclusionsFile, paths)
}

func (s *SettingsService) loadLines(filePath string) []string {
	data, err := os.ReadFile(filePath)
	if err != nil {
		return []string{}
	}
	lines := strings.Split(strings.TrimSpace(string(data)), "\n")
	var result []string
	for _, line := range lines {
		if strings.TrimSpace(line) != "" {
			result = append(result, line)
		}
	}
	return result
}

func (s *SettingsService) saveLines(filePath string, lines []string) error {
	content := strings.Join(lines, "\n")
	return os.WriteFile(filePath, []byte(content), 0644)
}
