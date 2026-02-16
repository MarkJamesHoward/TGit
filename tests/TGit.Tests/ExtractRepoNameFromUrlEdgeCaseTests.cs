using Xunit;

namespace TGit.Tests;

public class ExtractRepoNameFromUrlEdgeCaseTests
{
    [Fact]
    public void ReturnsInput_WhenNoSeparatorsExist()
    {
        var result = ProgramPrivateMethodInvoker.InvokeString("ExtractRepoNameFromUrl", "repo");

        Assert.Equal("repo", result);
    }
}
