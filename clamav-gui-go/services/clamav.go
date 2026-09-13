package services

import (
	"bufio"
	"fmt"
	"net"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"time"

	"clamav-gui/models"
)

type ClamAVService struct {
	mu         sync.Mutex
	managedPid int
}

func NewClamAVService() *ClamAVService {
	return &ClamAVService{}
}

func exeName(base string) string {
	if runtime.GOOS == "windows" {
		return base + ".exe"
	}
	return base
}

func (s *ClamAVService) IsClamAVInstalled(clamavPath string) bool {
	if clamavPath == "" {
		return false
	}
	info, err := os.Stat(clamavPath)
	if err != nil || !info.IsDir() {
		return false
	}
	clamscan := filepath.Join(clamavPath, exeName("clamscan"))
	freshclam := filepath.Join(clamavPath, exeName("freshclam"))
	_, err1 := os.Stat(clamscan)
	_, err2 := os.Stat(freshclam)
	return err1 == nil && err2 == nil
}

func (s *ClamAVService) CreateDefaultConfigFiles(clamavPath string) error {
	dbPath := filepath.Join(clamavPath, "database")
	os.MkdirAll(dbPath, 0755)

	freshclamConf := filepath.Join(clamavPath, "freshclam.conf")
	if _, err := os.Stat(freshclamConf); os.IsNotExist(err) {
		content := fmt.Sprintf("DatabaseDirectory \"%s\"\n", dbPath)
		if err := os.WriteFile(freshclamConf, []byte(content), 0644); err != nil {
			return err
		}
	}

	clamdConf := filepath.Join(clamavPath, "clamd.conf")
	if _, err := os.Stat(clamdConf); os.IsNotExist(err) {
		return s.UpdateClamdConfig(clamavPath, models.ScanOptions{})
	}
	return nil
}

func (s *ClamAVService) RunFreshclam(clamavPath string) (string, string, error) {
	if err := s.CreateDefaultConfigFiles(clamavPath); err != nil {
		return "", "", err
	}

	exe := filepath.Join(clamavPath, exeName("freshclam"))
	confPath := filepath.Join(clamavPath, "freshclam.conf")
	cmd := exec.Command(exe, "--config-file", confPath)
	cmd.Dir = clamavPath

	var stdout, stderr strings.Builder
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	cmd.Run() // ignore exit code, freshclam may return non-zero

	return stdout.String(), stderr.String(), nil
}

func (s *ClamAVService) InitializeConfiguration(clamavPath string) (string, error) {
	confExamples := filepath.Join(clamavPath, "conf_examples")
	if _, err := os.Stat(confExamples); os.IsNotExist(err) {
		return "Error: 'conf_examples' directory not found. The selected folder is not a valid ClamAV installation.", nil
	}

	freshclamSample := filepath.Join(confExamples, "freshclam.conf.sample")
	clamdSample := filepath.Join(confExamples, "clamd.conf.sample")

	if _, err := os.Stat(freshclamSample); os.IsNotExist(err) {
		return "Error: Sample .conf files not found in 'conf_examples'.", nil
	}
	if _, err := os.Stat(clamdSample); os.IsNotExist(err) {
		return "Error: Sample .conf files not found in 'conf_examples'.", nil
	}

	// Copy freshclam.conf from sample, removing "Example" line
	data, err := os.ReadFile(freshclamSample)
	if err != nil {
		return fmt.Sprintf("Error reading freshclam sample: %s", err), nil
	}
	lines := strings.Split(string(data), "\n")
	var filtered []string
	for _, line := range lines {
		if strings.TrimSpace(line) != "Example" {
			filtered = append(filtered, line)
		}
	}
	freshclamConf := filepath.Join(clamavPath, "freshclam.conf")
	if err := os.WriteFile(freshclamConf, []byte(strings.Join(filtered, "\n")), 0644); err != nil {
		return fmt.Sprintf("Error writing freshclam.conf: %s", err), nil
	}

	// Generate clamd.conf
	if err := s.UpdateClamdConfig(clamavPath, models.ScanOptions{}); err != nil {
		return fmt.Sprintf("Error generating clamd.conf: %s", err), nil
	}

	// Create database directory
	dbPath := filepath.Join(clamavPath, "database")
	os.MkdirAll(dbPath, 0755)

	return "Configuration files initialized successfully. You can now run an update.", nil
}

