package services

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sync"

	"clamav-gui/models"
)

type QuarantineService struct {
	mu     sync.Mutex
	dbPath string
}

func NewQuarantineService() *QuarantineService {
	appDataPath := getAppDataPath()
	os.MkdirAll(appDataPath, 0755)
	return &QuarantineService{
		dbPath: filepath.Join(appDataPath, "quarantine.json"),
	}
}

func (s *QuarantineService) LoadItems() ([]models.QuarantineItem, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.loadItemsUnsafe()
}

func (s *QuarantineService) AddItems(items []models.QuarantineItem) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	existing, _ := s.loadItemsUnsafe()
	existing = append(items, existing...)
	return s.save(existing)
}

func (s *QuarantineService) RemoveItem(id string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	existing, _ := s.loadItemsUnsafe()
	var filtered []models.QuarantineItem
	for _, item := range existing {
		if item.Id != id {
			filtered = append(filtered, item)
		}
	}
	return s.save(filtered)
}

func (s *QuarantineService) ClearMissingFiles() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	existing, _ := s.loadItemsUnsafe()
	var valid []models.QuarantineItem
	for _, item := range existing {
		if _, err := os.Stat(item.QuarantinePath); err == nil {
			valid = append(valid, item)
		}
	}
	return s.save(valid)
}

func (s *QuarantineService) loadItemsUnsafe() ([]models.QuarantineItem, error) {
	if _, err := os.Stat(s.dbPath); os.IsNotExist(err) {
		return []models.QuarantineItem{}, nil
	}
	data, err := os.ReadFile(s.dbPath)
	if err != nil {
		return []models.QuarantineItem{}, nil
	}
	var items []models.QuarantineItem
	if err := json.Unmarshal(data, &items); err != nil {
		return []models.QuarantineItem{}, nil
	}
	return items, nil
}

func (s *QuarantineService) save(items []models.QuarantineItem) error {
	if items == nil {
		items = []models.QuarantineItem{}
	}
	data, err := json.MarshalIndent(items, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(s.dbPath, data, 0644)
}
