<h1>
  <img
    src="/assets_readme/icon.png"
    alt="ClamAV-GUI Icon"
    width="50"
    height="50"
    style="vertical-align: middle; margin-right: 10px;"
  />
  ClamAV-GUI
</h1>

[![CI](https://github.com/sPROFFEs/ClamAV-GUI/actions/workflows/ci.yml/badge.svg)](https://github.com/sPROFFEs/ClamAV-GUI/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/sPROFFEs/ClamAV-GUI?include_prereleases)](https://github.com/sPROFFEs/ClamAV-GUI/releases)
[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](LICENSE.md)

## Description

**ClamAV-GUI** is a cross-platform desktop application written in C# and powered by **Avalonia UI** that provides a modern graphical interface for the open-source **ClamAV** antivirus engine.

Available natively on:
- 🐧 **Linux** (`x64`, `ARM64`)
- 🍏 **macOS** (Apple Silicon `ARM64` & Intel `x64`)
- 🪟 **Windows** (`x64`, `ARM64`)

---

## ⚡ Quick Install (One-Liners)

### Linux & macOS

Run the universal installer script in your terminal:

```bash
curl -sSL https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/migration/avalonia/install.sh | bash
```

*This automatically detects your OS and architecture (`x86_64` or `arm64`), downloads the latest release, installs it to `~/.local/share/clamav-gui`, creates the `clamav-gui` command, and registers the app in your desktop launcher.*

---

### Windows (PowerShell)

Run the PowerShell installer:

```powershell
irm https://raw.githubusercontent.com/sPROFFEs/ClamAV-GUI/migration/avalonia/install.ps1 | iex
```

*This installs ClamAV GUI to `%LOCALAPPDATA%\ClamAV-GUI`, creates a Start Menu shortcut, and adds it to your user `PATH`.*

---

## Features

- 🔍 **Manual Scanning**: Scan single files or recursive directories with live progress tracking, threat highlighting, and 1-click quarantine.
- ⚡ **Quick Scan Preset**: Scan your User / Downloads directory with one click.
- 🔄 **Virus Definitions Updater**: Integrated `freshclam` updates with live logs, status metrics, and cancellation support.
- ☣️ **Quarantine Vault**: Isolated threat storage with SHA-256 integrity checks, file size reporting, and safe restore/delete actions.
- ⚙️ **Daemon Control (`clamd`)**: Start, stop, and reload the ClamAV daemon using secure local loopback sockets without risking external system daemons.
- 👁️ **Real-Time Monitoring**: Debounced filesystem watcher for user-defined folders with inclusion filters and exclusion lists.
- ⏰ **Scheduled Scans**: Configure daily automated background scans using native platform schedulers.
- 🩺 **Diagnostics & Health Check**: Automatic validation of engine components, socket connections, and privacy-redacted diagnostic export.
- 🛠️ **Auto-Detection**: Auto-detects system ClamAV binaries across `$PATH` and standard platform directories in one click.

---

## Manual Download & Building

Standalone portable binaries are available on the [Releases](https://github.com/sPROFFEs/ClamAV-GUI/releases) page.

### Build from Source

Requirements: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
git clone -b migration/avalonia https://github.com/sPROFFEs/ClamAV-GUI.git
cd ClamAV-GUI

# Build and run tests
dotnet build
dotnet test

# Run the GUI
dotnet run --project src/ClamAVGui.App/ClamAVGui.App.csproj
```

---

## Contributing

Contributions are welcome! Please open an issue for bugs or feature suggestions, and submit pull requests targeting the `migration/avalonia` branch.

## License

This project is licensed under the Creative Commons Attribution-NonCommercial 4.0 International License. See [LICENSE.md](LICENSE.md) for details.

## Credits

This project interfaces with the ClamAV antivirus engine developed by Cisco Systems, licensed under GPL v2. See [clamav.net](https://www.clamav.net) for more information.
