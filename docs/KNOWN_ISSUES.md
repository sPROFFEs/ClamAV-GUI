# Known Issues & Architectural Blockers

This document tracks identified bugs, security risks, and technical debt prior to and during migration to Avalonia cross-platform architecture.

## P0 — Critical Security & Process Safety

1. **Unmanaged Daemon Killing (`ClamAVService.cs:301-314`)**
   - **Impact:** Any `clamd` process on the system is killed if managed PID is null.
   - **Resolution:** Implement strict `DaemonOwnership` tracking. Never kill unowned/external daemons.

2. **Unrestricted TCP Binding (`ClamAVService.cs:349`)**
   - **Impact:** ClamAV daemon listens on all interfaces on port 3310 without authentication.
   - **Resolution:** Explicitly bind local TCP daemon to `127.0.0.1` (`TCPAddr 127.0.0.1`). On Linux/macOS, use Unix domain sockets.

3. **External Configuration Overwrite (`ClamAVService.cs:73-108, 335-358`)**
   - **Impact:** Replaces system or user-managed `clamd.conf` and `freshclam.conf`.
   - **Resolution:** Classify configuration ownership (`External` vs `ManagedByApplication`). Never overwrite external configs.

4. **Missing Exit Code Interpretation (`ClamAVService.cs:65, 517`)**
   - **Impact:** Exit code 1 (Virus found) or 2 (Error) is not distinguished cleanly from process success.
   - **Resolution:** Introduce typed `ScanVerdict` and `ScanExecutionResult` with explicit exit code mapping.

## P1 — Portability & Correctness

1. **Sync-over-Async in Daemon Ping (`ClamAVService.cs:529-556`)**
   - **Impact:** Uses `Task.Run().GetAwaiter().GetResult()` which can deadlock the UI thread.
   - **Resolution:** End-to-end async health check and ping APIs.

2. **WPF and Windows API Coupling in ViewModels**
   - **Impact:** `MainViewModel` directly calls WPF dialogs, `RegistryKey`, and `Dispatcher`.
   - **Resolution:** Introduce `INotificationService`, `IFileDialogService`, `IStartupService`, and `ISchedulerService`.

3. **Hardcoded Windows Binary Names**
   - **Impact:** Expects `clamscan.exe`, `freshclam.exe`, `clamd.exe`.
   - **Resolution:** Implement platform-specific `IClamAvBinaryLocator` with support for PATH and standard Unix directories.

4. **Protocol Framing Fragility (`ClamAVService.cs:545, 565`)**
   - **Impact:** Uses `nCOMMAND\n` instead of `zCOMMAND\0` NUL-terminated framing.
   - **Resolution:** Implement `IClamdProtocol` with NUL-delimited commands.

## P2 — Maintainability & Diagnostics

1. **Monolithic Service Structure**
   - **Resolution:** Split into `IClamAvScanner`, `IClamAvUpdater`, `IClamAvDaemon`, `IClamAvBinaryLocator`, `IClamAvConfigurationProvider`.

2. **Tracked Build Artifacts**
   - **Impact:** Binaries and user files in git repo.
   - **Resolution:** Clean `bin/`, `obj/`, `*.user` and update `.gitignore`.
