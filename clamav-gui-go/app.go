package main

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"time"

	"clamav-gui/models"
	"clamav-gui/services"

	"github.com/wailsapp/wails/v2/pkg/runtime"
)

type App struct {
	ctx              context.Context
	clamavSvc        *services.ClamAVService
	historySvc       *services.HistoryService
	quarantineSvc    *services.QuarantineService
	settingsSvc      *services.SettingsService
	schedulerSvc     *services.SchedulerService
	healthCheckSvc   *services.HealthCheckService
	clamAVPath       string
	scanCancel       context.CancelFunc
	scanMu           sync.Mutex
	lastKnownViruses string
}

func NewApp() *App {
	return &App{
		clamavSvc:      services.NewClamAVService(),
		historySvc:     services.NewHistoryService(),
		quarantineSvc:  services.NewQuarantineService(),
		settingsSvc:    services.NewSettingsService(),
		schedulerSvc:   services.NewSchedulerService(),
		healthCheckSvc: services.NewHealthCheckService(),
	}
}

func (a *App) startup(ctx context.Context) {
	a.ctx = ctx

	// Load saved path and attempt initial setup
	path := a.settingsSvc.LoadPath()
	if path != "" && a.clamavSvc.IsClamAVInstalled(path) {
		a.clamAVPath = path
		// Try to start daemon on startup
		if !a.clamavSvc.IsClamdRunning() {
			go a.clamavSvc.StartClamd(path, models.ScanOptions{})
		}
	}
}

func (a *App) shutdown(ctx context.Context) {
	a.scanMu.Lock()
	if a.scanCancel != nil {
		a.scanCancel()
	}
	a.scanMu.Unlock()
}

// ============================================================
// Path / Configuration
// ============================================================

func (a *App) GetDashboardData() models.DashboardData {
	configured := a.clamAVPath != "" && a.clamavSvc.IsClamAVInstalled(a.clamAVPath)
	running := false
	statusText := "ClamAV is not configured."
	virusDefs := "N/A"
	daemonStats := "N/A"

	if configured {
		statusText = fmt.Sprintf("ClamAV configured at: %s", a.clamAVPath)
		running = a.clamavSvc.IsClamdRunning()
		if running {
			ver := a.clamavSvc.GetVersion()
			parts := strings.Split(ver, "/")
			if len(parts) >= 2 {
				virusDefs = strings.TrimSpace(parts[1])
			} else {
				virusDefs = ver
			}
			stats := a.clamavSvc.GetStats()
			if stats != "" {
				daemonStats = stats
			}
		} else {
			daemonStats = "Daemon not running"
		}
	}

	// Calculate history stats
	events, _ := a.historySvc.LoadHistory()
	totalScans := 0
	totalInfected := 0
	var lastUpdate *string
	re := regexp.MustCompile(`Infected files: (\d+)`)

	for _, e := range events {
		if e.EventType == "Scan" {
			totalScans++
			m := re.FindStringSubmatch(e.Details)
			if len(m) > 1 {
				n, _ := strconv.Atoi(m[1])
				totalInfected += n
			}
		}
		if e.EventType == "Update" && lastUpdate == nil {
			t := e.Timestamp.Format("2006-01-02 15:04")
			lastUpdate = &t
		}
	}

	scheduled := a.schedulerSvc.IsScheduledScanConfigured()

	return models.DashboardData{
		StatusText:              statusText,
		VirusDefinitionsVersion: virusDefs,
		DaemonStats:             daemonStats,
		LastUpdateTime:          lastUpdate,
		TotalScans:              totalScans,
		TotalInfectedFiles:      totalInfected,
		IsClamAVConfigured:      configured,
		IsClamDRunning:          running,
		IsScheduledScanEnabled:  scheduled,
	}
}

