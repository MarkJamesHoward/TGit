using Xunit;

namespace TGit.Tests;

public class ParseStatusEdgeCaseTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Fact]
    public void TrimsWhitespaceBeforeMapping()
    {
        var result = GitOperations.ParseStatus("  M  ");

        Assert.Equal("Modified", result);
    }

    [Fact]
    public void ReturnsOriginalCode_WhenUnknown()
    {
        var result = GitOperations.ParseStatus("XY");

        Assert.Equal("XY", result);
    }
}