func (s *ClamAVService) IsClamdRunning() bool {
	if s.tryPingDaemon(300 * time.Millisecond) {
		return true
	}
	// On Windows, check for clamd process
	if runtime.GOOS == "windows" {
		cmd := exec.Command("tasklist", "/FI", "IMAGENAME eq clamd.exe", "/NH")
		out, err := cmd.Output()
		if err == nil && strings.Contains(string(out), "clamd") {
			return true
		}
	} else {
		cmd := exec.Command("pgrep", "-x", "clamd")
		if err := cmd.Run(); err == nil {
			return true
		}
	}
	return false
}

func (s *ClamAVService) StartClamd(clamavPath string, options models.ScanOptions) error {
	if s.IsClamdRunning() {
		return nil
	}

	if err := s.UpdateClamdConfig(clamavPath, options); err != nil {
		return err
	}

	clamdExe := filepath.Join(clamavPath, exeName("clamd"))
	clamdConf := filepath.Join(clamavPath, "clamd.conf")

	if _, err := os.Stat(clamdExe); os.IsNotExist(err) {
		return fmt.Errorf("clamd executable not found at %s", clamdExe)
	}

	cmd := exec.Command(clamdExe, fmt.Sprintf("--config-file=%s", clamdConf), "--foreground")
	cmd.Dir = clamavPath
	if err := cmd.Start(); err != nil {
		return fmt.Errorf("failed to start clamd: %v", err)
	}

	s.mu.Lock()
	s.managedPid = cmd.Process.Pid
	s.mu.Unlock()

	// Wait for daemon to become responsive
	deadline := time.Now().Add(45 * time.Second)
	for time.Now().Before(deadline) {
		conn, err := net.DialTimeout("tcp", "127.0.0.1:3310", 500*time.Millisecond)
		if err == nil {
			conn.Close()
			return nil
		}
		// Check if process exited
		if cmd.ProcessState != nil && cmd.ProcessState.Exited() {
			break
		}
		time.Sleep(250 * time.Millisecond)
	}

	// Cleanup on failure
	if cmd.Process != nil {
		cmd.Process.Kill()
	}
	s.mu.Lock()
	s.managedPid = 0
	s.mu.Unlock()

	return fmt.Errorf("timed out waiting for clamd to become responsive")
}

func (s *ClamAVService) StopClamd() error {
	s.sendSimpleDaemonCommand("SHUTDOWN", false)
	time.Sleep(700 * time.Millisecond)

	s.mu.Lock()
	pid := s.managedPid
	s.mu.Unlock()

	if pid > 0 {
		proc, err := os.FindProcess(pid)
		if err == nil {
			proc.Kill()
		}
		s.mu.Lock()
		s.managedPid = 0
		s.mu.Unlock()
		return nil
	}

	// Kill any remaining clamd processes
	if runtime.GOOS == "windows" {
		exec.Command("taskkill", "/F", "/IM", "clamd.exe").Run()
	} else {
		exec.Command("pkill", "-x", "clamd").Run()
	}
	return nil
}

func (s *ClamAVService) GetLogFilePath(clamavPath string) string {
	confPath := filepath.Join(clamavPath, "clamd.conf")
	data, err := os.ReadFile(confPath)
	if err != nil {
		return ""
	}
	for _, line := range strings.Split(string(data), "\n") {
		trimmed := strings.TrimSpace(line)
		if strings.HasPrefix(strings.ToLower(trimmed), "logfile") {
			path := strings.TrimSpace(trimmed[len("LogFile"):])
			path = strings.Trim(path, "\"")
			return path
		}
	}
	return ""
}

