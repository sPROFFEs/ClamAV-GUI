package services

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

type HealthCheckService struct{}

func NewHealthCheckService() *HealthCheckService {
	return &HealthCheckService{}
}

func (s *HealthCheckService) Run(clamavPath string, clamavSvc *ClamAVService) string {
	var checks []string

	if clamavPath == "" {
		checks = append(checks, "FAIL: ClamAV path is not configured or does not exist.")
		return strings.Join(checks, "\n")
	}
	if info, err := os.Stat(clamavPath); err != nil || !info.IsDir() {
		checks = append(checks, "FAIL: ClamAV path does not exist.")
		return strings.Join(checks, "\n")
	}

	if _, err := os.Stat(filepath.Join(clamavPath, exeName("clamscan"))); err == nil {
		checks = append(checks, fmt.Sprintf("OK: %s found.", exeName("clamscan")))
	} else {
		checks = append(checks, fmt.Sprintf("FAIL: %s missing.", exeName("clamscan")))
	}

	if _, err := os.Stat(filepath.Join(clamavPath, exeName("freshclam"))); err == nil {
		checks = append(checks, fmt.Sprintf("OK: %s found.", exeName("freshclam")))
	} else {
		checks = append(checks, fmt.Sprintf("FAIL: %s missing.", exeName("freshclam")))
	}

	if _, err := os.Stat(filepath.Join(clamavPath, exeName("clamd"))); err == nil {
		checks = append(checks, fmt.Sprintf("OK: %s found.", exeName("clamd")))
	} else {
		checks = append(checks, fmt.Sprintf("WARN: %s missing (daemon features unavailable).", exeName("clamd")))
	}

	if _, err := os.Stat(filepath.Join(clamavPath, "clamd.conf")); err == nil {
		checks = append(checks, "OK: clamd.conf exists.")
	} else {
		checks = append(checks, "WARN: clamd.conf not found. Initialize configuration files.")
	}

	if _, err := os.Stat(filepath.Join(clamavPath, "freshclam.conf")); err == nil {
		checks = append(checks, "OK: freshclam.conf exists.")
	} else {
		checks = append(checks, "WARN: freshclam.conf not found. Initialize configuration files.")
	}

	dbPath := filepath.Join(clamavPath, "database")
	if info, err := os.Stat(dbPath); err == nil && info.IsDir() {
		entries, _ := os.ReadDir(dbPath)
		sigCount := 0
		for _, entry := range entries {
			name := entry.Name()
			if strings.HasSuffix(name, ".cvd") || strings.HasSuffix(name, ".cld") {
				sigCount++
			}
		}
		if sigCount > 0 {
			checks = append(checks, fmt.Sprintf("OK: signature database files found (%d).", sigCount))
		} else {
			checks = append(checks, "WARN: database folder exists but no .cvd/.cld signatures found.")
		}
	} else {
		checks = append(checks, "WARN: database folder is missing.")
	}

	ping := clamavSvc.PingDaemon()
	if strings.Contains(strings.ToUpper(ping), "PONG") {
		checks = append(checks, "OK: daemon responds to PING.")
	} else {
		checks = append(checks, fmt.Sprintf("INFO: daemon ping result: %s", ping))
	}

	return strings.Join(checks, "\n")
}
