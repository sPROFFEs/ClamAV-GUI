# ClamAV GUI — WPF to Avalonia Cross-Platform Migration Guide

## 1. Objective

Migrate the existing **ClamAV GUI** application from its current Windows-only WPF implementation to a **cross-platform Avalonia UI application**, while preserving as much of the existing C#/.NET codebase as possible.

Target platforms:

- Windows x64
- Windows ARM64 where practical
- Linux x64
- Linux ARM64
- macOS Apple Silicon / ARM64
- macOS x64 as an optional secondary target

The migration must prioritize:

1. Security and correctness.
2. Functional parity with the current application.
3. Separation between platform-independent and OS-specific behavior.
4. Reuse of existing C# code.
5. Testability.
6. Maintainability.
7. Cross-platform packaging.
8. A stable foundation for future development.

This is a controlled refactor and migration.

It is **not** a full rewrite.

---

# 2. Core Migration Principle

The project currently mixes:

```text
UI concerns
Windows integration
ClamAV discovery
ClamAV process management
clamd lifecycle
configuration generation
scanning
updating
quarantine
scheduler behavior
```

These concerns must be separated before the application becomes genuinely cross-platform.

The target direction is:

```text
Existing WPF application
        |
        v
Audit current behavior
        |
        v
Fix critical correctness/security issues
        |
        v
Extract platform-neutral Core
        |
        v
Introduce platform abstractions
        |
        v
Move Windows behavior behind adapters
        |
        v
Make WPF use the new Core temporarily
        |
        v
Create Avalonia frontend
        |
        v
Port functional workflows
        |
        v
Implement Linux/macOS adapters
        |
        v
Cross-platform validation
        |
        v
Packaging and release hardening
```

Do not start with a visual redesign.

Do not start by recreating every WPF screen.

The backend architecture must be corrected first.

---

# 3. Current Known Bugs and Design Problems

The following issues were identified during review of the current codebase.

They must be tracked explicitly during migration.

Priority levels:

```text
P0 = security / data-loss / external-system risk
P1 = correctness / architectural blocker
P2 = reliability / maintainability
P3 = product / UX / long-term improvement
```

---

## 3.1 P0 — clamd Can Be Stopped or Killed Without Ownership Verification

### Current problem

The existing daemon shutdown logic can:

1. send `SHUTDOWN` to the daemon available on the configured TCP endpoint;
2. lose the tracked managed PID;
3. enumerate processes named `clamd`;
4. kill matching processes.

This means the GUI may interfere with:

- a system ClamAV service;
- a daemon started manually by the user;
- an enterprise-managed daemon;
- another application's ClamAV instance.

The application must never kill a daemon merely because its process name is `clamd`.

### Required fix

Introduce explicit ownership:

```csharp
public enum DaemonOwnership
{
    ExternalSystemService,
    ManagedByApplication,
    Remote
}
```

Represent daemon state:

```csharp
public sealed record ClamAvDaemonInstance
{
    public required DaemonOwnership Ownership { get; init; }

    public int? ProcessId { get; init; }

    public required ClamdEndpoint Endpoint { get; init; }

    public string? ConfigPath { get; init; }
}
```

Rules:

```text
ManagedByApplication
    -> application may stop only the exact process it created

ExternalSystemService
    -> application may connect
    -> application must not issue SHUTDOWN by default
    -> application must never Process.Kill()

Remote
    -> application must never attempt local process management
```

PID ownership must be established when starting the process.

If ownership cannot be established:

```text
DO NOT KILL THE PROCESS
```

---

# 3.2 P0 — Unsafe clamd TCP Configuration

The current generated configuration enables:

```text
TCPSocket 3310
```

without explicitly restricting the bind address.

A managed `clamd` instance must not accidentally become reachable from untrusted interfaces.

ClamAV's TCP protocol has no built-in authentication or encryption.

### Required design

For a locally managed Windows daemon, explicitly bind to loopback.

Example conceptually:

```text
TCPSocket 3310
TCPAddr 127.0.0.1
```

For Unix:

```text
prefer LocalSocket
```

instead of TCP whenever possible.

Recommended priority:

```text
Linux/macOS local daemon:
    Unix domain socket

Windows local daemon:
    localhost TCP endpoint

Remote daemon:
    explicit user configuration only
```

Remote TCP support must never be automatically enabled.

---

# 3.3 P0 — Destructive clamd.conf Rewriting

### Current problem

Starting the daemon currently regenerates `clamd.conf`.

This is dangerous for installations managed by:

```text
apt
dnf
pacman
brew
system administrators
enterprise tooling
custom scripts
```

The GUI must not assume ownership of an existing ClamAV configuration.

### Required fix

Introduce configuration ownership:

```csharp
public enum ConfigurationOwnership
{
    External,
    ManagedByApplication
}
```

Rules:

```text
External configuration:
    read
    validate
    never rewrite automatically

Application-managed configuration:
    create in application-controlled storage
    update atomically
    retain backup
```

Never replace an external `clamd.conf` wholesale.

If a configuration must be edited:

```text
1. parse it
2. validate ownership
3. create backup
4. modify only the required directive
5. write atomically
6. validate result
```

---

# 3.4 P0 — Process Exit Codes Are Not Properly Interpreted

The current execution logic disables command result validation and does not consistently use process exit codes.

This is incorrect for antivirus software.

A scan has at least three semantically different states:

```text
clean
infected
execution error
```

These must never be collapsed into a generic "process completed" state.

### Required result model

```csharp
public enum ScanVerdict
{
    Clean,
    Infected,
    Error,
    Cancelled
}
```

```csharp
public sealed record ScanExecutionResult
{
    public required ScanVerdict Verdict { get; init; }

    public required int ExitCode { get; init; }

    public IReadOnlyList<ThreatDetection> Detections { get; init; }
        = Array.Empty<ThreatDetection>();

    public IReadOnlyList<string> Errors { get; init; }
        = Array.Empty<string>();

    public ScanStatistics? Statistics { get; init; }

    public required ScanBackendKind Backend { get; init; }
}
```

For `clamscan` and `clamdscan`, handle the documented semantics:

```text
0 -> no virus found
1 -> virus found
2 -> error
```

Do not interpret exit code `1` as a generic failure.

`freshclam` exit codes must also be explicitly mapped.

---

# 3.5 P1 — freshclam Does Not Consistently Support Cancellation

All long-running processes must receive a `CancellationToken`.

Required:

```csharp
Task<UpdateResult> UpdateDefinitionsAsync(
    CancellationToken cancellationToken);
```

Cancellation must propagate to the underlying process.

If cancellation occurs:

```text
terminate child process safely
collect available diagnostics
return Cancelled
```

Do not leave orphaned update processes.

---

# 3.6 P1 — clamd Health Detection Is Too Weak

A successful TCP connection does not prove that:

```text
the endpoint is actually clamd
the protocol works
the expected daemon is responding
the database is loaded
the daemon version is compatible
```

Similarly:

```text
Process.GetProcessesByName("clamd").Any()
```

does not prove the configured daemon is healthy.

### Introduce explicit health state

```csharp
public sealed record ClamdHealth
{
    public bool ProcessExists { get; init; }

    public bool EndpointReachable { get; init; }

    public bool ProtocolHealthy { get; init; }

    public bool DatabaseLoaded { get; init; }

    public bool OwnedByApplication { get; init; }

    public string? Version { get; init; }

    public string? Error { get; init; }
}
```

Health flow:

```text
resolve endpoint
      |
      v
connect
      |
      v
send PING
      |
      v
validate PONG
      |
      v
request VERSION
      |
      v
report health
```

