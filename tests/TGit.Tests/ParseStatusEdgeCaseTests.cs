using Xunit;

namespace TGit.Tests;

public class ParseStatusEdgeCaseTests
{
    [Fact]
    public void TrimsWhitespaceBeforeMapping()
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("ParseStatus", "  M  ");

        Assert.Equal("Modified", result);
    }

    [Fact]
    public void ReturnsOriginalCode_WhenUnknown()
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("ParseStatus", "XY");

        Assert.Equal("XY", result);
    }
}
