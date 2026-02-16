using Xunit;

namespace TGit.Tests;

public class EscapeArgumentSimpleTests
{
    [Fact]
    public void ReturnsDoubleQuotes_ForEmptyArgument()
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("EscapeArgument", "");

        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void ReturnsUnchanged_ForSimpleArgument()
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("EscapeArgument", "status");

        Assert.Equal("status", result);
    }
}
