using Xunit;

namespace TGit.Tests;

public class EscapeArgumentSimpleTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Fact]
    public void ReturnsDoubleQuotes_ForEmptyArgument()
    {
        var result = GitOperations.EscapeArgument("");

        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void ReturnsUnchanged_ForSimpleArgument()
    {
        var result = GitOperations.EscapeArgument("status");

        Assert.Equal("status", result);
    }
}
