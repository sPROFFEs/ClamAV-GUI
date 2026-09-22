# ClamAV GUI Windows PowerShell Installer
# Run via: irm https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/main/install.ps1 | iex

$ErrorActionPreference = 'Stop'

$Repo = "sPROFFEs/ClamAV-GUI"
$GitHubApi = "https://api.github.com/repos/$Repo/releases"

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
    $ReleaseInfo = Invoke-RestMethod -Uri $GitHubApi -Headers @{ "Accept" = "application/vnd.github.v3+json" }
    if ($ReleaseInfo.Count -gt 0) {
        $Asset = $ReleaseInfo[0].assets | Where-Object { $_.name -like "*$Arch*.zip" } | Select-Object -First 1
        if ($Asset) {
            $DownloadUrl = $Asset.browser_download_url
        }
    }
}
catch {
    Write-Warning "Could not fetch releases via GitHub API, using fallback URL."
}

if (-not $DownloadUrl) {
    throw "No release asset found for $Arch. Check https://github.com/$Repo/releases"
}

Write-Host "Downloading ClamAV GUI from: $DownloadUrl" -ForegroundColor Gray
$TempZip = Join-Path $env:TEMP "ClamAV-GUI-$Arch.zip"

Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip

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
$StartMenuDir = [System.IO.Path]::Combine($env:APPDATA, "Microsoft", "Windows", "Start Menu", "Programs")
$ShortcutPath = Join-Path $StartMenuDir "ClamAV GUI.lnk"

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "ClamAV GUI Antivirus Scanner"
$Shortcut.Save()

# 5. Add to User PATH if missing
$UserPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
if ($UserPath -notlike "*$InstallDir*") {
    $NewPath = "$UserPath;$InstallDir"
    [System.Environment]::SetEnvironmentVariable("PATH", $NewPath, "User")
    Write-Host "Added $InstallDir to User PATH." -ForegroundColor Gray
}

Write-Host "`n✓ ClamAV GUI has been successfully installed!" -ForegroundColor Green
Write-Host "You can launch it from your Start Menu or by running 'ClamAVGui.App.exe'."
