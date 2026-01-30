using TGitApi.Models;

namespace TGitApi.Services;

public interface IStorageService
{
    Task RecordActivityAsync(GitActivity activity);
    Task<List<UserStatus>> GetAllUsersAsync(string? tenant = null);
    Task<List<UserStatus>> GetActiveUsersAsync(string? tenant = null);
    bool IsUserActive(UserStatus user);
    string GetTimeSinceActivity(string timestamp);
    Task DeleteTenantAsync(string tenant);
}