func (a *App) SelectClamAVPath() (string, error) {
	path, err := runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select the folder where you extracted ClamAV",
	})
	if err != nil {
		return "", err
	}
	if path == "" {
		return "", nil
	}

	if !a.clamavSvc.IsClamAVInstalled(path) {
		return "", fmt.Errorf("the selected folder is not a valid ClamAV installation")
	}

	a.settingsSvc.SavePath(path)
	a.clamAVPath = path
	return path, nil
}

func (a *App) GetClamAVPath() string {
	return a.clamAVPath
}

func (a *App) IsClamAVConfigured() bool {
	return a.clamAVPath != "" && a.clamavSvc.IsClamAVInstalled(a.clamAVPath)
}

func (a *App) InitializeConfig() (string, error) {
	if a.clamAVPath == "" {
		return "ClamAV is not configured.", nil
	}
	result, err := a.clamavSvc.InitializeConfiguration(a.clamAVPath)
	if err != nil {
		return "", err
	}
	a.historySvc.LogEvent("Config Initialized", result)
	return result, nil
}

// ============================================================
// Daemon Control
// ============================================================

func (a *App) StartDaemon() (string, error) {
	if a.clamAVPath == "" {
		return "ClamAV is not configured.", nil
	}
	err := a.clamavSvc.StartClamd(a.clamAVPath, models.ScanOptions{})
	if err != nil {
		return fmt.Sprintf("Failed to start daemon: %v", err), nil
	}
	return "ClamAV daemon started successfully.", nil
}

func (a *App) StopDaemon() (string, error) {
	err := a.clamavSvc.StopClamd()
	if err != nil {
		return fmt.Sprintf("Failed to stop daemon: %v", err), nil
	}
	return "ClamAV daemon stopped.", nil
}

func (a *App) IsDaemonRunning() bool {
	return a.clamavSvc.IsClamdRunning()
}

func (a *App) PingDaemon() string {
	return a.clamavSvc.PingDaemon()
}

func (a *App) ReloadDatabase() string {
	return a.clamavSvc.ReloadDatabase()
}

func (a *App) GetDaemonVersion() string {
	return a.clamavSvc.GetVersion()
}

func (a *App) ShutdownDaemon() string {
	a.clamavSvc.ShutdownDaemon()
	return "Shutdown command sent."
}

func (a *App) GetDaemonStats() string {
	return a.clamavSvc.GetStats()
}

func (a *App) GetVersionCommands() string {
	return a.clamavSvc.GetVersionCommands()
}

func (a *App) ScanFolderWithDaemon() (string, error) {
	path, err := runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select a folder to scan using clamd daemon",
	})
	if err != nil || path == "" {
		return "", err
	}
	return a.clamavSvc.ScanFolderWithDaemon(path), nil
}

// ============================================================
// Signatures Update
// ============================================================

func (a *App) UpdateSignatures() (map[string]string, error) {
	if a.clamAVPath == "" {
		return map[string]string{"status": "error", "message": "ClamAV is not configured."}, nil
	}

	a.historySvc.LogEvent("Update", "Signature update process started.")

	stdout, stderr, err := a.clamavSvc.RunFreshclam(a.clamAVPath)
	if err != nil {
		msg := fmt.Sprintf("Unexpected error: %v", err)
		a.historySvc.LogEvent("Update Failed", msg)
		return map[string]string{"status": "error", "message": msg, "output": ""}, nil
	}

	output := fmt.Sprintf("Output:\n%s\n\nErrors:\n%s", stdout, stderr)

	re := regexp.MustCompile(`sigs: (\d+)`)
	m := re.FindStringSubmatch(stdout)
	if len(m) > 1 {
		a.lastKnownViruses = m[1]
	}

	if stderr != "" || strings.Contains(stdout, "ERROR") {
		a.historySvc.LogEvent("Update Failed", fmt.Sprintf("Error: %s\nOutput: %s", stderr, stdout))
		return map[string]string{"status": "error", "message": "An error occurred during the update.", "output": output}, nil
	} else if strings.Contains(stdout, "up-to-date") {
		a.historySvc.LogEvent("Update", "Database already up-to-date.")
		return map[string]string{"status": "uptodate", "message": "Virus database is already up-to-date.", "output": output}, nil
	}

	a.historySvc.LogEvent("Update", "Signature update process finished.")
	return map[string]string{"status": "success", "message": "Update successful!", "output": output}, nil
}

