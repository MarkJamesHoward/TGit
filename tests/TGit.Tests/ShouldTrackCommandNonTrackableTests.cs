using Xunit;

namespace TGit.Tests;

public class ShouldTrackCommandNonTrackableTests
{
    private static readonly GitOperationsService GitOperations = new();

    [Fact]
    public void ReturnsFalse_WhenNoArgs()
    {
        var result = InvokeShouldTrack([]);

        Assert.False(result);
    }

    [Theory]
    [InlineData("log")]
    [InlineData("branch")]
    [InlineData("diff")]
    [InlineData("remote")]
    [InlineData("init")]
    public void ReturnsFalse_ForNonTrackableCommands(string command)
    {
        var result = InvokeShouldTrack([command]);

        Assert.False(result);
    }

    [Fact]
    public void UsesOnlyFirstArg_ForDetection()
    {
        var trackableWithExtraArgs = InvokeShouldTrack(["status", "--short"]);
        var nonTrackableWithExtraArgs = InvokeShouldTrack(["log", "--oneline"]);

        Assert.True(trackableWithExtraArgs);
        Assert.False(nonTrackableWithExtraArgs);
    }

    private static bool InvokeShouldTrack(string[] args)
    {
        return GitOperations.ShouldTrackCommand(args);
    }
}
