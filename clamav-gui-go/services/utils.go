package services

import (
	"crypto/rand"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
)

func getAppDataPath() string {
	if runtime.GOOS == "windows" {
		docs := os.Getenv("USERPROFILE")
		if docs == "" {
			docs, _ = os.UserHomeDir()
		}
		return filepath.Join(docs, "Documents", "ClamAV-GUI")
	}
	home, _ := os.UserHomeDir()
	return filepath.Join(home, ".clamav-gui")
}

func generateID() string {
	b := make([]byte, 16)
	rand.Read(b)
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x",
		b[0:4], b[4:6], b[6:8], b[8:10], b[10:16])
}
