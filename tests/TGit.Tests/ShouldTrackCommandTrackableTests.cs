using Xunit;

namespace TGit.Tests;

public class ShouldTrackCommandTrackableTests
{
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

    private static bool InvokeShouldTrack(string[] args)
    {
        return ProgramPrivateMethodInvoker.InvokeBool("ShouldTrackCommand", [args]);
    }
}
