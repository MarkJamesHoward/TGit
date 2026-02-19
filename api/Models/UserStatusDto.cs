namespace TGitApi.Models;

public class UserStatusDto
{
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<RepoActivity> Activities { get; set; } = new();
}
