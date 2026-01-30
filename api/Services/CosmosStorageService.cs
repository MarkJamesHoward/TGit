using Microsoft.Azure.Cosmos;
using TGitApi.Models;

namespace TGitApi.Services;

public class CosmosStorageService : IStorageService
{
    private readonly CosmosClient _client;
    private readonly Container _container;
    private const int ActivityExpiryMs = 30 * 60 * 1000; // 30 minutes

    public CosmosStorageService(IConfiguration configuration)
    {
        var endpoint = configuration["Cosmos:Endpoint"] ?? throw new InvalidOperationException("Cosmos endpoint not configured");
        var key = configuration["Cosmos:Key"] ?? throw new InvalidOperationException("Cosmos key not configured");
        var databaseId = configuration["Cosmos:Database"] ?? "tgit";
        var containerId = configuration["Cosmos:Container"] ?? "users";

        _client = new CosmosClient(endpoint, key);
        _container = InitializeAsync(databaseId, containerId).GetAwaiter().GetResult();
    }

    private async Task<Container> InitializeAsync(string databaseId, string containerId)
    {
        var database = await _client.CreateDatabaseIfNotExistsAsync(databaseId);
        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = containerId,
                PartitionKeyPath = "/userEmail"
            }
        );
        return containerResponse.Container;
    }

    public async Task RecordActivityAsync(GitActivity activity)
    {
        var tenant = (activity.Tenant ?? "default").ToLowerInvariant();
        var id = $"{tenant}::{activity.UserEmail.ToLowerInvariant()}";
        var partitionKey = new PartitionKey(activity.UserEmail.ToLowerInvariant());

        UserStatus user;
        try
        {
            var response = await _container.ReadItemAsync<UserStatus>(id, partitionKey);
            user = response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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

        // Upsert to Cosmos DB
        await _container.UpsertItemAsync(user, partitionKey);
    }

    public async Task<List<UserStatus>> GetAllUsersAsync(string? tenant = null)
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);

        if (tenant != null)
        {
            query += " WHERE c.tenant = @tenant";
            queryDefinition = new QueryDefinition(query)
                .WithParameter("@tenant", tenant.ToLowerInvariant());
        }

        query += " ORDER BY c.lastActivity DESC";
        queryDefinition = new QueryDefinition(query);

        if (tenant != null)
        {
            queryDefinition = queryDefinition.WithParameter("@tenant", tenant.ToLowerInvariant());
        }

        var users = new List<UserStatus>();
        using var iterator = _container.GetItemQueryIterator<UserStatus>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            users.AddRange(response);
        }

        return users;
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
        var users = await GetAllUsersAsync(tenant.ToLowerInvariant());
        foreach (var user in users)
        {
            var partitionKey = new PartitionKey(user.UserEmail.ToLowerInvariant());
            await _container.DeleteItemAsync<UserStatus>(user.Id, partitionKey);
        }
    }
}
