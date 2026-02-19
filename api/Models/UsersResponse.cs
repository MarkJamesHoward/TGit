namespace TGitApi.Models;

public class UsersResponse
{
    public List<UserStatusDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
}
