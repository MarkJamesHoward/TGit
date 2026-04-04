namespace TGitApi.Data.Entities;

public class RepoActivityEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ActivityKey { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? RemoteUrl { get; set; }
    public string LastUpdated { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public UserEntity User { get; set; } = null!;
    public List<FileEditEntity> ModifiedFiles { get; set; } = new();
}
