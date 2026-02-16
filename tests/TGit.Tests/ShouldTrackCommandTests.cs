using System.Reflection;
using TGit;
using Xunit;

namespace TGit.Tests;

public class ShouldTrackCommandTests
{
    private static readonly MethodInfo ShouldTrackCommandMethod =
        typeof(GitTrackingInfo)
            .Assembly.GetType("TGit.Program", throwOnError: true)!
            .GetMethod("ShouldTrackCommand", BindingFlags.NonPublic | BindingFlags.Static)!
        ?? throw new InvalidOperationException("Could not locate ShouldTrackCommand method.");

    public static IEnumerable<object[]> TrackableCommands()
    {
        yield return ["status"];
        yield return ["add"];
        yield return ["commit"];
        yield return ["checkout"];
        yield return ["switch"];
        yield return ["restore"];
        yield return ["reset"];
        yield return ["merge"];
        yield return ["rebase"];
        yield return ["cherry-pick"];
        yield return ["revert"];
        yield return ["stash"];
        yield return ["pull"];
        yield return ["push"];
        yield return ["fetch"];
        yield return ["clone"];
    }

    [Theory]
    [MemberData(nameof(TrackableCommands))]
    public void ReturnsTrue_ForAllTrackableCommands(string command)
    {
        var result = InvokeShouldTrack([command]);

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(TrackableCommands))]
    public void IsCaseInsensitive_ForTrackableCommands(string command)
    {
        var result = InvokeShouldTrack([command.ToUpperInvariant()]);

        Assert.True(result);
    }

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
        var result = ShouldTrackCommandMethod.Invoke(null, [args]);
        return result is true;
    }
}