Do not equate process existence with daemon readiness.

---

# 3.7 P1 — clamd Protocol Framing Is Fragile

The current custom clamd protocol implementation uses newline framing.

File paths on Unix may contain newline characters.

Use ClamAV's NUL-terminated framing for new clients:

```text
zCOMMAND\0
```

rather than:

```text
nCOMMAND\n
```

Create a protocol encoder.

Example:

```csharp
public interface IClamdProtocol
{
    Task<ClamdResponse> SendAsync(
        ClamdCommand command,
        CancellationToken cancellationToken);
}
```

Do not build protocol messages by arbitrary string concatenation.

---

# 3.8 P1 — Remote clamd Cannot Safely Scan Arbitrary Local Paths

A path-based command sent to a remote daemon refers to the daemon host's filesystem.

Therefore:

```text
GUI machine path
!=
remote clamd machine path
```

For a remote daemon, use streaming when applicable.

Design backends accordingly:

```text
Local Unix clamd:
    Unix socket
    path commands / FILDES where appropriate

Local Windows clamd:
    local TCP
    local path commands

Remote clamd:
    INSTREAM
```

Do not silently send local paths to remote daemons.

---

# 3.9 P1 — ClamAV Installation Detection Assumes Windows Layout

Current code assumes executables such as:

```text
clamscan.exe
freshclam.exe
clamd.exe
```

are located under one root directory.

This is not portable.

ClamAV installations can have separate:

```text
binary directory
configuration directory
database directory
log directory
runtime/socket directory
```

### Required model

```csharp
public sealed record ClamAvInstallation
{
    public string? RootDirectory { get; init; }

    public required string ClamScanPath { get; init; }

    public string? FreshClamPath { get; init; }

    public string? ClamdPath { get; init; }

    public string? ClamdScanPath { get; init; }

    public string? ClamOnAccPath { get; init; }

    public string? ClamConfPath { get; init; }

    public string? ConfigDirectory { get; init; }

    public string? DatabaseDirectory { get; init; }

    public string? LogDirectory { get; init; }

    public required string Version { get; init; }

    public required ClamAvInstallationSource Source { get; init; }
}
```

Do not require `freshclam` for ClamAV to be considered installed.

A user may have:

```text
clamscan only
clamd only
clamdscan + remote clamd
externally managed definitions
```

Detection must be capability-based.

---

# 3.10 P1 — Configuration and Database Directories Can Diverge

The current application can:

```text
copy/use one freshclam configuration
generate a separate clamd configuration
force a different DatabaseDirectory
```

This can result in:

```text
freshclam updates database A
clamd loads database B
```

### Required validation

Introduce:

```csharp
public sealed record ClamAvEffectiveConfiguration
{
    public string? FreshClamDatabaseDirectory { get; init; }

    public string? ClamdDatabaseDirectory { get; init; }

    public string? ClamScanDatabaseDirectory { get; init; }
}
```

At startup validate that the effective database locations are compatible.

If not:

```text
show configuration error
do not silently rewrite configs
```

---

# 3.11 P1 — Scan Options Behave Differently Between Backends

The direct `clamscan` path applies scan options such as heuristics and encrypted-file behavior.

The generated clamd configuration currently does not consistently apply the equivalent behavior.

That means the same UI settings can produce different scanning semantics depending on backend.

This must be fixed.

### Introduce ScanProfile

```csharp
public sealed record ScanProfile
{
    public bool Recursive { get; init; } = true;

    public bool HeuristicAlerts { get; init; }

    public bool ScanEncryptedArchives { get; init; }

    public bool DetectPua { get; init; }

    public bool ScanArchives { get; init; } = true;

    public bool ScanMail { get; init; } = true;

    public bool ScanPdf { get; init; } = true;

    public bool ScanOle2 { get; init; } = true;

    public bool ScanHtml { get; init; } = true;
}
```

Every backend must report supported capabilities:

```csharp
public sealed record ScanBackendCapabilities
{
    public bool SupportsHeuristics { get; init; }

    public bool SupportsEncryptedArchiveDetection { get; init; }

    public bool SupportsStreaming { get; init; }

    public bool SupportsPathScanning { get; init; }
}
```

Unsupported settings must not silently disappear.

---

# 3.12 P1 — clamd Log Directive Parsing Is Too Loose

Configuration parsing must never use prefix matching such as:

```text
StartsWith("LogFile")
```

because it can also match:

```text
LogFileMaxSize
LogFileUnlock
```

Create an actual directive parser.

Example:

```csharp
public sealed record ClamAvConfigDirective(
    string Name,
    string Value);
```

Comparison must use the complete directive name.

---

# 3.13 P1 — Sync-over-Async Daemon Checks

Avoid patterns equivalent to:

```csharp
Task.Run(...)
    .GetAwaiter()
    .GetResult();
```

Daemon health checks must be asynchronous end-to-end.

Use:

```csharp
Task<ClamdHealth> CheckHealthAsync(
    CancellationToken cancellationToken);
```

ViewModels must await them.

---

# 3.14 P2 — Daemon Process Output Handling Can Lose Diagnostics

When starting a long-lived daemon:

```text
redirecting stdout/stderr
then stopping asynchronous reads
while the process remains alive
```

is fragile.

Potential consequences include:

```text
lost diagnostics
pipe buffering
difficult troubleshooting
process lifecycle ambiguity
```

If the application owns the daemon:

```text
retain the Process handle
continuously drain stdout/stderr
log bounded diagnostic output
dispose it only after process exit
```

Better:

```csharp
public sealed class ManagedProcess : IAsyncDisposable
{
    public int ProcessId { get; }

    public Task<int> Completion { get; }
}
```

---

# 3.15 P2 — Generated Logs Are Too Verbose by Default

Do not force:

```text
LogVerbose yes
```

for production operation.

Verbose logging should be:

```text
off by default
enabled through diagnostics/debug settings
```

Avoid unnecessary path and scanning information in logs.

---

# 3.16 P2 — Install Directory Is Used for Mutable Data

Configuration, logs and databases must not be assumed writable under the ClamAV installation directory.

This is especially important on:

```text
Linux
macOS
managed Windows installations
```

Separate:

```text
installation files
configuration
databases
logs
application data
runtime files
```

---

# 3.17 P2 — Repository Contains Build/User Artifacts

Remove tracked development artifacts such as:

```text
bin/
obj/
*.csproj.user
```

Ensure `.gitignore` excludes them.

Do this early in the migration to avoid noisy commits.

---

# 3.18 P2 — Monolithic Static Service

The existing `ClamAVService` is large and mixes many responsibilities.

Do not migrate it as-is.

Break it into independently testable components.

---

# 3.19 P2 — Missing Automated Architecture Protection

Add tests that fail if Core accidentally gains platform dependencies.

Examples:

```text
Core must not reference Avalonia
Core must not reference WPF
Core must not reference System.Windows
Core must not contain Windows executable names
```

These may initially be CI grep checks and later architecture tests.

---

# 3.20 P3 — Current Software License Should Be Reconsidered

The repository currently uses a Creative Commons NonCommercial license.

Creative Commons licenses are generally not recommended for software, and a NonCommercial restriction prevents the project from being considered open source under the standard OSI definition.

Do not automatically change the license.

The maintainer must decide whether the desired model is for example:

```text
MIT
Apache-2.0
GPL-3.0
another software license
proprietary/non-commercial
dual licensing
```

Track this as a project decision before aggressively seeking contributors or distributors.

---

