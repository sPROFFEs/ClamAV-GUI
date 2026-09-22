# ClamAV GUI Windows PowerShell Installer
# Run via: irm https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/migration/avalonia/install.ps1 | iex

$ErrorActionPreference = 'Stop'

$Repo = "sPROFFEs/ClamAV-GUI"
$GitHubApi = "https://api.github.com/repos/$Repo/releases?per_page=20"

Write-Host "=== ClamAV GUI Windows Installer ===" -ForegroundColor Cyan

# 1. Determine architecture
$Architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$Arch = switch ($Architecture) {
    "x64" { "win-x64" }
    "arm64" { "win-arm64" }
    default { Write-Error "Unsupported Windows architecture: $Architecture" }
}

# 2. Find download URL
Write-Host "Fetching latest release information..."
$DownloadUrl = $null
try {
    $ReleaseInfo = @(Invoke-RestMethod -Uri $GitHubApi -Headers @{
        "Accept" = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "ClamAV-GUI-Installer"
    } -TimeoutSec 30)
    $Asset = $ReleaseInfo |
        ForEach-Object { $_.assets } |
        Where-Object { $_.name -match "-$([regex]::Escape($Arch))\.zip$" } |
        Select-Object -First 1
    if ($Asset) {
        $DownloadUrl = $Asset.browser_download_url
        $ExpectedSize = [long]$Asset.size
    }
}
catch {
    throw "Could not fetch GitHub release information: $($_.Exception.Message)"
}

if (-not $DownloadUrl) {
    throw "No release asset found for $Arch. Check https://github.com/$Repo/releases"
}

Write-Host "Downloading ClamAV GUI from: $DownloadUrl" -ForegroundColor Gray
$TempZip = Join-Path $env:TEMP "ClamAV-GUI-$Arch-$([guid]::NewGuid().ToString('N')).zip"
$InstallDir = Join-Path $env:LOCALAPPDATA "ClamAV-GUI"
$StagingDir = Join-Path $env:LOCALAPPDATA "ClamAV-GUI-staging-$([guid]::NewGuid().ToString('N'))"
$BackupDir = Join-Path $env:LOCALAPPDATA "ClamAV-GUI-backup-$([guid]::NewGuid().ToString('N'))"

try {
    Invoke-WebRequest -UseBasicParsing -Uri $DownloadUrl -OutFile $TempZip -Headers @{ "User-Agent" = "ClamAV-GUI-Installer" } -TimeoutSec 300
    if (-not (Test-Path -LiteralPath $TempZip) -or (Get-Item -LiteralPath $TempZip).Length -ne $ExpectedSize) {
        throw "The downloaded package is empty or incomplete."
    }

    # Extract and validate the new package before touching an existing installation.
    New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
    Expand-Archive -LiteralPath $TempZip -DestinationPath $StagingDir -Force
    $StagedExe = Join-Path $StagingDir "ClamAVGui.App.exe"
    if (-not (Test-Path -LiteralPath $StagedExe -PathType Leaf)) {
        throw "The package does not contain ClamAVGui.App.exe."
    }

    # 3. Replace the installation only after the staged copy is valid.
    Write-Host "Installing to: $InstallDir" -ForegroundColor Gray
    $HadPreviousInstall = Test-Path -LiteralPath $InstallDir
    if ($HadPreviousInstall) {
        Move-Item -LiteralPath $InstallDir -Destination $BackupDir
    }
    try {
        Move-Item -LiteralPath $StagingDir -Destination $InstallDir
    }
    catch {
        if ($HadPreviousInstall -and (Test-Path -LiteralPath $BackupDir)) {
            Move-Item -LiteralPath $BackupDir -Destination $InstallDir
        }
        throw
    }
    if (Test-Path -LiteralPath $BackupDir) {
        Remove-Item -LiteralPath $BackupDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
finally {
    Remove-Item -LiteralPath $TempZip -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

$ExePath = Join-Path $InstallDir "ClamAVGui.App.exe"

# 4. Create Start Menu Shortcut
$StartMenuDir = [System.IO.Path]::Combine($env:APPDATA, "Microsoft", "Windows", "Start Menu", "Programs")
New-Item -ItemType Directory -Path $StartMenuDir -Force | Out-Null
$ShortcutPath = Join-Path $StartMenuDir "ClamAV GUI.lnk"

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "ClamAV GUI Antivirus Scanner"
$Shortcut.Save()

# 5. Add to User PATH if missing
$UserPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
if (($UserPath -split ';' | ForEach-Object { $_.TrimEnd('\') }) -notcontains $InstallDir.TrimEnd('\')) {
    $NewPath = if ([string]::IsNullOrWhiteSpace($UserPath)) { $InstallDir } else { "$UserPath;$InstallDir" }
    [System.Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
    Write-Host "Added $InstallDir to User PATH." -ForegroundColor Gray
}

Write-Host "`n✓ ClamAV GUI has been successfully installed!" -ForegroundColor Green
Write-Host "You can launch it from your Start Menu or by running '$ExePath'."
