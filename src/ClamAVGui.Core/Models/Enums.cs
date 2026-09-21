namespace ClamAVGui.Core.Models;

public enum ScanVerdict
{
    Clean,
    Infected,
    Error,
    Cancelled
}

public enum ScanBackendKind
{
    ClamScan,
    ClamD,
    ClamDScan
}

public enum DaemonOwnership
{
    ExternalSystemService,
    ManagedByApplication,
    Remote
}

public enum ConfigurationOwnership
{
    External,
    ManagedByApplication
}

public enum ClamAvInstallationSource
{
    System,
    Managed,
    Bundled,
    Custom
}

public enum ScanTargetType
{
    File,
    Directory
}

public enum RealTimeProtectionMode
{
    Unsupported,
    ReactiveFileWatcher,
    NativeOnAccess
}

public enum PlatformKind
{
    Windows,
    Linux,
    MacOS,
    Unknown
}

public enum SymbolicLinkPolicy
{
    DoNotFollow,
    FollowFiles,
    FollowAll
}
