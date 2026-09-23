# ClamAV GUI Windows PowerShell Installer
# Run via: irm https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/migration/avalonia/install.ps1 | iex

$ErrorActionPreference = 'Stop'

# Ensure TLS 1.2 is used
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls11 -bor [Net.SecurityProtocolType]::Tls

$Repo = "sPROFFEs/ClamAV-GUI"
$GitHubApi = "https://api.github.com/repos/$Repo/releases"

Write-Host "=== ClamAV GUI Windows Installer ===" -ForegroundColor Cyan

# 1. Architecture Check
$Arch = "win-x64"
if (-not [System.Environment]::Is64BitOperatingSystem) {
    Write-Error "32-bit Windows is not supported. Please run on 64-bit Windows."
}

# 2. Determine Download URL
Write-Host "Fetching latest release information..." -ForegroundColor Gray
$DownloadUrl = "https://github.com/$Repo/releases/download/v2.0.0-beta.1/ClamAV-GUI-v2.0.0-beta-$Arch.zip"

try {
    $ReleaseInfo = Invoke-RestMethod -Uri $GitHubApi -Headers @{ "Accept" = "application/vnd.github.v3+json"; "User-Agent" = "ClamAV-GUI-Installer" } -TimeoutSec 10 -ErrorAction SilentlyContinue
    if ($ReleaseInfo -and $ReleaseInfo.Count -gt 0) {
        $Asset = $ReleaseInfo[0].assets | Where-Object { $_.name -like "*$Arch*.zip" } | Select-Object -First 1
        if ($Asset) {
            $DownloadUrl = $Asset.browser_download_url
        }
    }
}
catch {
    # Silently use direct fallback URL
}

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

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

Expand-Archive -Path $TempZip -DestinationPath $InstallDir -Force
Remove-Item -Path $TempZip -Force -ErrorAction SilentlyContinue

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

Write-Host "`n✓ ClamAV GUI has been successfully installed!" -ForegroundColor Green
Write-Host "Launch it from the Start Menu or run: $ExePath"