# 3.21 P3 — Release Signing and Supply-Chain Hardening

Production releases should eventually include:

```text
Windows code signing
macOS signing
macOS notarization
release checksums
SBOM
dependency vulnerability scanning
reproducible or documented builds
GitHub Actions release pipeline
```

Unsigned executables should not be the long-term production distribution strategy.

---

# 4. Required Pre-Migration Bug-Fix Order

Before serious UI migration work, address problems in this order.

## P0

```text
1. Add daemon ownership model
2. Prevent killing external clamd instances
3. Stop sending SHUTDOWN to external/unknown daemons
4. Bind managed TCP clamd to loopback explicitly
5. Prefer Unix sockets on Unix
6. Stop destructive rewriting of external clamd.conf
7. Correctly interpret scan/update exit codes
```

## P1

```text
8. Introduce separate binary/config/database paths
9. Make daemon operations fully asynchronous
10. Implement proper protocol framing
11. Introduce backend capabilities
12. Make scan options consistent across backends
13. Validate effective database configuration
14. Implement exact configuration parsing
15. Add cancellation to all external processes
```

## P2

```text
16. Split ClamAVService
17. Improve process lifecycle management
18. Add tests
19. Add CI
20. Clean repository artifacts
21. Improve diagnostics/logging
```

## P3

```text
22. Signing/notarization
23. License decision
24. Localization
25. Release supply-chain hardening
```

---

# 5. Git Strategy

Preserve the existing working Windows application.

Recommended branches:

```text
main
legacy/wpf-windows
migration/avalonia
```

Before migration:

```bash
git checkout main
git pull

git tag pre-avalonia-migration
git push origin pre-avalonia-migration

git checkout -b legacy/wpf-windows
git push -u origin legacy/wpf-windows

git checkout main
git checkout -b migration/avalonia
```

Do not delete the WPF frontend until Avalonia reaches functional parity.

---

# 6. Repository Cleanup

Before architectural changes:

1. remove tracked `bin`;
2. remove tracked `obj`;
3. remove `.csproj.user`;
4. verify `.gitignore`;
5. ensure generated packages are ignored;
6. keep only source-controlled assets.

Commit separately:

```text
chore: remove generated build and user artifacts
```

Do not mix cleanup with functional refactoring.

---

# 7. Current Architecture Audit

Before changing behavior, create:

```text
docs/ARCHITECTURE_CURRENT.md
```

Inventory:

```text
Models
Services
ViewModels
Views
WPF dependencies
Windows APIs
NuGet packages
ClamAV execution
ClamAV discovery
clamd lifecycle
freshclam lifecycle
configuration handling
database handling
quarantine
history
scheduler
tray
notifications
real-time monitoring
filesystem access
settings
logging
```

Classify files as:

```text
PORTABLE
UI-SPECIFIC
WINDOWS-SPECIFIC
MIXED
```

Example:

```text
ScanResult.cs              PORTABLE
ScanHistory.cs             PORTABLE
ClamAVService.cs           MIXED
MainViewModel.cs           MOSTLY PORTABLE
MainWindow.xaml            UI-SPECIFIC
SchedulerService.cs        WINDOWS-SPECIFIC
TrayIconService.cs         WINDOWS-SPECIFIC
```

Do not begin bulk UI conversion until this exists.

---

# 8. Target Solution Architecture

Recommended structure:

```text
ClamAVGui.sln

src/

    ClamAVGui.Core/
        Models/
        Interfaces/
        Services/
        Scanning/
        Parsing/
        Configuration/
        Quarantine/
        History/

    ClamAVGui.Platform/
        Interfaces/
        Models/

    ClamAVGui.Platform.Windows/
        Services/

    ClamAVGui.Platform.Linux/
        Services/

    ClamAVGui.Platform.MacOS/
        Services/

    ClamAVGui.App/
        Views/
        ViewModels/
        Controls/
        Converters/
        Assets/
        Services/

tests/

    ClamAVGui.Core.Tests/
    ClamAVGui.Platform.Tests/
    ClamAVGui.IntegrationTests/
```

Dependency direction:

```text
                         +-------------------+
                         | Avalonia App      |
                         +---------+---------+
                                   |
                    +--------------+--------------+
                    |                             |
                    v                             v
           +----------------+            +------------------+
           | Core           |            | Platform API     |
           +----------------+            +---------+--------+
                                                  |
                           +----------------------+----------------------+
                           |                      |                      |
                           v                      v                      v
                    Windows Adapter         Linux Adapter          macOS Adapter
```

Forbidden:

```text
Core -> Avalonia
Core -> WPF
Core -> Windows-specific APIs
Core -> Linux-specific APIs
Core -> macOS-specific APIs
```

---

# 9. Create ClamAVGui.Core

Target:

```xml
<TargetFramework>net8.0</TargetFramework>
```

Do not use:

```xml
net8.0-windows
```

Move portable models first.

Examples:

```text
ScanResult
ScanStatus
ScanVerdict
ScanRequest
ScanTarget
ScanProfile
ScanProgress
ThreatDetection
ScanStatistics
ScanHistoryEntry
QuarantineEntry
UpdateResult
ClamAvVersion
ClamAvConfiguration
ClamdHealth
```

Core may contain:

```text
scan orchestration
result parsing
history logic
quarantine metadata
validation
configuration models
domain errors
```

It must not know:

```text
which desktop environment is running
where Windows installs software
where Homebrew installs ClamAV
how Task Scheduler works
how launchd works
how systemd works
```

---

# 10. Break Apart ClamAVService

Replace the current monolithic service with focused interfaces.

## Scanner

```csharp
public interface IClamAvScanner
{
    Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

## Definition updater

```csharp
public interface IClamAvUpdater
{
    Task<UpdateResult> UpdateDefinitionsAsync(
        CancellationToken cancellationToken = default);
}
```

## Daemon

```csharp
public interface IClamAvDaemon
{
    Task<ClamdHealth> CheckHealthAsync(
        CancellationToken cancellationToken = default);

    Task StartAsync(
        CancellationToken cancellationToken = default);

    Task StopAsync(
        CancellationToken cancellationToken = default);
}
```

## Locator

```csharp
public interface IClamAvBinaryLocator
{
    Task<ClamAvInstallation?> FindInstallationAsync(
        CancellationToken cancellationToken = default);
}
```

## Configuration

```csharp
public interface IClamAvConfigurationProvider
{
    Task<ClamAvEffectiveConfiguration> LoadAsync(
        CancellationToken cancellationToken = default);
}
```

---

# 11. Introduce Scan Backends

Do not couple the scanner directly to one executable.

```csharp
public enum ScanBackendKind
{
    ClamScan,
    ClamD,
    ClamDScan
}
```

```csharp
public interface IScanBackend
{
    ScanBackendKind Kind { get; }

    ScanBackendCapabilities Capabilities { get; }

