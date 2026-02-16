using System.Reflection;
using TGit;
using Xunit;

namespace TGit.Tests;

internal static class ProgramPrivateMethodInvoker
{
    private static readonly Type ProgramType = typeof(GitTrackingInfo).Assembly.GetType(
        "TGit.Program",
        throwOnError: true
    )!;

    internal static bool InvokeBool(string methodName, object[] args)
    {
        var result = Invoke(methodName, args);
        return result is true;
    }

    internal static string InvokeString(string methodName, params object[] args)
    {
        var result = Invoke(methodName, args);
        return Assert.IsType<string>(result);
    }

    private static object? Invoke(string methodName, object[] args)
    {
        var method =
            ProgramType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not locate {methodName} method.");

        return method.Invoke(null, args);
    }
}
