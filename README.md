<!-- App icon placeholder -->
<img src="/assets_readme/icon.png" alt="ClamAV-GUI Icon" width="50" height="50" style="float:left; margin-right:10px;" />

# ClamAV-GUI  
<br clear="left" />

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

- browse and select files or directories for scanning  
- display real-time progress and scan statistics  
- generate reports of any threats found  
- integrate seamlessly with an existing ClamAV installation  

## Installation

1. clone this repository to your local machine  
2. open `ClamAVGui.sln` in Visual Studio 2019 or later  
3. restore NuGet packages  
4. build and run the solution  
5. Or compile the project and run the executable file

Alternatively, you can download the latest release from the [Releases](https://github.com/sdksdk/ClamAV-GUI/releases) page.

## Usage

1. ensure ClamAV is installed and up to date on your system—you can download it from https://www.clamav.net/downloads  
2. I recommend using the portable version of ClamAV, which is available [here](https://www.clamav.net/downloads/production/clamav-1.4.3.win.x64.zip).  
3. After decompressing or installing ClamAV, launch ClamAV-GUI.  
4. Go to the settings panel and click “Locate ClamAV installation path.” Select the root directory of your ClamAV installation.  
5. On the settings panel, click “Initialize configuration” then “Download latest database.”  
6. select the target files or directories  
7. click “Scan” to start the analysis  
8. review the report for any detections  

## Contributing

Contributions are welcome. please open an issue for bugs or feature requests and submit pull requests with clear descriptions of your changes.

## License

This project is licensed under the Creative Commons Attribution-NonCommercial 4.0 International License. see the LICENSE file for details.

## Credits

this project relies on the ClamAV antivirus engine by Cisco Systems, licensed under the GNU General Public License v2. see https://www.clamav.net for more information.
