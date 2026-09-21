using ClamAVGui.Core.Models;
using ClamAVGui.Core.Parsing;
using Xunit;

namespace ClamAVGui.Core.Tests;

public class OutputParserTests
{
    private readonly ClamAvOutputParser _parser = new();

    [Fact]
    public void Parse_CleanScan_ReturnsCleanVerdict()
    {
        var stdout = @"/home/user/test.txt: OK

----------- SCAN SUMMARY -----------
Known viruses: 8694020
Engine version: 1.4.1
Scanned directories: 0
Scanned files: 1
Infected files: 0
Data scanned: 0.05 MB
Data read: 0.02 MB (ratio 2.50:1)
Time: 0.123 sec (0 m 0 s)
Start Date: 2026:09:21 12:00:00
End Date:   2026:09:21 12:00:00";

        var result = _parser.Parse(stdout, string.Empty, exitCode: 0, ScanBackendKind.ClamScan);

        Assert.Equal(ScanVerdict.Clean, result.Verdict);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Detections);
        Assert.NotNull(result.Statistics);
        Assert.Equal("1", result.Statistics.ScannedFiles);
        Assert.Equal("0", result.Statistics.InfectedFiles);
        Assert.Equal("1.4.1", result.Statistics.EngineVersion);
    }

    [Fact]
    public void Parse_InfectedScan_ReturnsInfectedVerdictAndThreat()
    {
        var stdout = @"/home/user/downloads/eicar.com: Win.Test.EICAR_HDB-1 FOUND

----------- SCAN SUMMARY -----------
Known viruses: 8694020
Engine version: 1.4.1
Scanned directories: 0
Scanned files: 1
Infected files: 1
Data scanned: 0.00 MB
Data read: 0.00 MB (ratio 0.00:1)
Time: 0.010 sec (0 m 0 s)";

        var result = _parser.Parse(stdout, string.Empty, exitCode: 1, ScanBackendKind.ClamScan);

        Assert.Equal(ScanVerdict.Infected, result.Verdict);
        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Detections);
        Assert.Equal("/home/user/downloads/eicar.com", result.Detections[0].FilePath);
        Assert.Equal("Win.Test.EICAR_HDB-1", result.Detections[0].ThreatName);
    }

    [Fact]
    public void Parse_WindowsPath_CorrectlyExtractsFilePath()
    {
        var line = @"C:\Users\Admin\Downloads\virus.exe: Eicar-Test-Signature FOUND";
        var parsed = _parser.TryParseLine(line);

        Assert.NotNull(parsed);
        Assert.Equal(@"C:\Users\Admin\Downloads\virus.exe", parsed.Value.FilePath);
        Assert.Equal("Eicar-Test-Signature FOUND", parsed.Value.Status);
    }

    [Fact]
    public void Parse_ScanErrorExitCode_ReturnsErrorVerdict()
    {
        var stderr = "ERROR: Could not open directory /root/secret";
        var result = _parser.Parse(string.Empty, stderr, exitCode: 2, ScanBackendKind.ClamScan);

        Assert.Equal(ScanVerdict.Error, result.Verdict);
        Assert.Equal(2, result.ExitCode);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseUpdateOutput_UpToDate_ReturnsAlreadyUpToDate()
    {
        var stdout = @"ClamAV update process started at Mon Sep 21 12:00:00 2026
daily.cvd database is up to date (version: 27000, sigs: 8694020, f-level: 90, builder: raynman)
main.cvd database is up to date (version: 62, sigs: 6647427, f-level: 90, builder: sigmgr)";

        var result = _parser.ParseUpdateOutput(stdout, string.Empty, exitCode: 0, wasCancelled: false);

        Assert.True(result.Success);
        Assert.True(result.IsAlreadyUpToDate);
        Assert.Equal(8694020, result.SignaturesCount);
    }

    [Fact]
    public void ParseUpdateOutput_Cancelled_ReturnsCancelledState()
    {
        var result = _parser.ParseUpdateOutput(string.Empty, string.Empty, exitCode: -1, wasCancelled: true);

        Assert.False(result.Success);
        Assert.True(result.WasCancelled);
    }
}
