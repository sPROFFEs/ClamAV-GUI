# Current Architecture Audit — ClamAV GUI (WPF)

## 1. Overview

The existing ClamAV GUI application is a Windows-only WPF (.NET 8) desktop client designed to interact with local ClamAV installations. It combines scanning orchestration, daemon management, signature updating, quarantine, scheduled tasks, and reactive filesystem monitoring.

## 2. Component Inventory & Classification

| Component / File | Classification | Description & Dependencies |
|------------------|----------------|----------------------------|
| `Models/ClamAVFeatures.cs` | PORTABLE | On-access feature flags |
| `Models/HistoryEvent.cs` | PORTABLE | History entry model (Guid, Timestamp, EventType, Details) |
| `Models/QuarantineItem.cs` | PORTABLE | Quarantined item metadata |
| `Models/ScanOptions.cs` | PORTABLE | Options for heuristic alerts, encrypted scanning, quarantine moves |
| `Models/ScanResult.cs` | PORTABLE | Hierarchical scan result representation |
| `Models/ScanSummary.cs` | PORTABLE | Summary counts, version, time |
| `Services/ClamAVService.cs` | MIXED | Monolithic static service managing process execution (CliWrap), socket I/O, process killing (`Process.GetProcessesByName`), config generation |
| `Services/HealthCheckService.cs` | MIXED | Performs executable & config checks, assumes Windows `.exe` names |
| `Services/HistoryService.cs` | PORTABLE | JSON/CSV persistence in MyDocuments |
| `Services/QuarantineService.cs` | PORTABLE | JSON quarantine database management |
| `Services/SchedulerService.cs` | WINDOWS-SPECIFIC | Windows Task Scheduler execution via `schtasks.exe` |
| `Services/SettingsService.cs` | PORTABLE | Settings persistence in text files |
| `ViewModels/MainViewModel.cs` | MIXED | Manages commands, FileSystemWatcher reactive monitoring, WPF Dispatcher, MessageBox, VistaDialogs, Windows Registry (`SOFTWARE\Microsoft\Windows\CurrentVersion\Run`) |
| `MainWindow.xaml` / `.cs` | UI-SPECIFIC | WPF XAML UI with MaterialDesignThemes |
| `App.xaml` / `.cs` | UI-SPECIFIC | WPF Application shell and command-line entry |
| `Converters/*` | UI-SPECIFIC | WPF `IValueConverter` implementations |

## 3. Key Issues Identified

1. **Daemon Lifecycle & Process Killing (P0):** `ClamAVService.StopClamdAsync()` kills all processes named `clamd` on the system if managed PID is not tracked, risking destruction of unmanaged/system daemons.
2. **Insecure TCP Configuration (P0):** Managed `clamd.conf` specifies `TCPSocket 3310` without restricting `TCPAddr 127.0.0.1`.
3. **Destructive Config Overwrites (P0):** `InitializeConfigurationAsync` and `UpdateClamdConfigAsync` rewrite `clamd.conf` and `freshclam.conf` indiscriminately.
4. **Exit Codes & Result Semantics (P0):** Uses `CommandResultValidation.None` without mapping ClamAV exit codes (0 = Clean, 1 = Infected, 2 = Error).
5. **Direct WPF Coupling in ViewModel (P1):** `MainViewModel` references `Application.Current.Dispatcher`, `MessageBox.Show`, `VistaFolderBrowserDialog`, `SaveFileDialog`, and `Microsoft.Win32.Registry`.
6. **Platform Assumptions (P1):** Hardcoded paths and `.exe` executable extensions throughout service layers.
7. **Monolithic Service (P2):** `ClamAVService` mixes scanning, freshclam, daemon protocols, and config file generation.
