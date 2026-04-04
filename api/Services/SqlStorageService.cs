using Microsoft.EntityFrameworkCore;
using TGitApi.Data;
using TGitApi.Data.Entities;
using TGitApi.Models;

namespace TGitApi.Services;

public class SqlStorageService : IStorageService
{
    private readonly IDbContextFactory<TGitDbContext> _dbFactory;
    private const int ActivityExpiryMs = 30 * 60 * 1000; // 30 minutes

    public SqlStorageService(IDbContextFactory<TGitDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task RecordActivityAsync(GitActivity activity)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var tenant = (activity.Tenant ?? "default").ToLowerInvariant();
        var id = $"{tenant}::{activity.UserEmail.ToLowerInvariant()}";
        var activityKey = $"{activity.RepoName}::{activity.MachineName}";

        var user = await db.Users
            .Include(u => u.Activities.Where(a => a.ActivityKey == activityKey))
                .ThenInclude(a => a.ModifiedFiles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            user = new UserEntity
            {
                Id = id,
                UserName = activity.UserName,
                UserEmail = activity.UserEmail.ToLowerInvariant(),
                LastActivity = activity.Timestamp,
                Tenant = tenant
            };
            db.Users.Add(user);
        }
        else
        {
            user.UserName = activity.UserName;
            user.LastActivity = activity.Timestamp;
        }

        var repoActivity = user.Activities.FirstOrDefault(a => a.ActivityKey == activityKey);
        if (repoActivity != null)
        {
            repoActivity.RepoName = activity.RepoName;
            repoActivity.Branch = activity.Branch;
            repoActivity.RemoteUrl = activity.RemoteUrl;
            repoActivity.LastUpdated = activity.Timestamp;
            repoActivity.MachineName = activity.MachineName;
            db.FileEdits.RemoveRange(repoActivity.ModifiedFiles);
            repoActivity.ModifiedFiles.Clear();
        }
        else
        {
            repoActivity = new RepoActivityEntity
            {
                UserId = id,
                ActivityKey = activityKey,
                RepoName = activity.RepoName,
                Branch = activity.Branch,
                RemoteUrl = activity.RemoteUrl,
                LastUpdated = activity.Timestamp,
                MachineName = activity.MachineName
            };
            user.Activities.Add(repoActivity);
        }

        foreach (var file in activity.ModifiedFiles)
        {
            repoActivity.ModifiedFiles.Add(new FileEditEntity
            {
                FilePath = file.FilePath,
                Status = file.Status,
                IsStaged = file.IsStaged
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<UserStatus>> GetAllUsersAsync(string? tenant = null)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Users
            .Include(u => u.Activities)
                .ThenInclude(a => a.ModifiedFiles)
            .AsQueryable();

        if (tenant != null)
            query = query.Where(u => u.Tenant == tenant.ToLowerInvariant());

        query = query.OrderByDescending(u => u.LastActivity);

        var entities = await query.AsNoTracking().ToListAsync();
        return entities.Select(MapToUserStatus).ToList();
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

    public async Task DeleteTenantAsync(string tenant)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var users = await db.Users
            .Where(u => u.Tenant == tenant.ToLowerInvariant())
            .ToListAsync();
        db.Users.RemoveRange(users);
        await db.SaveChangesAsync();
    }

    private static UserStatus MapToUserStatus(UserEntity entity)
    {
        var activities = new Dictionary<string, RepoActivity>();
        foreach (var a in entity.Activities)
        {
            activities[a.ActivityKey] = new RepoActivity
            {
                RepoName = a.RepoName,
                Branch = a.Branch,
                RemoteUrl = a.RemoteUrl,
                ModifiedFiles = a.ModifiedFiles.Select(f => new FileEditInfo
                {
                    FilePath = f.FilePath,
                    Status = f.Status,
                    IsStaged = f.IsStaged
                }).ToList(),
                LastUpdated = a.LastUpdated,
                MachineName = a.MachineName
            };
        }
        return new UserStatus
        {
            Id = entity.Id,
            UserName = entity.UserName,
            UserEmail = entity.UserEmail,
            LastActivity = entity.LastActivity,
            Activities = activities,
            Tenant = entity.Tenant
        };
    }
}
