using Newtonsoft.Json;

namespace TGitApi.Models;

public class UserStatus
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
    public Dictionary<string, RepoActivity> Activities { get; set; } = new();
    public string Tenant { get; set; } = "default";
}
