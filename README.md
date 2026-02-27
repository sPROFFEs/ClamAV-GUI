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

## Description

ClamAV-GUI is a Windows desktop application written in C# that provides a graphical interface for the open-source ClamAV antivirus engine. It enables users to pick files or folders, run scans, and view comprehensive results without touching the command line.

<p align="center">
  <img src="/assets_readme/dashboard.png" alt="ClamAV-GUI Screenshot 1" width="600" /><br>
</p>

<p align="center">
  <img src="/assets_readme/scan.png" alt="ClamAV-GUI Screenshot 2" width="600" /><br>
</p>

<p align="center">
  <img src="/assets_readme/settings.png" alt="ClamAV-GUI Screenshot 2" width="600" /><br>
</p>

<p align="center">
  <img src="/assets_readme/history.png" alt="ClamAV-GUI Screenshot 2" width="600" /><br>
</p>


<p align="center">
  <img src="/assets_readme/monitoring.png" alt="ClamAV-GUI Screenshot 2" width="600" /><br>
</p>

<p align="center">
  <img src="/assets_readme/daemon.png" alt="ClamAV-GUI Screenshot 2" width="600" /><br>
</p>

## Features

- Scan files and folders with live progress and detailed scan summaries.
- Real-time monitoring for selected directories with extension filters and exclusions.
- ClamAV daemon controls (start/stop, ping, reload database, supported commands).
- Quarantine management with restore and delete actions.
- Daily scheduled scans using Windows Task Scheduler.
- Health-check diagnostics for installation, configuration, signatures, and daemon availability.
- Scan/update history with search, type filters, and CSV/JSON export.

## Installation

1. clone this repository to your local machine  
2. open `ClamAVGui.sln` in Visual Studio 2022 or later  
3. restore NuGet packages  
4. build and run the solution  
5. Or compile the project and run the executable file

Alternatively, you can download the latest release from the [Releases](https://github.com/sPROFFEs/ClamAV-GUI/releases) page.

## Usage

1. Ensure ClamAV is installed and up to date on your system—you can download it from https://www.clamav.net/downloads  
2. I recommend using the portable version of ClamAV, which is available [here](https://www.clamav.net/downloads/production/clamav-1.4.3.win.x64.zip).  
3. After decompressing or installing ClamAV, launch ClamAV-GUI.  
4. Go to the Settings tab and select the root directory of your ClamAV installation.  
5. Click "Initialize Configuration Files" and then "Download Virus Database."  
6. Use the Scan tab to run manual scans (file or folder).  
7. Use the Monitoring tab to enable on-access scanning for selected folders.  
8. Use the Daemon tab for direct daemon commands and troubleshooting.  
9. Use the Quarantine and History tabs to review detections and exported reports.  

## Contributing

Contributions are welcome. please open an issue for bugs or feature requests and submit pull requests with clear descriptions of your changes.

## License

This project is licensed under the Creative Commons Attribution-NonCommercial 4.0 International License. see the LICENSE file for details.

## Credits

this project relies on the ClamAV antivirus engine by Cisco Systems, licensed under the GNU General Public License v2. see https://www.clamav.net for more information.
