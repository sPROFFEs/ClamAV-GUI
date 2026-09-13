package models

import "time"

type HistoryEvent struct {
	Id        string    `json:"id"`
	Timestamp time.Time `json:"timestamp"`
	EventType string    `json:"eventType"`
	Details   string    `json:"details"`
}

type QuarantineItem struct {
	Id             string    `json:"id"`
	QuarantinePath string    `json:"quarantinePath"`
	OriginalPath   string    `json:"originalPath"`
	ThreatName     string    `json:"threatName"`
	QuarantinedAt  time.Time `json:"quarantinedAt"`
	Notes          string    `json:"notes"`
}

type ScanOptions struct {
	MoveToQuarantine bool   `json:"moveToQuarantine"`
	QuarantinePath   string `json:"quarantinePath"`
	HeuristicAlerts  bool   `json:"heuristicAlerts"`
	ScanEncrypted    bool   `json:"scanEncrypted"`
	LeaveTemps       bool   `json:"leaveTemps"`
}

type ScanResult struct {
	FilePath string       `json:"filePath"`
	Status   string       `json:"status"`
	IsFolder bool         `json:"isFolder"`
	Children []ScanResult `json:"children"`
}

type ScanSummary struct {
	KnownViruses      string `json:"knownViruses"`
	EngineVersion     string `json:"engineVersion"`
	ScannedDirectories string `json:"scannedDirectories"`
	ScannedFiles      string `json:"scannedFiles"`
	InfectedFiles     string `json:"infectedFiles"`
	TimeTaken         string `json:"timeTaken"`
}

type ScanProgress struct {
	Results []ScanResult `json:"results"`
	Summary *ScanSummary `json:"summary,omitempty"`
	Done    bool         `json:"done"`
	Error   string       `json:"error,omitempty"`
}

type DaemonStatus struct {
	Running               bool   `json:"running"`
	StatusText            string `json:"statusText"`
	VirusDefinitionsVersion string `json:"virusDefinitionsVersion"`
	DaemonStats           string `json:"daemonStats"`
}

type DashboardData struct {
	StatusText            string  `json:"statusText"`
	VirusDefinitionsVersion string `json:"virusDefinitionsVersion"`
	DaemonStats           string  `json:"daemonStats"`
	LastUpdateTime        *string `json:"lastUpdateTime"`
	TotalScans            int     `json:"totalScans"`
	TotalInfectedFiles    int     `json:"totalInfectedFiles"`
	IsClamAVConfigured    bool    `json:"isClamAVConfigured"`
	IsClamDRunning        bool    `json:"isClamDRunning"`
	IsScheduledScanEnabled bool   `json:"isScheduledScanEnabled"`
}