    Task<ScanExecutionResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
```

Selection policy:

```text
if healthy local clamd exists:
    prefer daemon backend

else:
    use clamscan

remote clamd:
    use streaming backend
```

`clamdscan` can be preferred where using the standard ClamAV client reduces the need for maintaining custom protocol code.

Only maintain a custom protocol implementation where it provides a clear advantage.

---

# 12. Process Execution Abstraction

Create:

```csharp
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record ProcessRequest
{
    public required string FileName { get; init; }

    public IReadOnlyList<string> Arguments { get; init; }
        = Array.Empty<string>();

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string>? EnvironmentVariables
        { get; init; }
}
```

```csharp
public sealed record ProcessResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public bool WasCancelled { get; init; }
}
```

Use structured `ArgumentList`.

Avoid:

```csharp
Arguments = $"--database=\"{path}\" \"{target}\"";
```

Prefer:

```csharp
startInfo.ArgumentList.Add("--database");
startInfo.ArgumentList.Add(databasePath);
startInfo.ArgumentList.Add(targetPath);
```

---

# 13. ClamAV Discovery

Discovery must be capability-based.

Search `$PATH` first where appropriate.

## Windows

Inspect:

```text
configured custom path
PATH
known installer locations
Program Files
registry when relevant
```

Executables:

```text
clamscan.exe
freshclam.exe
clamd.exe
clamdscan.exe
```

## Linux

Search:

```text
PATH
/usr/bin
/usr/sbin
/usr/local/bin
/usr/local/sbin
/opt/clamav/bin
/opt/clamav/sbin
```

Possible components:

```text
clamscan
freshclam
clamd
clamdscan
clamonacc
clamconf
```

Do not assume all distributions package them together.

## macOS

Search:

```text
PATH
/usr/local/clamav/bin
/usr/local/clamav/sbin
/opt/homebrew/bin
/opt/homebrew/sbin
/usr/local/bin
/usr/local/sbin
```

Do not hard-code Homebrew as the only installation mechanism.

---

# 14. Discover Effective Configuration

Do not infer configuration solely from installation paths.

Where practical:

```text
inspect command-line configuration
use clamconf
inspect known config locations
read explicit application settings
```

Possible locations vary by platform and installation method.

Represent the discovered configuration explicitly.

---

# 15. clamd Transport Abstraction

Create:

```csharp
public abstract record ClamdEndpoint;
```

```csharp
public sealed record TcpClamdEndpoint(
    string Host,
    int Port) : ClamdEndpoint;
```

```csharp
public sealed record UnixClamdEndpoint(
    string SocketPath) : ClamdEndpoint;
```

```csharp
public interface IClamdTransport
{
    Task<Stream> ConnectAsync(
        CancellationToken cancellationToken = default);
}
```

Implement:

```text
TcpClamdTransport
UnixSocketClamdTransport
```

Transport selection must not occur inside ViewModels.

---

# 16. Secure clamd Protocol Client

If custom protocol communication remains:

- use NUL framing;
- enforce timeouts;
- enforce maximum response sizes where applicable;
- support cancellation;
- validate responses;
- distinguish transport and protocol errors;
- use streaming for remote file scans.

Example commands:

```text
zPING\0
zVERSION\0
```

Do not blindly concatenate untrusted paths into protocol messages.

---

# 17. Daemon Ownership Model

This is mandatory.

Store managed daemon state for the process lifetime.

Example:

```csharp
public interface IManagedDaemonProcess : IAsyncDisposable
{
    int ProcessId { get; }

    Task<int> Completion { get; }

    Task StopAsync(
        CancellationToken cancellationToken = default);
}
```

Never stop a daemon merely because:

```text
its executable is called clamd
```

Before shutdown:

```text
assert Ownership == ManagedByApplication
assert PID matches managed process
assert process identity matches expected executable
```

---

# 18. Platform Abstraction Layer

At minimum provide:

```text
IPlatformService
IClamAvBinaryLocator
IFileSystemMonitor
IStartupService
ISchedulerService
INotificationService
ITrayService
IFileDialogService
IPrivilegeService
IPathService
IClamdTransportFactory
```

Example:

```csharp
public interface IPlatformService
{
    PlatformKind Platform { get; }

    Architecture Architecture { get; }

    string UserDataDirectory { get; }

    string UserCacheDirectory { get; }

    string RuntimeDirectory { get; }
}
```

---

# 19. Platform Data Paths

Respect native conventions.

## Windows

Use appropriate application-data directories such as:

```text
LocalApplicationData
```

## Linux

Respect XDG:

```text
XDG_CONFIG_HOME
XDG_DATA_HOME
XDG_CACHE_HOME
XDG_RUNTIME_DIR
```

with correct fallbacks.

## macOS

Use:

```text
~/Library/Application Support/
~/Library/Caches/
```

Do not place mutable application data next to installed binaries.

---

# 20. Create the Avalonia Application

Create:

```text
ClamAVGui.App
```

Target:

```xml
<TargetFramework>net8.0</TargetFramework>
```

Use:

```text
Avalonia
CommunityToolkit.Mvvm where useful
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging
```

Avoid unnecessary custom frameworks.

---

# 21. Dependency Injection

Register shared services centrally.

Conceptually:

```csharp
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<IClamAvScanner, ClamAvScanner>();
services.AddSingleton<IClamAvUpdater, ClamAvUpdater>();

if (OperatingSystem.IsWindows())
{
    services.AddSingleton<IClamAvBinaryLocator,
        WindowsClamAvBinaryLocator>();
}
else if (OperatingSystem.IsLinux())
{
    services.AddSingleton<IClamAvBinaryLocator,
        LinuxClamAvBinaryLocator>();
}
else if (OperatingSystem.IsMacOS())
{
    services.AddSingleton<IClamAvBinaryLocator,
        MacOsClamAvBinaryLocator>();
}
```

Platform detection belongs near composition/startup.

Do not scatter:

```csharp
OperatingSystem.IsWindows()
```

throughout Core and ViewModels.

---

# 22. Migrate ViewModels Before Views

Search existing ViewModels for:

```text
System.Windows
Application.Current
Dispatcher
MessageBox
Window
OpenFileDialog
FolderBrowserDialog
Clipboard
WPF controls
```

Remove those dependencies.

Bad:

```csharp
MessageBox.Show("Scan completed");
```

Good:

```csharp
await _notificationService.ShowAsync(
    "Scan completed",
    result.Message);
```

Bad:

```csharp
var dialog = new OpenFileDialog();
```

Good:

```csharp
var file = await _fileDialogService.SelectFileAsync();
```

---

# 23. UI Migration Order

Port functionality in this order:

```text
1. Application shell
2. Navigation
3. Diagnostics
4. Dashboard
5. Manual scan
6. Scan progress
7. Results
8. Definition updates
9. History
10. Quarantine
11. Settings
12. Daemon controls
13. Scheduler
14. Tray
15. Notifications
16. Real-time monitoring
```

Compile after every vertical slice.

Do not port all XAML before testing functionality.

---

# 24. Do Not Perform a Visual Redesign During Parity Work

WPF and Avalonia XAML are similar but not identical.

Port concepts, not syntax blindly.

Maintain approximately the existing UX until feature parity is reached.

After parity:

```text
UI redesign may begin
```

This keeps migration regressions distinguishable from design changes.

---

# 25. File and Directory Selection

Use Avalonia storage APIs behind an abstraction.

Support:

```text
single file
multiple files
directory
export/save file
```

Normalize scan targets:

```csharp
public sealed record ScanTarget
{
    public required string Path { get; init; }

    public required ScanTargetType Type { get; init; }
}
```

---

# 26. Scan Target Safety

Cross-platform scanning needs explicit policies.

Support:

```text
symlink policy
mount crossing policy
special files
permission failures
network filesystems
huge files
excluded system trees
```

On Linux, avoid blindly traversing pseudo-filesystems such as:

```text
/proc
/sys
/dev
/run
```

unless explicitly requested and safe.

Define:

```csharp
public enum SymbolicLinkPolicy
{
    DoNotFollow,
    FollowFiles,
    FollowAll
}
```

Default should avoid unexpected recursive traversal.

---

# 27. Scan Concurrency

Do not launch unlimited scanner processes.

Introduce a scan coordinator:

```csharp
public interface IScanCoordinator
{
    Task<ScanExecutionResult> QueueAsync(
        ScanRequest request,
        CancellationToken cancellationToken);
}
```

Use bounded concurrency.

Potential defaults:

```text
manual foreground scan:
    one active scan

