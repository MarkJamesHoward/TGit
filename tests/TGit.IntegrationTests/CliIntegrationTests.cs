using System.Text.Json;
using Xunit;

namespace TGit.IntegrationTests;

public class CliIntegrationTests
{
    [Fact]
    public async Task StatusCommand_PassthroughsToGit_AndReturnsSuccess()
    {
        var repoRoot = IntegrationTestSupport.FindRepoRoot();
        var tempRepoPath = await IntegrationTestSupport.CreateTempGitRepoAsync();
        var tempHome = Path.Combine(
            Path.GetTempPath(),
            "tgit-it-home",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempHome);

        try
        {
            var result = await IntegrationTestSupport.RunTgitAsync(
                repoRoot,
                tempRepoPath,
                ["status"],
                new Dictionary<string, string?>
                {
                    ["USERPROFILE"] = tempHome,
                    ["TGIT_CLI_API_URL"] = "http://127.0.0.1:1",
                }
            );

            Assert.Equal(0, result.ExitCode);
            Assert.True(
                result.StdOut.Contains("On branch", StringComparison.OrdinalIgnoreCase)
                    || result.StdOut.Contains("No commits yet", StringComparison.OrdinalIgnoreCase),
                $"Unexpected output. StdOut: {result.StdOut}{Environment.NewLine}StdErr: {result.StdErr}"
            );
        }
        finally
        {
            TryDeleteDirectory(tempRepoPath);
            TryDeleteDirectory(tempHome);
        }
    }

    [Fact]
    public async Task StatusCommand_SendsTrackingPostToConfiguredApi()
    {
        var repoRoot = IntegrationTestSupport.FindRepoRoot();
        var tempRepoPath = await IntegrationTestSupport.CreateTempGitRepoAsync();
        var tempHome = Path.Combine(
            Path.GetTempPath(),
            "tgit-it-home",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempHome);

        await using var server = LoopbackHttpCaptureServer.Start();

        try
        {
            var result = await IntegrationTestSupport.RunTgitAsync(
                repoRoot,
                tempRepoPath,
                ["status"],
                new Dictionary<string, string?>
                {
                    ["USERPROFILE"] = tempHome,
                    ["TGIT_CLI_API_URL"] = server.BaseUrl,
                }
            );

            Assert.Equal(0, result.ExitCode);

            var request = await server.WaitForRequestAsync();

            Assert.Equal("POST", request.Method);
            Assert.Equal("/api/GitActivity", request.Path);

            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            Assert.True(
                root.TryGetProperty("tenant", out _),
                "Missing tenant in tracking payload."
            );
            Assert.True(
                root.TryGetProperty("repoName", out _),
                "Missing repoName in tracking payload."
            );
            Assert.True(
                root.TryGetProperty("modifiedFiles", out _),
                "Missing modifiedFiles in tracking payload."
            );
        }
        finally
        {
            TryDeleteDirectory(tempRepoPath);
            TryDeleteDirectory(tempHome);
        }
    }

    [Fact]
    public async Task ClearCommand_SendsDeleteWithTenant_AndReturnsSuccess()
    {
        var repoRoot = IntegrationTestSupport.FindRepoRoot();
        var tempRepoPath = await IntegrationTestSupport.CreateTempGitRepoAsync();
        var tempHome = Path.Combine(
            Path.GetTempPath(),
            "tgit-it-home",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempHome);

        await using var server = LoopbackHttpCaptureServer.Start();

        try
        {
            var result = await IntegrationTestSupport.RunTgitAsync(
                repoRoot,
                tempRepoPath,
                ["--clear"],
                new Dictionary<string, string?>
                {
                    ["USERPROFILE"] = tempHome,
                    ["TGIT_CLI_API_URL"] = server.BaseUrl,
                    ["TGIT_TENANT"] = "Acme.Team",
                }
            );

            var request = await server.WaitForRequestAsync();

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("DELETE", request.Method);
            Assert.Equal("/api/GitActivity?tenant=acme.team", request.Path);
            Assert.True(
                result.StdOut.Contains(
                    "Deleting all data for tenant: acme.team",
                    StringComparison.Ordinal
                )
            );
            Assert.True(
                result.StdOut.Contains("Data deleted successfully.", StringComparison.Ordinal)
            );
        }
        finally
        {
            TryDeleteDirectory(tempRepoPath);
            TryDeleteDirectory(tempHome);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch { }
    }
}