func (s *ClamAVService) UpdateClamdConfig(clamavPath string, options models.ScanOptions) error {
	clamdConf := filepath.Join(clamavPath, "clamd.conf")
	dbPath := filepath.Join(clamavPath, "database")
	os.MkdirAll(dbPath, 0755)
	logPath := filepath.Join(clamavPath, "clamd.log")

	lines := []string{
		fmt.Sprintf("DatabaseDirectory \"%s\"", dbPath),
		fmt.Sprintf("LogFile \"%s\"", logPath),
		"LogTime yes",
		"LogVerbose yes",
		"TCPSocket 3310",
	}

	return os.WriteFile(clamdConf, []byte(strings.Join(lines, "\n")+"\n"), 0644)
}

func (s *ClamAVService) PingDaemon() string {
	if !s.IsClamdRunning() {
		return "Daemon not running."
	}
	result, err := s.sendSimpleDaemonCommand("PING", false)
	if err != nil {
		return fmt.Sprintf("Error pinging daemon: %v", err)
	}
	if result == "" {
		return "No response."
	}
	return result
}

func (s *ClamAVService) ReloadDatabase() string {
	if !s.IsClamdRunning() {
		return "Daemon not running."
	}
	result, err := s.sendSimpleDaemonCommand("RELOAD", false)
	if err != nil {
		return fmt.Sprintf("Error reloading database: %v", err)
	}
	if result == "" {
		return "No response."
	}
	return result
}

func (s *ClamAVService) GetVersion() string {
	if !s.IsClamdRunning() {
		return "Daemon not running."
	}
	result, err := s.sendSimpleDaemonCommand("VERSION", false)
	if err != nil {
		return fmt.Sprintf("Error getting version: %v", err)
	}
	if result == "" {
		return "No response."
	}
	return result
}

func (s *ClamAVService) ShutdownDaemon() {
	if !s.IsClamdRunning() {
		return
	}
	s.sendSimpleDaemonCommand("SHUTDOWN", false)
	s.mu.Lock()
	s.managedPid = 0
	s.mu.Unlock()
}

func (s *ClamAVService) GetStats() string {
	if !s.IsClamdRunning() {
		return "Daemon not running."
	}
	result, err := s.sendSimpleDaemonCommand("STATS", true)
	if err != nil {
		return fmt.Sprintf("Error getting stats: %v", err)
	}
	if result == "" {
		return "No response."
	}
	return result
}

func (s *ClamAVService) GetVersionCommands() string {
	if !s.IsClamdRunning() {
		return "Daemon not running."
	}
	result, err := s.sendSimpleDaemonCommand("VERSIONCOMMANDS", true)
	if err != nil {
		return fmt.Sprintf("Error getting version commands: %v", err)
	}
	if result == "" {
		return "No response."
	}
	return result
}

func (s *ClamAVService) ScanFileWithDaemon(filePath string) string {
	if !s.IsClamdRunning() {
		return filePath + ": Daemon not running."
	}
	if _, err := os.Stat(filePath); os.IsNotExist(err) {
		return filePath + ": File not found."
	}
	result, err := s.sendSimpleDaemonCommand("CONTSCAN "+filePath, false)
	if err != nil {
		return fmt.Sprintf("%s: Error scanning file: %v", filePath, err)
	}
	if result == "" {
		return filePath + ": No response from daemon."
	}
	return result
}

func (s *ClamAVService) ScanFolderWithDaemon(folderPath string) string {
	if !s.IsClamdRunning() {
		return folderPath + ": Daemon not running."
	}
	if info, err := os.Stat(folderPath); err != nil || !info.IsDir() {
		return folderPath + ": Folder not found."
	}
	result, err := s.sendSimpleDaemonCommand("CONTSCAN "+folderPath, true)
	if err != nil {
		return fmt.Sprintf("%s: Error scanning folder: %v", folderPath, err)
	}
	if result == "" {
		return folderPath + ": No response from daemon."
	}
	return result
}