background/reactive scans:
    small bounded worker pool
```

This avoids:

```text
CPU exhaustion
disk contention
duplicate scans
large process explosions
```

---

# 28. Definition Update Coordination

Prevent multiple concurrent `freshclam` runs from the GUI.

Use an async lock/semaphore.

Example:

```text
only one definition update at a time
```

After successful update:

```text
if managed clamd is running:
    request safe database reload

if external clamd:
    respect external configuration/service
```

Do not restart external services automatically.

---

# 29. System Tray

Implement tray support through Avalonia/platform APIs.

Expose:

```csharp
public interface ITrayService
{
    void Initialize();

    void SetStatus(TrayStatus status);

    void Show();

    void Hide();
}
```

Useful commands:

```text
Open
Quick Scan
Update Definitions
Show ClamAV Status
Exit
```

Validate separately on:

```text
Windows
GNOME
KDE
macOS
```

Linux tray support varies by desktop environment.

---

# 30. Notifications

Create:

```csharp
public interface INotificationService
{
    Task ShowAsync(
        string title,
        string message,
        NotificationSeverity severity =
            NotificationSeverity.Information);
}
```

A notification failure must not cause:

```text
scan failure
application crash
daemon failure
```

---

# 31. Scheduler

Create:

```csharp
public interface ISchedulerService
{
    Task<IReadOnlyList<ScheduledScan>> GetSchedulesAsync();

    Task CreateAsync(ScheduledScan schedule);

    Task UpdateAsync(ScheduledScan schedule);

    Task DeleteAsync(string scheduleId);
}
```

Adapters:

```text
Windows:
    Task Scheduler

Linux:
    systemd user timers where available
    isolated fallback if required

macOS:
    launchd / LaunchAgents
```

The Core model must remain platform-neutral.

---

# 32. Real-Time and On-Access Protection

Do not imply identical security guarantees on all operating systems.

Represent the actual implementation mode.

```csharp
public enum RealTimeProtectionMode
{
    Unsupported,
    ReactiveFileWatcher,
    NativeOnAccess
}
```

Potential behavior:

```text
Linux:
    clamonacc/fanotify where available
    native on-access

Windows:
    filesystem watcher + reactive scan

macOS:
    filesystem/event watcher + reactive scan
```

The UI must distinguish:

```text
Native on-access protection
```

from:

```text
Reactive real-time monitoring
```

Do not call a watcher "on-access prevention" when it only scans after a filesystem event.

---

# 33. Linux clamonacc Privilege Handling

Native Linux on-access scanning may require elevated capabilities/privileges.

The GUI must not silently run itself as root.

Preferred architecture:

```text
unprivileged GUI
        |
        v
existing/system-managed privileged ClamAV component
```

If privileged setup is needed:

```text
provide explicit setup workflow
explain required permissions
do not automatically escalate
```

Never run the entire Avalonia application as root merely to access ClamAV features.

---

# 34. Quarantine

Store quarantined files in application-controlled storage.

Conceptually:

```text
Windows:
%LOCALAPPDATA%/ClamAVGui/Quarantine

Linux:
$XDG_DATA_HOME/clamav-gui/quarantine

macOS:
~/Library/Application Support/ClamAVGui/Quarantine
```

Metadata should include:

```text
ID
original path
internal quarantine path
threat name
timestamp
SHA-256
original size
engine version
signature/database version
```

Use randomized internal names.

Example:

```text
quarantine/
    31c7a154-...
    31c7a154-....json
```

Do not retain executable file extensions unnecessarily.

---

# 35. Quarantine Atomicity

Quarantine operations must avoid partial states.

Workflow:

```text
calculate metadata/hash
      |
      v
move/copy into temporary quarantine file
      |
      v
fsync/validate as practical
      |
      v
write metadata atomically
      |
      v
finalize quarantine entry
```

If failure occurs:

```text
do not report successful quarantine
```

Restoration must:

```text
check destination conflicts
recalculate hash
require explicit confirmation where appropriate
handle permission failures
```

---

# 36. Settings

Version settings.

```csharp
public sealed class ApplicationSettings
{
    public int SchemaVersion { get; set; } = 1;

    public string? CustomClamAvPath { get; set; }

    public bool AutoUpdateDefinitions { get; set; }

    public bool MinimizeToTray { get; set; }

    public bool StartOnLogin { get; set; }
}
```

Use platform data directories.

Do not hard-code Windows paths.

Use atomic settings writes.

---

# 37. Logging

Use structured logging.

Categories:

```text
Application
Scanner
ClamD
ClamDProtocol
FreshClam
Configuration
Scheduler
Quarantine
Platform
RealTimeProtection
```

Startup diagnostics can include:

```text
OS
architecture
application version
.NET runtime
ClamAV version
component availability
database version
daemon transport
```

Do not log:

```text
file contents
unnecessary personal paths
tokens/secrets
full diagnostics without redaction
```

---

# 38. Diagnostic Export Redaction

A public diagnostics export should support redaction.

Potential sensitive information:

```text
username
home directory
network hostnames
custom paths
remote daemon host
scanned filenames
```

Implement:

```text
Copy diagnostics
Copy diagnostics with paths
```

with the safer redacted option as default.

---

# 39. Domain Error Model

Do not expose arbitrary exceptions directly.

Example error codes:

```text
ClamAvNotFound
ClamAvExecutionFailed
DatabaseMissing
DatabaseMismatch
DatabaseUpdateFailed
DaemonUnavailable
DaemonProtocolError
DaemonOwnershipViolation
PermissionDenied
InvalidConfiguration
ConfigurationReadOnly
ScanCancelled
UnsupportedFeature
QuarantineFailed
```

Use typed exceptions/results.

---

# 40. Cancellation and Timeouts

Every potentially blocking external operation must support cancellation and timeout.

Includes:

```text
scan
freshclam
daemon connection
daemon PING
version query
binary discovery
large quarantine operation
scheduler calls
```

Use different timeouts for:

```text
connection
protocol command
process startup
graceful shutdown
```

Do not use one arbitrary global timeout.

---

# 41. Parsing ClamAV Output

Keep parsing separate from execution.

```csharp
public interface IClamAvOutputParser
{
    ScanExecutionResult Parse(
        string stdout,
        string stderr,
        int exitCode,
        ScanBackendKind backend);
}
```

Test against captured output from:

```text
clean scan
infected scan
permission failure
missing database
malformed target
cancelled scan
Windows output
Linux output
macOS output
```

---

# 42. Do Not Parse Localized Human Text More Than Necessary

Prefer:

```text
exit codes
structured protocol responses
stable summary fields
```

over assumptions based entirely on English text.

Use domain error codes internally.

Localization belongs in the UI layer.

---

# 43. Tests

Before full UI migration, add tests for:

```text
exit-code mapping
ClamAV output parsing
configuration directive parsing
daemon ownership
daemon endpoint parsing
protocol framing
scan command generation
update command generation
settings serialization
quarantine metadata
hash verification
version parsing
capability detection
```

---

# 44. EICAR Integration Tests

Use the standard EICAR test file for antivirus integration tests rather than real malware.

Integration tests should verify:

```text
clean file -> Clean
EICAR -> Infected
missing file -> Error
cancelled scan -> Cancelled
```

Do not commit dangerous malware samples to the repository.

---

# 45. Platform Capability Model

Create:

```csharp
public sealed record PlatformCapabilities
{
    public bool SupportsClamScan { get; init; }

