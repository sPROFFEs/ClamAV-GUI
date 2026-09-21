using ClamAVGui.Core.Parsing;
using Xunit;

namespace ClamAVGui.Core.Tests;

public class ConfigParserTests
{
    private readonly ClamAvConfigParser _parser = new();

    [Fact]
    public void Parse_ExactDirectiveMatching_DoesNotConfuseLogFileWithLogFileMaxSize()
    {
        var config = @"
# Sample configuration
LogFile /var/log/clamav/clamd.log
LogFileMaxSize 10M
LogTime yes
TCPSocket 3310
TCPAddr 127.0.0.1
";

        var directives = _parser.Parse(config);

        var logFile = _parser.GetDirectiveValue(directives, "LogFile");
        var logFileMaxSize = _parser.GetDirectiveValue(directives, "LogFileMaxSize");
        var tcpAddr = _parser.GetDirectiveValue(directives, "TCPAddr");

        Assert.Equal("/var/log/clamav/clamd.log", logFile);
        Assert.Equal("10M", logFileMaxSize);
        Assert.Equal("127.0.0.1", tcpAddr);
    }

    [Fact]
    public void Parse_IgnoresCommentsAndSampleMarker()
    {
        var config = @"
Example
# DatabaseDirectory /var/lib/clamav
; LogClean yes
DatabaseDirectory /opt/clamav/database
";

        var directives = _parser.Parse(config);

        Assert.Single(directives);
        Assert.Equal("DatabaseDirectory", directives[0].Name);
        Assert.Equal("/opt/clamav/database", directives[0].Value);
    }

    [Fact]
    public void SetOrUpdateDirective_UpdatesExistingAndAppendsNew()
    {
        var original = "TCPSocket 3310\nTCPAddr 0.0.0.0";
        var updated = _parser.SetOrUpdateDirective(original, "TCPAddr", "127.0.0.1");

        var directives = _parser.Parse(updated);
        Assert.Equal("127.0.0.1", _parser.GetDirectiveValue(directives, "TCPAddr"));
        Assert.Equal("3310", _parser.GetDirectiveValue(directives, "TCPSocket"));
    }
}
