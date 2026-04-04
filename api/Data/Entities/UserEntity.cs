namespace TGitApi.Data.Entities;

public class UserEntity
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
    public string Tenant { get; set; } = "default";
    public List<RepoActivityEntity> Activities { get; set; } = new();
}