// ============================================================
// Scanning
// ============================================================

func (a *App) ScanFolder(optionsJSON string) error {
	path, err := runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select a folder to scan",
	})
	if err != nil || path == "" {
		return err
	}
	return a.runScan(path, optionsJSON)
}

func (a *App) ScanFile(optionsJSON string) error {
	path, err := runtime.OpenFileDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select a file to scan",
	})
	if err != nil || path == "" {
		return err
	}
	return a.runScan(path, optionsJSON)
}

func (a *App) CancelScan() {
	a.scanMu.Lock()
	defer a.scanMu.Unlock()
	if a.scanCancel != nil {
		a.scanCancel()
		a.scanCancel = nil
	}
}

func (a *App) runScan(scanPath, optionsJSON string) error {
	if a.clamAVPath == "" {
		return fmt.Errorf("ClamAV is not configured")
	}

	// Parse options
	var options models.ScanOptions
	if optionsJSON != "" {
		// Simple JSON parse
		options = parseScanOptions(optionsJSON)
	}

	a.scanMu.Lock()
	if a.scanCancel != nil {
		a.scanCancel()
	}
	ctx, cancel := context.WithCancel(context.Background())
	a.scanCancel = cancel
	a.scanMu.Unlock()

	a.historySvc.LogEvent("Scan", fmt.Sprintf("Scan started for: %s", scanPath))

	// Emit scan started
	runtime.EventsEmit(a.ctx, "scan:started", scanPath)

	go func() {
		defer func() {
			a.scanMu.Lock()
			a.scanCancel = nil
			a.scanMu.Unlock()
		}()

		infectedFiles := 0
		summary := models.ScanSummary{}
		inSummarySection := false
		detectedThreats := map[string]string{}
		isFolder := false
		if info, err := os.Stat(scanPath); err == nil {
			isFolder = info.IsDir()
		}

		// Record quarantine files before scan
		var quarantineBefore map[string]bool
		if options.MoveToQuarantine && options.QuarantinePath != "" {
			quarantineBefore = make(map[string]bool)
			if info, err := os.Stat(options.QuarantinePath); err == nil && info.IsDir() {
				filepath.Walk(options.QuarantinePath, func(path string, info os.FileInfo, err error) error {
					if err == nil && !info.IsDir() {
						quarantineBefore[path] = true
					}
					return nil
				})
			}
		}

		onLine := func(line string) {
			if strings.TrimSpace(line) == "" {
				return
			}

			if strings.Contains(strings.ToUpper(line), "SCAN SUMMARY") {
				inSummarySection = true
				return
			}

			if inSummarySection {
				parts := strings.SplitN(line, ":", 2)
				if len(parts) != 2 {
					return
				}
				key := strings.TrimSpace(parts[0])
				value := strings.TrimSpace(parts[1])
				switch key {
				case "Known viruses":
					summary.KnownViruses = value
				case "Engine version":
					summary.EngineVersion = value
				case "Scanned directories":
					summary.ScannedDirectories = value
				case "Scanned files":
					summary.ScannedFiles = value
				case "Infected files":
					summary.InfectedFiles = value
				case "Time":
					summary.TimeTaken = value
				}
				return
			}

			// Parse scan result line
			filePath, status := parseScanResultLine(line)
			if filePath == "" {
				return
			}

			if strings.HasSuffix(strings.ToUpper(status), "FOUND") {
				infectedFiles++
				threatName := strings.TrimSuffix(status, "FOUND")
				threatName = strings.TrimSpace(threatName)
				if threatName == "" {
					threatName = "Unknown"
				}
				detectedThreats[filePath] = threatName
			}

			result := models.ScanResult{
				FilePath: filePath,
				Status:   status,
				IsFolder: false,
			}
			runtime.EventsEmit(a.ctx, "scan:result", result)
		}

		err := a.clamavSvc.RunClamscan(a.clamAVPath, scanPath, options, onLine, ctx.Done())

		cancelled := false
		if err != nil && strings.Contains(err.Error(), "cancelled") {
			cancelled = true
		}

		// Handle quarantine items
		if options.MoveToQuarantine && options.QuarantinePath != "" {
			if info, err := os.Stat(options.QuarantinePath); err == nil && info.IsDir() {
				var newItems []models.QuarantineItem
				filepath.Walk(options.QuarantinePath, func(path string, info os.FileInfo, err error) error {
					if err == nil && !info.IsDir() && !quarantineBefore[path] {
						threat := "Detected by clamscan"
						for k, v := range detectedThreats {
							if filepath.Base(k) == filepath.Base(path) {
								threat = v
								break
							}
						}
						originalPath := "Unknown"
						for k := range detectedThreats {
							if filepath.Base(k) == filepath.Base(path) {
								originalPath = k
								break
							}
						}
						newItems = append(newItems, models.QuarantineItem{
							Id:             fmt.Sprintf("%d", time.Now().UnixNano()),
							QuarantinePath: path,
							OriginalPath:   originalPath,
							ThreatName:     threat,
							QuarantinedAt:  time.Now(),
							Notes:          "Moved automatically by clamscan --move",
						})
					}
					return nil
				})
				if len(newItems) > 0 {
					a.quarantineSvc.AddItems(newItems)
				}
			}
		}

		if cancelled {
			a.historySvc.LogEvent("Scan", fmt.Sprintf("Scan cancelled for: %s", scanPath))
		} else {
			a.historySvc.LogEvent("Scan", fmt.Sprintf("Scan finished for: %s. Infected files: %d.", scanPath, infectedFiles))
		}

		_ = isFolder
		runtime.EventsEmit(a.ctx, "scan:complete", models.ScanProgress{
			Summary: &summary,
			Done:    true,
		})
	}()

	return nil
}

