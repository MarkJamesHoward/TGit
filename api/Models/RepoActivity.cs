namespace TGitApi.Models;

public class RepoActivity
{
    public string RepoName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? RemoteUrl { get; set; }
    public List<FileEditInfo> ModifiedFiles { get; set; } = new();
    public string LastUpdated { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}
