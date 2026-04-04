using System.Text.Json;

namespace TGit;

internal class TrackingService
{
    private readonly string _apiEndpoint;
    private readonly HttpClient _httpClient;
    private readonly GitOperationsService _gitOperations;
    private readonly ConfigService _configService;

    public TrackingService(
        string apiEndpoint,
        HttpClient httpClient,
        GitOperationsService gitOperations,
        ConfigService configService
    )
    {
        _apiEndpoint = apiEndpoint;
        _httpClient = httpClient;
        _gitOperations = gitOperations;
        _configService = configService;
    }

    public async Task SendTrackingInfoAsync()
    {
        try
        {
            var trackingInfo = await GatherTrackingInfoAsync();
            if (trackingInfo == null)
                return;

            var json = JsonSerializer.Serialize(
                trackingInfo,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                }
            );

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                var response = await _httpClient.PostAsync(_apiEndpoint, content, cts.Token);
                if (Environment.GetEnvironmentVariable("TGIT_DEBUG") == "1")
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"[TGit Debug] POST {_apiEndpoint} -> {(int)response.StatusCode} {response.StatusCode}");
                    if (!response.IsSuccessStatusCode)
                        Console.Error.WriteLine($"[TGit Debug] Response: {body}");
                }
            }
            catch (TaskCanceledException) { }
            catch (HttpRequestException ex)
            {
                if (Environment.GetEnvironmentVariable("TGIT_DEBUG") == "1")
                    Console.Error.WriteLine($"[TGit Debug] HTTP error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("TGIT_DEBUG") == "1")
            {
                Console.Error.WriteLine($"[TGit Debug] Error sending tracking info: {ex.Message}");
            }
        }
    }

    public async Task<GitTrackingInfo?> GatherTrackingInfoAsync()
    {
        var repoRoot = await _gitOperations.GetGitOutputAsync("rev-parse", "--show-toplevel");
        if (string.IsNullOrEmpty(repoRoot))
            return null;

        var userName = await _gitOperations.GetGitOutputAsync("config", "user.name");
        var userEmail = await _gitOperations.GetGitOutputAsync("config", "user.email");
        var repoName = await _gitOperations.GetRepoNameAsync();
        var branch = await _gitOperations.GetGitOutputAsync("rev-parse", "--abbrev-ref", "HEAD");
        var modifiedFiles = await _gitOperations.GetModifiedFilesAsync();
        var remoteUrl = await _gitOperations.GetGitOutputAsync(
            "config",
            "--get",
            "remote.origin.url"
        );

        return new GitTrackingInfo
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName ?? "unknown",
            UserEmail = userEmail ?? "unknown",
            RepoName = repoName ?? "unknown",
            Branch = branch ?? "unknown",
            RemoteUrl = remoteUrl,
            ModifiedFiles = modifiedFiles,
            MachineName = Environment.MachineName,
            Tenant = _configService.GetTenant(),
        };
    }
}
