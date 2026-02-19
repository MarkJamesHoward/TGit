using Xunit;

namespace TGit.Tests;

public class ParseStatusMappingTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Theory]
    [InlineData("A", "Added")]
    [InlineData("M", "Modified")]
    [InlineData("D", "Deleted")]
    [InlineData("R", "Renamed")]
    [InlineData("C", "Copied")]
    [InlineData("U", "Unmerged")]
    [InlineData("?", "Untracked")]
    public void ReturnsExpectedMappedValue(string statusCode, string expected)
    {
        var result = GitOperations.ParseStatus(statusCode);

        Assert.Equal(expected, result);
    }
}