    public bool SupportsClamD { get; init; }

    public bool SupportsFreshClam { get; init; }

    public bool SupportsNativeOnAccess { get; init; }

    public bool SupportsReactiveMonitoring { get; init; }

    public bool SupportsScheduling { get; init; }

    public bool SupportsNotifications { get; init; }

    public bool SupportsTray { get; init; }

    public bool SupportsStartupRegistration { get; init; }
}
```

UI controls must depend on capabilities, not only OS names.

---

# 46. Startup Detection Flow

Recommended flow:

```text
Application starts
      |
      v
Detect OS + architecture
      |
      v
Discover ClamAV components
      |
      v
Read effective configuration
      |
      v
Validate database locations
      |
      v
Detect clamd endpoint
      |
      v
Determine ownership
      |
      v
PING/VERSION daemon if available
      |
      v
Build capability model
      |
      v
Load application UI
```

If detection fails:

```text
open application normally
show setup/diagnostic state
```

Never crash because ClamAV is missing.

---

# 47. Diagnostics Page

Expose:

```text
Application version
OS
architecture
.NET runtime

clamscan:
    path
    version

freshclam:
    availability
    path

clamd:
    availability
    endpoint
    transport
    ownership
    process ID when managed
    protocol status
    version

clamdscan:
    availability
    path

clamonacc:
    availability
    path

configuration:
    source
    ownership
    directory

database:
    directory
    version/status

scheduler backend
real-time backend
quarantine directory
```

Also:

```text
Run health check
Copy redacted diagnostics
```

---

# 48. First Avalonia Milestone

The first milestone should include only:

```text
application launches
ClamAV detection
diagnostics
file picker
directory picker
manual scan
progress
cancellation
result interpretation
definition update
settings
```

Test on:

```text
Windows
Linux
```

before migrating secondary functionality.

---

# 49. Second Milestone

Add:

```text
history
quarantine
restore
daemon backend
tray
notifications
macOS support
```

Test on:

```text
Windows x64
Linux x64
macOS ARM64
```

---

# 50. Third Milestone

Add:

```text
scheduler
startup integration
reactive monitoring
Linux native on-access integration
advanced configuration
packaging
release pipeline
```

---

# 51. Continuous Integration

Create GitHub Actions early.

Every pull request:

```text
windows-latest
ubuntu-latest
macos-latest
```

Run:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Publishing targets:

```text
win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

A cross-compiled artifact is not considered fully validated until tested on the corresponding native architecture.

---

# 52. Architecture CI Checks

Add simple CI checks during migration.

Example:

```bash
if rg 'System\.Windows' src/ClamAVGui.Core; then
    echo "WPF dependency found in Core"
    exit 1
fi
```

```bash
if rg '\.exe' src/ClamAVGui.Core; then
    echo "Windows executable assumption found in Core"
    exit 1
fi
```

```bash
if rg 'OperatingSystem\.' src/ClamAVGui.Core; then
    echo "OS-specific logic found in Core"
    exit 1
fi
```

These can later become proper architecture tests.

---

# 53. Publishing

Start with:

```text
self-contained
not trimmed
not Native AOT
```

Do not introduce aggressive publishing optimizations during migration.

Example:

```bash
dotnet publish src/ClamAVGui.App \
    -c Release \
    -r win-x64 \
    --self-contained true
```

Validate reflection/resource behavior before enabling:

```text
PublishTrimmed
NativeAOT
single-file optimizations
```

---

# 54. Packaging

## Windows

Target:

```text
signed MSI
or
signed installer EXE
```

Optional:

```text
portable ZIP
```

## Linux

Initial:

```text
AppImage
tar.gz
```

Later:

```text
.deb
.rpm
```

## macOS

Target:

```text
.app
DMG
code signing
notarization
```

---

# 55. Do Not Bundle ClamAV Initially

Initial releases should prefer a detected system/user ClamAV installation.

Bundling ClamAV immediately adds:

```text
native runtime maintenance
security update responsibility
architecture variants
license/distribution review
definition management
larger installers
signing complexity
```

Represent source explicitly:

```csharp
public enum ClamAvInstallationSource
{
    System,
    Managed,
    Bundled,
    Custom
}
```

This permits a managed/bundled mode later.

---

# 56. User Data Migration

On Windows, attempt to detect old WPF application data.

Possible migration targets:

```text
settings
scan history
quarantine metadata
scheduled scans
```

Use versioned migration:

```text
SettingsSchemaVersion
DatabaseSchemaVersion
```

Do not blindly move existing quarantined files.

Validate:

```text
metadata
existence
hash
destination
```

---

# 57. Feature Migration Checklist

Create:

```text
docs/MIGRATION_CHECKLIST.md
```

Suggested content:

```markdown
# Core

- [ ] ClamAV component discovery
- [ ] Effective configuration discovery
- [ ] clamscan backend
- [ ] clamd backend
- [ ] clamdscan backend
- [ ] Unix socket transport
- [ ] TCP transport
- [ ] NUL protocol framing
- [ ] Exit-code mapping
- [ ] Manual file scan
- [ ] Directory scan
- [ ] Cancellation
- [ ] Definition update
- [ ] Database validation

# Security / correctness

- [ ] Daemon ownership
- [ ] No external clamd termination
- [ ] Loopback-only managed TCP clamd
- [ ] No destructive external config writes
- [ ] Atomic settings/config writes
- [ ] Remote clamd streaming
- [ ] Protocol timeouts

# User data

- [ ] Settings
- [ ] History
- [ ] Quarantine
- [ ] Restore
- [ ] Delete

# Desktop

- [ ] Tray
- [ ] Notifications
- [ ] Start on login
- [ ] Scheduler
- [ ] Diagnostics

# Real-time

- [ ] Capability detection
- [ ] Windows reactive watcher
- [ ] Linux clamonacc integration
- [ ] Linux watcher fallback
- [ ] macOS reactive watcher

# Windows

- [ ] x64
- [ ] ARM64

# Linux

- [ ] x64
- [ ] ARM64

# macOS

- [ ] ARM64
- [ ] x64

# Release

- [ ] CI
- [ ] Checksums
- [ ] Windows signing
- [ ] macOS signing
- [ ] macOS notarization
- [ ] SBOM
```

---

# 58. Definition of Done

A migrated feature is complete only when:

```text
1. Portable behavior is in Core.
2. OS-specific behavior is behind an interface.
3. Errors have typed semantics.
4. Cancellation is implemented.
5. Timeouts are implemented where needed.
6. Logging exists.
7. Tests exist where practical.
8. No WPF dependency exists.
9. Windows is validated.
10. Linux is validated where applicable.
11. macOS is validated where applicable.
12. Security behavior is equivalent or explicitly documented.
```

---

# 59. Coding Rules for the Agent

## Rule 1

Inspect existing implementation before changing it.

## Rule 2

Do not rewrite portable working C# without a concrete reason.

## Rule 3

Do not put platform checks in Core.

Forbidden:

```csharp
OperatingSystem.IsWindows()
```

inside `ClamAVGui.Core`.

## Rule 4

Do not reference Avalonia from Core.

## Rule 5

Do not introduce WPF dependencies into new projects.

## Rule 6

Do not silently modify external ClamAV configuration.

## Rule 7

Do not automatically elevate to administrator/root.

## Rule 8

Do not assume all ClamAV components live in one directory.

## Rule 9

