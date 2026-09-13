package services

import (
	"fmt"
	"os"
	"os/exec"
	"runtime"
	"strings"
)

type SchedulerService struct{}

func NewSchedulerService() *SchedulerService {
	return &SchedulerService{}
}

const taskName = "ClamAV-GUI Daily Scan"

func (s *SchedulerService) CreateOrUpdateDailyScanTask(targetPath string, timeHH, timeMM int) (string, error) {
	if runtime.GOOS != "windows" {
		return "Scheduled tasks are only supported on Windows at this time.", nil
	}

	executablePath, err := os.Executable()
	if err != nil {
		return "Could not determine executable path for task scheduler.", nil
	}

	formattedTime := fmt.Sprintf("%02d:%02d", timeHH, timeMM)
	taskRun := fmt.Sprintf("\"%s\" -scan \"%s\"", executablePath, targetPath)
	arguments := fmt.Sprintf("/Create /F /SC DAILY /TN \"%s\" /TR \"%s\" /ST %s", taskName, taskRun, formattedTime)

	exitCode, output := runSchtasks(arguments)
	if exitCode == 0 {
		return fmt.Sprintf("Scheduled daily scan at %s.", formattedTime), nil
	}
	return fmt.Sprintf("Failed to schedule scan: %s", output), nil
}

func (s *SchedulerService) RemoveDailyScanTask() (string, error) {
	if runtime.GOOS != "windows" {
		return "Scheduled tasks are only supported on Windows at this time.", nil
	}

	exitCode, output := runSchtasks(fmt.Sprintf("/Delete /F /TN \"%s\"", taskName))
	if exitCode == 0 {
		return "Scheduled scan removed.", nil
	}
	return fmt.Sprintf("Failed to remove scheduled scan: %s", output), nil
}

func (s *SchedulerService) IsScheduledScanConfigured() bool {
	if runtime.GOOS != "windows" {
		return false
	}
	exitCode, _ := runSchtasks(fmt.Sprintf("/Query /TN \"%s\"", taskName))
	return exitCode == 0
}

func runSchtasks(arguments string) (int, string) {
	args := strings.Fields(arguments)
	// Re-parse to handle quoted strings properly
	cmd := exec.Command("schtasks.exe")
	cmd.SysProcAttr = nil

	// Build args properly
	cmd = exec.Command("schtasks.exe", args...)
	stdout, err := cmd.Output()
	if err != nil {
		if exitErr, ok := err.(*exec.ExitError); ok {
			return exitErr.ExitCode(), string(exitErr.Stderr)
		}
		return -1, err.Error()
	}
	return 0, strings.TrimSpace(string(stdout))
}
