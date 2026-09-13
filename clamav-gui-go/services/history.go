package services

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"clamav-gui/models"
)

type HistoryService struct {
	mu          sync.Mutex
	logFilePath string
}

func NewHistoryService() *HistoryService {
	appDataPath := getAppDataPath()
	os.MkdirAll(appDataPath, 0755)
	return &HistoryService{
		logFilePath: filepath.Join(appDataPath, "history.json"),
	}
}

func (s *HistoryService) LoadHistory() ([]models.HistoryEvent, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, err := os.Stat(s.logFilePath); os.IsNotExist(err) {
		return []models.HistoryEvent{}, nil
	}

	data, err := os.ReadFile(s.logFilePath)
	if err != nil {
		return []models.HistoryEvent{}, nil
	}

	var events []models.HistoryEvent
	if err := json.Unmarshal(data, &events); err != nil {
		return []models.HistoryEvent{}, nil
	}
	return events, nil
}

func (s *HistoryService) LogEvent(eventType, details string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	newEvent := models.HistoryEvent{
		Id:        generateID(),
		Timestamp: time.Now(),
		EventType: eventType,
		Details:   details,
	}

	events, _ := s.loadHistoryUnsafe()
	events = append([]models.HistoryEvent{newEvent}, events...)
	return s.saveHistory(events)
}

func (s *HistoryService) DeleteHistoryEvent(id string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	events, _ := s.loadHistoryUnsafe()
	var filtered []models.HistoryEvent
	for _, e := range events {
		if e.Id != id {
			filtered = append(filtered, e)
		}
	}
	return s.saveHistory(filtered)
}

func (s *HistoryService) ClearHistory() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if _, err := os.Stat(s.logFilePath); err == nil {
		return os.Remove(s.logFilePath)
	}
	return nil
}

func (s *HistoryService) ExportAsJSON(outputPath string) error {
	events, err := s.LoadHistory()
	if err != nil {
		return err
	}
	data, err := json.MarshalIndent(events, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(outputPath, data, 0644)
}

func (s *HistoryService) ExportAsCSV(outputPath string) error {
	events, err := s.LoadHistory()
	if err != nil {
		return err
	}

	var sb strings.Builder
	sb.WriteString("Id,Timestamp,EventType,Details\n")
	for _, e := range events {
		sb.WriteString(fmt.Sprintf("\"%s\",\"%s\",\"%s\",\"%s\"\n",
			e.Id,
			e.Timestamp.Format("2006-01-02 15:04:05"),
			escapeCsv(e.EventType),
			escapeCsv(e.Details),
		))
	}
	return os.WriteFile(outputPath, []byte(sb.String()), 0644)
}

func (s *HistoryService) loadHistoryUnsafe() ([]models.HistoryEvent, error) {
	if _, err := os.Stat(s.logFilePath); os.IsNotExist(err) {
		return []models.HistoryEvent{}, nil
	}
	data, err := os.ReadFile(s.logFilePath)
	if err != nil {
		return []models.HistoryEvent{}, nil
	}
	var events []models.HistoryEvent
	if err := json.Unmarshal(data, &events); err != nil {
		return []models.HistoryEvent{}, nil
	}
	return events, nil
}

func (s *HistoryService) saveHistory(events []models.HistoryEvent) error {
	data, err := json.MarshalIndent(events, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(s.logFilePath, data, 0644)
}

func escapeCsv(value string) string {
	return strings.ReplaceAll(value, "\"", "\"\"")
}