// ============================================================
// History
// ============================================================

func (a *App) LoadHistory() ([]models.HistoryEvent, error) {
	return a.historySvc.LoadHistory()
}

func (a *App) DeleteHistoryEvent(id string) error {
	return a.historySvc.DeleteHistoryEvent(id)
}

func (a *App) ClearHistory() error {
	return a.historySvc.ClearHistory()
}

func (a *App) ExportHistory(format string) (string, error) {
	path, err := runtime.SaveFileDialog(a.ctx, runtime.SaveDialogOptions{
		Title:           "Export History",
		DefaultFilename: fmt.Sprintf("ClamAV_History_%s", time.Now().Format("20060102_150405")),
	})
	if err != nil || path == "" {
		return "", err
	}

	if format == "json" {
		err = a.historySvc.ExportAsJSON(path)
	} else {
		err = a.historySvc.ExportAsCSV(path)
	}
	if err != nil {
		return "", err
	}
	return "History exported successfully.", nil
}

// ============================================================
// Quarantine
// ============================================================

func (a *App) LoadQuarantine() ([]models.QuarantineItem, error) {
	a.quarantineSvc.ClearMissingFiles()
	return a.quarantineSvc.LoadItems()
}

func (a *App) RemoveQuarantineItem(id, quarantinePath string) error {
	if _, err := os.Stat(quarantinePath); err == nil {
		os.Remove(quarantinePath)
	}
	return a.quarantineSvc.RemoveItem(id)
}

