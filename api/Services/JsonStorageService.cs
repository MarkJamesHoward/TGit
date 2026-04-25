using System.Text.Json;
using TGitApi.Models;

namespace TGitApi.Services;

public class JsonStorageService : IStorageService
{
    private readonly string _dataDir;
    private const int ActivityExpiryMs = 30 * 60 * 1000; // 30 minutes
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonStorageService(IConfiguration configuration)
    {
        var defaultDir = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") != null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "data", "tgit")
            : "./storage";
        _dataDir = configuration["Storage:DataDir"] ?? defaultDir;
        EnsureDataDir();
    }

    private void EnsureDataDir()
    {
        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }
    }

    private string GetUsersFilePath(string tenant)
    {
        var safeTenant = new string(tenant.Select(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        return Path.Combine(_dataDir, $"users-{safeTenant}.json");
    }

    private async Task<List<UserStatus>> LoadUsersFromFileAsync(string tenant)
    {
        var filePath = GetUsersFilePath(tenant);
        if (!File.Exists(filePath))
        {
            return new List<UserStatus>();
        }

        await _fileLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<UserStatus>>(json) ?? new List<UserStatus>();
        }
        catch
        {
            return new List<UserStatus>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveUsersToFileAsync(string tenant, List<UserStatus> users)
    {
        var filePath = GetUsersFilePath(tenant);

        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<UserStatus>> LoadAllUsersFromFilesAsync()
    {
        if (!Directory.Exists(_dataDir))
        {
            return new List<UserStatus>();
        }

        var files = Directory.GetFiles(_dataDir, "users-*.json");
        var allUsers = new List<UserStatus>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var users = JsonSerializer.Deserialize<List<UserStatus>>(json);
                if (users != null)
                {
                    allUsers.AddRange(users);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return allUsers;
    }

    public async Task RecordActivityAsync(GitActivity activity)
    {
        var tenant = (activity.Tenant ?? "default").ToLowerInvariant();
        var users = await LoadUsersFromFileAsync(tenant);
        var id = $"{tenant}::{activity.UserEmail.ToLowerInvariant()}";

        var user = users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            user = new UserStatus
            {
                Id = id,
                UserName = activity.UserName,
                UserEmail = activity.UserEmail,
                LastActivity = activity.Timestamp,
                Activities = new Dictionary<string, RepoActivity>(),
                Tenant = tenant
            };
            users.Add(user);
        }

        // Update user info
        user.UserName = activity.UserName;
        user.LastActivity = activity.Timestamp;

        // Key by repo+machine
        var activityKey = $"{activity.RepoName}::{activity.MachineName}";
        user.Activities[activityKey] = new RepoActivity
        {
            RepoName = activity.RepoName,
            Branch = activity.Branch,
            RemoteUrl = activity.RemoteUrl,
            ModifiedFiles = activity.ModifiedFiles,
            LastUpdated = activity.Timestamp,
            MachineName = activity.MachineName
        };

        await SaveUsersToFileAsync(tenant, users);
    }

    public async Task<List<UserStatus>> GetAllUsersAsync(string? tenant = null)
    {
        List<UserStatus> users;

        if (tenant != null)
        {
            users = await LoadUsersFromFileAsync(tenant.ToLowerInvariant());
        }
        else
        {
            users = await LoadAllUsersFromFilesAsync();
        }

        return users.OrderByDescending(u => DateTime.Parse(u.LastActivity)).ToList();
    }

    public async Task<List<UserStatus>> GetActiveUsersAsync(string? tenant = null)
    {
        var users = await GetAllUsersAsync(tenant);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return users.Where(user =>
        {
            var lastActivityTime = DateTime.Parse(user.LastActivity);
            var diffMs = now - new DateTimeOffset(lastActivityTime).ToUnixTimeMilliseconds();
            return diffMs < ActivityExpiryMs;
        }).ToList();
    }

    public bool IsUserActive(UserStatus user)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var lastActivityTime = DateTime.Parse(user.LastActivity);
        var diffMs = now - new DateTimeOffset(lastActivityTime).ToUnixTimeMilliseconds();
        return diffMs < ActivityExpiryMs;
    }

    public string GetTimeSinceActivity(string timestamp)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activityTime = DateTime.Parse(timestamp);
        var diffMs = now - new DateTimeOffset(activityTime).ToUnixTimeMilliseconds();

        var minutes = (int)(diffMs / 60000);
        var hours = (int)(diffMs / 3600000);
        var days = (int)(diffMs / 86400000);

        if (minutes < 1) return "just now";
        if (minutes < 60) return $"{minutes}m ago";
        if (hours < 24) return $"{hours}h ago";
        return $"{days}d ago";
    }

    public Task DeleteTenantAsync(string tenant)
    {
        var filePath = GetUsersFilePath(tenant.ToLowerInvariant());
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}