Do not assume `freshclam` exists.

## Rule 10

Do not assume TCP port 3310.

## Rule 11

Do not assume a running `clamd` process belongs to this application.

## Rule 12

Do not use process name as proof of ownership.

## Rule 13

Do not expose a managed ClamAV daemon to external interfaces by default.

## Rule 14

Do not ignore process exit codes.

## Rule 15

Do not silently ignore unsupported scan options.

## Rule 16

Do not construct shell commands unnecessarily.

## Rule 17

Do not use synchronous waits around asynchronous I/O.

## Rule 18

Do not introduce major new features until parity is reached.

## Rule 19

Keep commits small and independently understandable.

## Rule 20

Leave the repository buildable after each major refactor whenever practical.

---

# 60. Recommended Commit Sequence

```text
chore: remove generated repository artifacts

docs: document current architecture and migration risks

fix: prevent shutdown of unmanaged clamd instances

fix: restrict managed clamd TCP endpoint to loopback

fix: preserve external ClamAV configuration

fix: map ClamAV process exit codes

refactor: create platform-neutral core project

refactor: extract ClamAV output parser

refactor: introduce process runner

refactor: introduce ClamAV installation discovery

refactor: split scanner and updater services

refactor: introduce daemon ownership model

refactor: introduce clamd endpoint abstraction

refactor: introduce clamd protocol client

test: add scanner and protocol unit tests

feat: add Windows platform adapter

refactor: migrate WPF application to new core services

feat: create Avalonia application shell

feat: add diagnostics page

feat: migrate manual scan workflow

feat: migrate signature update workflow

feat: migrate history

feat: migrate quarantine

feat: add Linux platform adapter

feat: add macOS platform adapter

feat: add Unix clamd socket support

feat: add cross-platform tray

feat: add notification abstraction

feat: add scheduler adapters

feat: add real-time capability model

ci: add cross-platform build matrix

build: add Windows package

build: add Linux package

build: add macOS package

build: add release checksums and SBOM
```

---

# 61. Validation After Every Major Phase

Run:

```bash
dotnet clean
dotnet restore
dotnet build
dotnet test
```

Check for WPF leakage:

```bash
rg 'System\.Windows' src
```

Check Core for executable assumptions:

```bash
rg '\.exe' src/ClamAVGui.Core
```

Expected:

```text
no platform executable assumptions
```

Check for Windows paths:

```bash
rg 'C:\\\\' src/ClamAVGui.Core
```

Check Core OS branching:

```bash
rg 'OperatingSystem\.' src/ClamAVGui.Core
```

Expected:

```text
no results
```

Check unsafe daemon logic:

```bash
rg 'GetProcessesByName' src
```

Any remaining usage must be reviewed carefully.

Check hard-coded daemon port:

```bash
rg '3310' src
```

Any remaining occurrence must belong to defaults/configuration, not hidden connection logic.

---

# 62. Cross-Platform Acceptance Matrix

| Feature | Windows | Linux | macOS ARM64 |
|---|---|---|---|
| Launch | Required | Required | Required |
| Detect ClamAV | Required | Required | Required |
| Manual file scan | Required | Required | Required |
| Directory scan | Required | Required | Required |
| Correct exit-code semantics | Required | Required | Required |
| Cancellation | Required | Required | Required |
| Definition update | Required | Required | Required |
| History | Required | Required | Required |
| Quarantine | Required | Required | Required |
| Restore | Required | Required | Required |
| clamd | Required | Required | Required |
| External daemon safety | Required | Required | Required |
| Diagnostics | Required | Required | Required |
| Tray | Required | Required | Required |
| Notifications | Required | Required | Required |
| Scheduler | Required | Required | Required |
| Packaging | Required | Required | Required |
| Native on-access | N/A | Conditional | N/A |
| Reactive monitoring | Required | Optional fallback | Required |

---

# 63. Exact Initial Execution Plan for the Coding Agent

## Step 1 — Establish Baseline

Build the existing WPF application without modifications.

Record:

```text
build result
warnings
runtime errors
known failing features
```

---

## Step 2 — Create Architecture Documentation

Create:

```text
docs/ARCHITECTURE_CURRENT.md
docs/MIGRATION_CHECKLIST.md
docs/KNOWN_ISSUES.md
```

Populate them from actual code inspection.

---

## Step 3 — Fix P0 Daemon Ownership

Before changing UI architecture:

```text
identify managed daemon
retain exact process identity
remove kill-by-process-name fallback
prevent SHUTDOWN against external daemon
```

Add tests where practical.

---

## Step 4 — Fix Managed Daemon Networking

If an application-owned Windows daemon uses TCP:

```text
bind explicitly to localhost
```

On Unix plan for:

```text
LocalSocket
```

Do not expose an unauthenticated ClamAV daemon.

---

## Step 5 — Stop Rewriting External Configuration

Separate:

```text
external config
managed config
```

External configuration becomes read-only from the application's normal operation.

---

## Step 6 — Fix Process Result Semantics

Introduce:

```text
ProcessResult
ScanVerdict
UpdateResult
```

Correctly interpret exit codes.

Propagate cancellation.

---

## Step 7 — Clean Repository

Remove:

```text
bin
obj
*.csproj.user
```

Commit separately.

---

## Step 8 — Create Core

Create:

```text
ClamAVGui.Core
```

Move portable domain models first.

Build.

---

## Step 9 — Extract Output Parsing

Move ClamAV result parsing into Core.

Add unit tests for:

```text
clean
infected
error
```

Build.

---

## Step 10 — Introduce Process Runner

Replace direct process invocation incrementally.

Use structured argument lists.

Build.

---

## Step 11 — Extract Installation Discovery

Introduce:

```text
IClamAvBinaryLocator
ClamAvInstallation
```

Windows implementation first.

Build.

---

## Step 12 — Extract Scanner

Introduce:

```text
IClamAvScanner
IScanBackend
```

Move direct clamscan behavior.

Build and test.

---

## Step 13 — Extract Updater

Introduce:

```text
IClamAvUpdater
```

Correct freshclam:

```text
exit codes
cancellation
locking
logs
```

Build and test.

---

## Step 14 — Extract Daemon Communication

Introduce:

```text
ClamdEndpoint
IClamdTransport
IClamdProtocol
IClamAvDaemon
```

Implement:

```text
PING
VERSION
NUL framing
timeouts
cancellation
ownership
```

---

## Step 15 — Make Existing WPF Use the New Core

This is mandatory.

Do not create Avalonia until the existing Windows frontend can use the extracted services successfully.

Verify:

```text
scan
updates
daemon
history
quarantine
```

This proves the backend refactor independently of the UI migration.

---

## Step 16 — Add Tests

Add tests around the newly extracted Core.

The goal is to reduce the chance that UI migration hides backend regressions.

---

## Step 17 — Create Avalonia Application

Initially implement:

```text
startup
dependency injection
main window
navigation
diagnostics
```

No full redesign.

---

## Step 18 — Port Manual Scan as the First Vertical Slice

Required flow:

```text
Avalonia View
      |
      v
ViewModel
      |
      v
Scan Coordinator
      |
      v
IScanBackend
      |
      v
ClamAV
      |
      v
Parser/result mapping
      |
      v
ViewModel
      |
      v
UI result
```

Verify:

```text
clean
infected
error
cancel
```

before moving on.

---

## Step 19 — Port Definition Updates

Verify:

```text
already up to date
successful update
network failure
permission failure
cancel
```

---

## Step 20 — Port History and Quarantine

Include atomicity and hash validation.

---