func (a *App) RestoreQuarantineItem(id, quarantinePath, originalPath string) (string, error) {
	if _, err := os.Stat(quarantinePath); os.IsNotExist(err) {
		return "Quarantined file no longer exists.", fmt.Errorf("file not found")
	}

	targetPath := originalPath
	if targetPath == "" || strings.EqualFold(targetPath, "Unknown") {
		// Ask user where to restore
		path, err := runtime.SaveFileDialog(a.ctx, runtime.SaveDialogOptions{
			Title:           "Select restore destination",
			DefaultFilename: filepath.Base(quarantinePath),
		})
		if err != nil || path == "" {
			return "", err
		}
		targetPath = path
	}

	dir := filepath.Dir(targetPath)
	os.MkdirAll(dir, 0755)

	if err := os.Rename(quarantinePath, targetPath); err != nil {
		return "", fmt.Errorf("failed to restore file: %v", err)
	}
	a.quarantineSvc.RemoveItem(id)
	return "File restored successfully.", nil
}

// ============================================================
// Monitoring
// ============================================================

func (a *App) GetMonitoredPaths() []string {
	return a.settingsSvc.LoadMonitoredPaths()
}

func (a *App) AddMonitoredPath() (string, error) {
	path, err := runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select a folder to monitor",
	})
	if err != nil || path == "" {
		return "", err
	}

	paths := a.settingsSvc.LoadMonitoredPaths()
	for _, p := range paths {
		if strings.EqualFold(p, path) {
			return path, nil // already exists
		}
	}
	paths = append(paths, path)
	a.settingsSvc.SaveMonitoredPaths(paths)
	return path, nil
}

func (a *App) RemoveMonitoredPath(path string) error {
	paths := a.settingsSvc.LoadMonitoredPaths()
	var filtered []string
	for _, p := range paths {
		if !strings.EqualFold(p, path) {
			filtered = append(filtered, p)
		}
	}
	return a.settingsSvc.SaveMonitoredPaths(filtered)
}

func (a *App) GetFileTypeFilters() []string {
	return a.settingsSvc.LoadMonitoringFilters()
}

func (a *App) AddFileTypeFilter(filter string) error {
	filter = normalizeFilter(filter)
	if filter == "" {
		return nil
	}
	filters := a.settingsSvc.LoadMonitoringFilters()
	for _, f := range filters {
		if strings.EqualFold(f, filter) {
			return nil
		}
	}
	filters = append(filters, filter)
	return a.settingsSvc.SaveMonitoringFilters(filters)
}

func (a *App) RemoveFileTypeFilter(filter string) error {
	filters := a.settingsSvc.LoadMonitoringFilters()
	var filtered []string
	for _, f := range filters {
		if !strings.EqualFold(f, filter) {
			filtered = append(filtered, f)
		}
	}
	return a.settingsSvc.SaveMonitoringFilters(filtered)
}

func (a *App) GetExcludedPaths() []string {
	return a.settingsSvc.LoadMonitoringExclusions()
}

func (a *App) AddExcludedPath(pathType string) (string, error) {
	var path string
	var err error
	if pathType == "File" {
		path, err = runtime.OpenFileDialog(a.ctx, runtime.OpenDialogOptions{
			Title: "Select a file to exclude from monitoring",
		})
	} else {
		path, err = runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
			Title: "Select a folder to exclude from monitoring",
		})
	}
	if err != nil || path == "" {
		return "", err
	}

	paths := a.settingsSvc.LoadMonitoringExclusions()
	for _, p := range paths {
		if strings.EqualFold(p, path) {
			return path, nil
		}
	}
	paths = append(paths, path)
	a.settingsSvc.SaveMonitoringExclusions(paths)
	return path, nil
}

func (a *App) RemoveExcludedPath(path string) error {
	paths := a.settingsSvc.LoadMonitoringExclusions()
	var filtered []string
	for _, p := range paths {
		if !strings.EqualFold(p, path) {
			filtered = append(filtered, p)
		}
	}
	return a.settingsSvc.SaveMonitoringExclusions(filtered)
}

// ============================================================
// Health Check
// ============================================================

func (a *App) RunHealthCheck() string {
	return a.healthCheckSvc.Run(a.clamAVPath, a.clamavSvc)
}

// ============================================================
// Scheduler
// ============================================================

