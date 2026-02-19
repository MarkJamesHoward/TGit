namespace TGit;

public class GitTrackingInfo
{
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? RemoteUrl { get; set; }
    public List<FileEditInfo> ModifiedFiles { get; set; } = new();
    public string MachineName { get; set; } = string.Empty;
    public string Tenant { get; set; } = "default";
}
