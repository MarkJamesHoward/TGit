using Xunit;

namespace TGit.Tests;

public class ExtractRepoNameFromUrlCommonFormatTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Theory]
    [InlineData("https://github.com/user/repo.git", "repo")]
    [InlineData("https://github.com/user/repo", "repo")]
    [InlineData("git@github.com:user/repo.git", "repo")]
    [InlineData("ssh://git@github.com/user/repo.git", "repo")]
    [InlineData("https://github.com/user/repo/", "repo")]
    public void ExtractsRepoName_FromCommonGitUrlFormats(string url, string expected)
    {
        var result = GitOperations.ExtractRepoNameFromUrl(url);

        Assert.Equal(expected, result);
    }
}