func (a *App) ScheduleDailyScan(scanPath, scanTime string) (string, error) {
	if scanPath == "" {
		return "Set a scheduled scan path first.", nil
	}
	if _, err := os.Stat(scanPath); os.IsNotExist(err) {
		return "Scheduled scan path does not exist.", nil
	}

	parts := strings.Split(scanTime, ":")
	if len(parts) != 2 {
		return "Time format must be HH:mm.", nil
	}
	hh, err1 := strconv.Atoi(parts[0])
	mm, err2 := strconv.Atoi(parts[1])
	if err1 != nil || err2 != nil || hh < 0 || hh > 23 || mm < 0 || mm > 59 {
		return "Time format must be HH:mm.", nil
	}

	return a.schedulerSvc.CreateOrUpdateDailyScanTask(scanPath, hh, mm)
}

func (a *App) RemoveScheduledScan() (string, error) {
	return a.schedulerSvc.RemoveDailyScanTask()
}

func (a *App) IsScheduledScanConfigured() bool {
	return a.schedulerSvc.IsScheduledScanConfigured()
}

func (a *App) BrowseScheduledPath() (string, error) {
	return runtime.OpenDirectoryDialog(a.ctx, runtime.OpenDialogOptions{
		Title: "Select a folder to be scanned daily",
	})
}

// ============================================================
// Monitoring Log Export
// ============================================================

func (a *App) ExportMonitoringLog(entries []string) (string, error) {
	path, err := runtime.SaveFileDialog(a.ctx, runtime.SaveDialogOptions{
		Title:           "Export Monitoring Log",
		DefaultFilename: fmt.Sprintf("ClamAV_Monitoring_Log_%s.log", time.Now().Format("20060102_150405")),
	})
	if err != nil || path == "" {
		return "", err
	}

	content := strings.Join(entries, "\n")
	if err := os.WriteFile(path, []byte(content), 0644); err != nil {
		return "", fmt.Errorf("failed to export log: %v", err)
	}
	return "Log exported successfully.", nil
}

// ============================================================
// Helpers
// ============================================================

func parseScanOptions(json string) models.ScanOptions {
	// Simple manual parse - avoids importing encoding/json in this file
	opts := models.ScanOptions{}
	opts.HeuristicAlerts = strings.Contains(json, `"heuristicAlerts":true`)
	opts.ScanEncrypted = strings.Contains(json, `"scanEncrypted":true`)
	opts.LeaveTemps = strings.Contains(json, `"leaveTemps":true`)
	opts.MoveToQuarantine = strings.Contains(json, `"moveToQuarantine":true`)

	// Extract quarantinePath
	re := regexp.MustCompile(`"quarantinePath"\s*:\s*"([^"]*)"`)
	m := re.FindStringSubmatch(json)
	if len(m) > 1 {
		opts.QuarantinePath = m[1]
	}
	return opts
}

func parseScanResultLine(line string) (string, string) {
	trimmed := strings.TrimSpace(line)
	if trimmed == "" {
		return "", ""
	}

	// Windows path pattern: C:\path\to\file: STATUS
	re := regexp.MustCompile(`^(?P<path>[A-Za-z]:\\.*?):\s*(?P<status>.+)$`)
	m := re.FindStringSubmatch(trimmed)
	if len(m) >= 3 {
		return strings.TrimSpace(m[1]), strings.TrimSpace(m[2])
	}

	// Unix path or fallback
	idx := strings.LastIndex(trimmed, ":")
	if idx > 0 && idx < len(trimmed)-1 {
		return strings.TrimSpace(trimmed[:idx]), strings.TrimSpace(trimmed[idx+1:])
	}

	if strings.HasPrefix(strings.ToUpper(trimmed), "WARNING") || strings.HasPrefix(strings.ToUpper(trimmed), "ERROR") {
		return "Scanner", trimmed
	}

	return "", ""
}

func normalizeFilter(filter string) string {
	f := strings.TrimSpace(filter)
	if f == "" {
		return ""
	}
	if strings.HasPrefix(f, ".") {
		return "*" + f
	}
	return f
}
