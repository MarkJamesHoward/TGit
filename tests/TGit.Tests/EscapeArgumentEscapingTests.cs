using Xunit;

namespace TGit.Tests;

public class EscapeArgumentEscapingTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Theory]
    [InlineData("hello world", "\"hello world\"")]
    [InlineData("has\"quote", "\"has\\\"quote\"")]
    [InlineData("C:\\tmp\\file.txt", "\"C:\\\\tmp\\\\file.txt\"")]
    public void EscapesAndWraps_WhenArgumentNeedsQuoting(string arg, string expected)
    {
        var result = GitOperations.EscapeArgument(arg);

        Assert.Equal(expected, result);
    }
}
