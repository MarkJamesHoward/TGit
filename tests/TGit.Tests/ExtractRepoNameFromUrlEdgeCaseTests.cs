using Xunit;

namespace TGit.Tests;

public class ExtractRepoNameFromUrlEdgeCaseTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Fact]
    public void ReturnsInput_WhenNoSeparatorsExist()
    {
        var result = GitOperations.ExtractRepoNameFromUrl("repo");

        Assert.Equal("repo", result);
    }
}
