# ClamAV GUI Windows PowerShell Installer
# Run via: irm https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/main/install.ps1 | iex

$ErrorActionPreference = 'Stop'

# Ensure TLS 1.2 is used
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls11 -bor [Net.SecurityProtocolType]::Tls

$Repo = "sPROFFEs/ClamAV-GUI"
$GitHubApi = "https://api.github.com/repos/$Repo/releases"

Write-Host "=== ClamAV GUI Windows Installer ===" -ForegroundColor Cyan

# 1. Architecture Check
if (-not [System.Environment]::Is64BitOperatingSystem) {
    Write-Error "32-bit Windows is not supported. Please run on 64-bit Windows."
}
$Arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }

# 2. Determine Download URL
Write-Host "Fetching latest release information..." -ForegroundColor Gray
$ReleaseInfo = Invoke-RestMethod -Uri "$GitHubApi`?per_page=20" -Headers @{ "Accept" = "application/vnd.github+json"; "User-Agent" = "ClamAV-GUI-Installer" } -TimeoutSec 30
$Asset = $ReleaseInfo | ForEach-Object { $_.assets } | Where-Object { $_.name -match "-$Arch\.zip$" } | Select-Object -First 1
if (-not $Asset) {
    throw "No published release package was found for $Arch."
}
$DownloadUrl = $Asset.browser_download_url

Write-Host "Downloading ClamAV GUI from: $DownloadUrl" -ForegroundColor Gray
$TempZip = Join-Path $env:TEMP "ClamAV-GUI-$Arch-$([guid]::NewGuid().ToString('N')).zip"

try {
    (New-Object System.Net.WebClient).DownloadFile($DownloadUrl, $TempZip)
}
catch {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip -UseBasicParsing
}

# 3. Extract to LocalAppData
$InstallDir = Join-Path $env:LOCALAPPDATA "ClamAV-GUI"
Write-Host "Installing to: $InstallDir" -ForegroundColor Gray

$StagingDir = "$InstallDir.staging.$([guid]::NewGuid().ToString('N'))"
$BackupDir = "$InstallDir.backup.$([guid]::NewGuid().ToString('N'))"
try {
    Expand-Archive -Path $TempZip -DestinationPath $StagingDir -Force
    if (-not (Test-Path -LiteralPath (Join-Path $StagingDir "ClamAVGui.App.exe") -PathType Leaf)) {
        throw "The release package does not contain ClamAVGui.App.exe."
    }

    if (Test-Path -LiteralPath $InstallDir) {
        Move-Item -LiteralPath $InstallDir -Destination $BackupDir
    }
    try {
        Move-Item -LiteralPath $StagingDir -Destination $InstallDir
    }
    catch {
        if (Test-Path -LiteralPath $BackupDir) {
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
try {
    $StartMenuDir = [System.IO.Path]::Combine($env:APPDATA, "Microsoft", "Windows", "Start Menu", "Programs")
    if (-not (Test-Path $StartMenuDir)) {
        New-Item -ItemType Directory -Path $StartMenuDir -Force | Out-Null
    }
    $ShortcutPath = Join-Path $StartMenuDir "ClamAV GUI.lnk"

    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $ExePath
    $Shortcut.WorkingDirectory = $InstallDir
    $Shortcut.Description = "ClamAV GUI Antivirus Scanner"
    $Shortcut.Save()
}
catch {
    Write-Warning "Could not create Start Menu shortcut: $($_.Exception.Message)"
}

# 5. Add to User PATH if missing
try {
    $UserPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
    if ($UserPath -notlike "*$InstallDir*") {
        $NewPath = if ([string]::IsNullOrWhiteSpace($UserPath)) { $InstallDir } else { "$UserPath;$InstallDir" }
        [System.Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
        Write-Host "Added $InstallDir to User PATH." -ForegroundColor Gray
    }
}
catch {
}

Write-Host "`nClamAV GUI has been successfully installed!" -ForegroundColor Green
Write-Host "Launch it from the Start Menu or run: $ExePath"
