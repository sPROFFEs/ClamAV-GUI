# Migration Checklist

## Core

- [x] ClamAV component discovery abstraction (`IClamAvBinaryLocator`)
- [x] Effective configuration model (`ClamAvEffectiveConfiguration`)
- [x] clamscan backend (`ClamScanBackend`)
- [x] clamd backend (`ClamdBackend`)
- [x] clamdscan backend (`ClamdScanBackend`)
- [x] Unix socket transport (`UnixSocketClamdTransport`)
- [x] TCP transport (`TcpClamdTransport`)
- [x] NUL protocol framing (`ClamdProtocolClient`)
- [x] Exit-code mapping (`ScanVerdict`, `ScanExecutionResult`)
- [x] Manual file scan & directory scan orchestration
- [x] Cancellation propagation
- [x] Definition update coordination (`IClamAvUpdater`)
- [x] Database validation & health checks

## Security / Correctness

- [x] Daemon ownership tracking (`DaemonOwnership`, `IManagedDaemonProcess`)
- [x] No external clamd termination
- [x] Loopback-only managed TCP clamd (`127.0.0.1`)
- [x] No destructive external config writes
- [x] Atomic settings/config writes
- [x] Protocol timeouts & cancellation

## User Data & Storage

- [x] Settings service (`ISettingsService`)
- [x] History service (`IHistoryService`)
- [x] Quarantine metadata & integrity (`IQuarantineService`)
- [x] Quarantine restore and delete

## Desktop & Platform

- [x] Tray service (`ITrayService`)
- [x] Notifications service (`INotificationService`)
- [x] Start on login abstraction (`IStartupService`)
- [x] Scheduler abstraction (`ISchedulerService`)
- [x] Diagnostics & System Info (`IDiagnosticsService`)
- [x] File / Folder dialogs (`IFileDialogService`)

## Real-Time & Monitoring

- [x] Capability detection (`PlatformCapabilities`)
- [x] Windows reactive file watcher
- [x] Linux reactive file watcher / on-access capability
- [x] macOS reactive file watcher

## Platforms

- [x] Windows (x64, ARM64)
- [x] Linux (x64, ARM64)
- [x] macOS (ARM64, x64)

## Testing & CI

- [x] Core unit tests
- [x] Platform unit tests
- [x] Output parser tests
- [x] Protocol framing tests
- [x] Architecture safety checks (no WPF / Windows leakage in Core)
- [x] GitHub Actions CI matrix (Windows, Ubuntu, macOS)