## Step 21 — Implement Linux Adapter

Add:

```text
binary discovery
config discovery
Unix socket
paths
scheduler
notifications
```

Do not assume Ubuntu.

---

## Step 22 — Implement macOS Adapter

Test natively on Apple Silicon.

Support:

```text
official ClamAV package
Homebrew where detected
custom PATH
```

---

## Step 23 — Add Platform Real-Time Backends

Implement the capability model.

Do not advertise identical prevention semantics.

---

## Step 24 — Add CI

Build and test:

```text
Windows
Linux
macOS
```

---

## Step 25 — Add Packaging

Start simple.

Then add signing/notarization.

---

## Step 26 — Compare Against Legacy WPF

Perform an explicit feature-by-feature parity review.

Do not infer parity from screenshots.

---

## Step 27 — Promote Avalonia

Once the acceptance matrix passes:

```text
Avalonia -> main
WPF -> legacy/wpf-windows
```

Do not immediately delete legacy code.

---

# 64. Things the Agent Must Not Do

Do not:

```text
rewrite the entire application from scratch
switch away from C#
introduce Rust
introduce Electron
introduce a browser frontend
copy the monolithic ClamAVService into the new app
redesign every screen while migrating
bundle ClamAV during the first phase
hard-code .exe names into Core
hard-code port 3310 throughout the application
assume TCP is the only clamd transport
expose clamd to external interfaces by default
kill processes based only on their name
send SHUTDOWN to an unowned daemon
overwrite system clamd.conf
require freshclam for basic scanning
assume config/database/bin directories are identical
ignore process exit codes
ignore unsupported scan options
run the GUI as root
silently elevate privileges
assume Linux means Ubuntu
assume macOS means Homebrew
assume macOS means Intel
assume all filesystem paths are simple printable strings
follow symlinks recursively without policy
launch unlimited concurrent scanner processes
enable verbose logging permanently
```

---

# 65. Security Invariants

The following invariants must hold after the migration.

## Daemon

```text
The application never kills an unowned daemon.

The application never sends shutdown commands to an external daemon by default.

A managed TCP daemon listens only on trusted local interfaces.

Unix local daemons prefer Unix sockets.

Remote daemon usage is explicit.
```

## Configuration

```text
External ClamAV configuration is never silently overwritten.

Application-managed configuration lives in an application-controlled path.

Configuration changes are atomic.
```

## Scanning

```text
Clean, infected and failed scans are distinguishable.

Cancellation is not reported as success.

Unsupported options are not silently ignored.
```

## Privileges

```text
The entire GUI is never elevated merely to simplify implementation.

Privileged functionality is isolated.
```

## Quarantine

```text
Quarantined objects are tracked with integrity metadata.

Restore operations verify destination and integrity.
```

---

# 66. Recommended Product Improvements After Parity

Do these only after the architecture is stable.

## ClamAV Setup Assistant

Show:

```text
ClamAV detected
components available
database status
daemon status
recommended actions
```

Do not force installation ownership.

---

## Scan Profiles

Examples:

```text
Quick Scan
Full User Scan
Custom Scan
High-Sensitivity Scan
```

Profiles must map to explicit `ScanProfile` objects.

---

## Better Result Details

Display:

```text
verdict
backend
duration
files scanned
data scanned
threats
errors
database version
engine version
```

---

## Configuration Health

Detect:

```text
database mismatch
stale definitions
unreachable daemon
unsafe daemon endpoint
missing executable
read-only config
```

---

## Exportable Reports

Support:

```text
JSON
CSV
plain text
```

Prefer structured internal records over parsing UI text.

---

## Localization

Use resource files.

Do not use displayed strings as domain state.

---

## Accessibility

Validate:

```text
keyboard navigation
screen readers
contrast
scalable text
focus state
```

on all platforms.

---

# 67. Release Engineering Improvements

Before calling the project production-ready:

```text
CI on every PR
unit tests
integration tests
dependency update automation
dependency vulnerability scanning
signed release artifacts
checksums
SBOM
macOS notarization
documented build process
versioned changelog
```

Release artifacts should include their target architecture clearly.

Example:

```text
ClamAV-GUI-2.0.0-win-x64
ClamAV-GUI-2.0.0-linux-x64
ClamAV-GUI-2.0.0-linux-arm64
ClamAV-GUI-2.0.0-macos-arm64
```

---

# 68. Final Target Architecture

```text
                    +------------------------+
                    |      Avalonia UI       |
                    +-----------+------------+
                                |
                                v
                    +------------------------+
                    |      ViewModels        |
                    +-----------+------------+
                                |
                                v
                    +------------------------+
                    | Application Services   |
                    +-----------+------------+
                                |
                                v
                    +------------------------+
                    |     ClamAV Core        |
                    +-----------+------------+
                                |
                 +--------------+---------------+
                 |              |               |
                 v              v               v
          +-------------+ +-------------+ +-------------+
          |   Windows   | |    Linux    | |    macOS    |
          |   Adapter   | |   Adapter   | |   Adapter   |
          +------+------+ +------+------+ +------+------+
                 |              |               |
                 +--------------+---------------+
                                |
                                v
                  +---------------------------+
                  |          ClamAV           |
                  |                           |
                  | clamscan                  |
                  | clamdscan                 |
                  | clamd                     |
                  | freshclam                 |
                  | clamonacc where available |
                  +---------------------------+
```

Responsibilities:

```text
UI:
    presentation only

ViewModels:
    application state and commands

Core:
    domain behavior

Platform adapters:
    OS integration

ClamAV backends:
    antivirus execution/protocol
```

---

# 69. Final Success Criteria

The migration is successful when one C# codebase provides:

```text
one Avalonia UI
one platform-neutral Core
one shared domain model

Windows adapter
Linux adapter
macOS adapter
```

and produces reliable applications for:

```text
Windows
Linux
Apple Silicon macOS
```

without duplicating scan logic.

Additionally:

```text
external clamd instances are protected from accidental termination

managed daemons are not exposed insecurely

system ClamAV configuration is not destructively overwritten

scan/update process results have correct semantics

the same user settings have predictable behavior across backends

cross-platform builds are continuously tested
```

At that point:

```text
legacy/wpf-windows
```

should receive only critical fixes.

Future product development should target the Avalonia architecture.

---

# 70. Mandatory Agent Reporting Format

After every significant task, report:

```markdown
## Completed

- Files changed
- Responsibilities moved
- Bugs fixed
- Interfaces introduced
- Tests added

## Architecture Impact

- Windows-specific code removed from Core
- New platform-specific behavior
- New dependencies

## Validation

- dotnet build result
- dotnet test result
- platform tested
- manual tests performed

## Known Limitations

- Remaining issues
- Unsupported platforms/features
- Technical debt intentionally deferred

## Next Step

- Next smallest migration task
```

Do not report a feature as complete merely because it compiles.

---

# 71. Priority Rule for the Entire Migration

When decisions conflict, use this order:

```text
Security
    >
Correctness
    >
Architecture
    >
Functional parity
    >
Cross-platform support
    >
Reliability
    >
UI redesign
    >
New features
```

In particular:

```text
Do not preserve a dangerous behavior merely for parity.

Do not reproduce a Windows-specific workaround on all platforms.

Do not sacrifice process ownership or configuration safety to make migration faster.

Do not hide behavioral differences between ClamAV backends.
```

The goal is not to reproduce the current implementation line-for-line.

The goal is to preserve useful behavior while removing the architectural and correctness problems that currently prevent the application from becoming a safe, maintainable cross-platform ClamAV frontend.
