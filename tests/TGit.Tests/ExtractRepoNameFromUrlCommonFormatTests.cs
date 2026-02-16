using Xunit;

namespace TGit.Tests;

public class ExtractRepoNameFromUrlCommonFormatTests
{
    [Theory]
    [InlineData("https://github.com/user/repo.git", "repo")]
    [InlineData("https://github.com/user/repo", "repo")]
    [InlineData("git@github.com:user/repo.git", "repo")]
    [InlineData("ssh://git@github.com/user/repo.git", "repo")]
    [InlineData("https://github.com/user/repo/", "repo")]
    public void ExtractsRepoName_FromCommonGitUrlFormats(string url, string expected)
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("ExtractRepoNameFromUrl", url);

        Assert.Equal(expected, result);
    }
}