// RunClamscan runs clamscan and calls onLine for each output line.
// Returns when the scan is complete or the done channel is closed.
func (s *ClamAVService) RunClamscan(clamavPath, scanPath string, options models.ScanOptions, onLine func(string), done <-chan struct{}) error {
	clamscanExe := filepath.Join(clamavPath, exeName("clamscan"))
	dbPath := filepath.Join(clamavPath, "database")

	args := []string{"--stdout", "--database", dbPath}

	info, err := os.Stat(scanPath)
	if err != nil {
		return fmt.Errorf("scan path does not exist: %v", err)
	}
	if info.IsDir() {
		args = append(args, "-r")
	}

	if options.HeuristicAlerts {
		args = append(args, "--heuristic-alerts=yes")
	}
	if options.ScanEncrypted {
		args = append(args, "--alert-encrypted=yes")
	}
	if options.LeaveTemps {
		args = append(args, "--leave-temps=yes")
	}
	if options.MoveToQuarantine && options.QuarantinePath != "" {
		os.MkdirAll(options.QuarantinePath, 0755)
		args = append(args, "--move", options.QuarantinePath)
	}

	args = append(args, scanPath)

	cmd := exec.Command(clamscanExe, args...)
	cmd.Dir = clamavPath

	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}
	stderr, err := cmd.StderrPipe()
	if err != nil {
		return err
	}

	if err := cmd.Start(); err != nil {
		return err
	}

	// Read stdout in a goroutine
	go func() {
		scanner := bufio.NewScanner(stdout)
		for scanner.Scan() {
			select {
			case <-done:
				cmd.Process.Kill()
				return
			default:
				onLine(scanner.Text())
			}
		}
	}()

	// Read stderr in a goroutine
	go func() {
		scanner := bufio.NewScanner(stderr)
		for scanner.Scan() {
			select {
			case <-done:
				return
			default:
				onLine(scanner.Text())
			}
		}
	}()

	// Wait for process or cancellation
	waitCh := make(chan error, 1)
	go func() {
		waitCh <- cmd.Wait()
	}()

	select {
	case <-done:
		cmd.Process.Kill()
		return fmt.Errorf("scan cancelled")
	case err := <-waitCh:
		// clamscan returns 1 when viruses found, that's not an error for us
		if err != nil {
			if exitErr, ok := err.(*exec.ExitError); ok {
				if exitErr.ExitCode() == 1 {
					return nil // virus found is expected
				}
			}
		}
		return err
	}
}

func (s *ClamAVService) tryPingDaemon(timeout time.Duration) bool {
	conn, err := net.DialTimeout("tcp", "127.0.0.1:3310", timeout)
	if err != nil {
		return false
	}
	defer conn.Close()

	conn.SetDeadline(time.Now().Add(timeout))
	fmt.Fprintf(conn, "nPING\n")
	reader := bufio.NewReader(conn)
	response, err := reader.ReadString('\n')
	if err != nil {
		return false
	}
	return strings.TrimSpace(strings.ToUpper(response)) == "PONG"
}

func (s *ClamAVService) sendSimpleDaemonCommand(command string, readToEnd bool) (string, error) {
	conn, err := net.DialTimeout("tcp", "127.0.0.1:3310", 5*time.Second)
	if err != nil {
		return "", err
	}
	defer conn.Close()

	conn.SetDeadline(time.Now().Add(30 * time.Second))
	fmt.Fprintf(conn, "n%s\n", command)

	if readToEnd {
		var sb strings.Builder
		scanner := bufio.NewScanner(conn)
		for scanner.Scan() {
			sb.WriteString(scanner.Text())
			sb.WriteString("\n")
		}
		return strings.TrimSpace(sb.String()), nil
	}

	reader := bufio.NewReader(conn)
	line, err := reader.ReadString('\n')
	if err != nil {
		return "", err
	}
	return strings.TrimSpace(line), nil
}
