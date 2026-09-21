using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using ClamAVGui.Platform.Linux;
using ClamAVGui.Platform.MacOS;
using ClamAVGui.Platform.Services;
using Moq;
using Xunit;

namespace ClamAVGui.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void LinuxPlatformService_HasLinuxKindAndXdgPaths()
    {
        var service = new LinuxPlatformService();
        Assert.Equal(PlatformKind.Linux, service.Platform);
        Assert.Contains("clamav-gui", service.UserDataDirectory);
        Assert.True(service.Capabilities.SupportsClamScan);
        Assert.True(service.Capabilities.SupportsScheduling);
    }

    [Fact]
    public void MacOsPlatformService_HasMacOsKindAndLibraryPaths()
    {
        var service = new MacOsPlatformService();
        Assert.Equal(PlatformKind.MacOS, service.Platform);
        Assert.Contains("Library/Application Support/ClamAVGui", service.UserDataDirectory);
        Assert.True(service.Capabilities.SupportsClamScan);
    }

    [Fact]
    public async Task DiagnosticsService_CollectsAndFormatsReport()
    {
        var platformMock = new Mock<IPlatformService>();
        platformMock.Setup(p => p.Platform).Returns(PlatformKind.Linux);
        platformMock.Setup(p => p.Architecture).Returns("x64");
        platformMock.Setup(p => p.Capabilities).Returns(new PlatformCapabilities { SupportsClamScan = true });
        platformMock.Setup(p => p.UserDataDirectory).Returns("/home/user/.local/share/clamav-gui");

        var binaryMock = new Mock<IClamAvBinaryLocator>();
        binaryMock.Setup(b => b.FindInstallationAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvInstallation
            {
                ClamScanPath = "/usr/bin/clamscan",
                FreshClamPath = "/usr/bin/freshclam",
                ClamdPath = "/usr/sbin/clamd",
                ConfigDirectory = "/etc/clamav",
                DatabaseDirectory = "/var/lib/clamav",
                Version = "1.4.1",
                Source = ClamAvInstallationSource.System
            });

        var daemonMock = new Mock<IClamAvDaemon>();
        daemonMock.Setup(d => d.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamdHealth
            {
                EndpointReachable = true,
                ProtocolHealthy = true,
                Version = "ClamAV 1.4.1"
            });

        var configMock = new Mock<IClamAvConfigurationProvider>();
        configMock.Setup(c => c.LoadAsync(It.IsAny<ClamAvInstallation?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvEffectiveConfiguration
            {
                DaemonEndpoint = new UnixClamdEndpoint("/run/clamav/clamd.ctl")
            });

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationSettings());

        var diagnosticsService = new DiagnosticsService(
            platformMock.Object,
            binaryMock.Object,
            daemonMock.Object,
            configMock.Object,
            settingsMock.Object);

        var report = await diagnosticsService.CollectReportAsync();
        Assert.NotNull(report);
        Assert.Equal("/usr/bin/clamscan", report.ClamScanPath);
        Assert.True(report.DaemonRunning);

        var formatted = diagnosticsService.FormatReport(report, redactSensitivePaths: false);
        Assert.Contains("=== ClamAV GUI Diagnostics Report ===", formatted);
        Assert.Contains("/usr/bin/clamscan", formatted);
    }
}
