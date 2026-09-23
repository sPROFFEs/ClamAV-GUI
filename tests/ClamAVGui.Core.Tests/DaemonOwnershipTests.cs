using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Core.Services;
using Moq;
using Xunit;

namespace ClamAVGui.Core.Tests;

public class DaemonOwnershipTests
{
    [Fact]
    public async Task StopAsync_WhenNoManagedProcess_DoesNotSendShutdownOrThrow()
    {
        var protocolMock = new Mock<IClamdProtocol>();
        var manager = new ClamAvDaemonManager(
            protocolMock.Object,
            () => Task.FromResult<ClamAvInstallation?>(null),
            () => Task.FromResult<ClamdEndpoint>(new TcpClamdEndpoint("127.0.0.1", 3310)));

        // Attempting to stop when unmanaged / external
        await manager.StopAsync();

        // Must NOT send SHUTDOWN to external endpoint
        protocolMock.Verify(p => p.ShutdownAsync(It.IsAny<ClamdEndpoint>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
