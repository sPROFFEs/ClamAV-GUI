using ClamAVGui.Core.Quarantine;
using Xunit;

namespace ClamAVGui.Core.Tests;

public class QuarantineTests : IDisposable
{
    private readonly string _testDir;
    private readonly QuarantineService _service;

    public QuarantineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "clamav_quarantine_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _service = new QuarantineService(_testDir);
    }

    [Fact]
    public async Task QuarantineFile_MovesFile_GeneratesSha256_AndRestoresSuccessfully()
    {
        var sourceFile = Path.Combine(_testDir, "suspicious.txt");
        await File.WriteAllTextAsync(sourceFile, "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

        var item = await _service.QuarantineFileAsync(sourceFile, "EICAR-Test-Signature");

        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(item.QuarantinePath));
        Assert.NotEmpty(item.FileHashSha256);

        var items = await _service.LoadItemsAsync();
        Assert.Single(items);

        // Restore file
        await _service.RestoreItemAsync(item.Id);
        Assert.True(File.Exists(sourceFile));
        Assert.False(File.Exists(item.QuarantinePath));

        items = await _service.LoadItemsAsync();
        Assert.Empty(items);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
        }
    }
}
