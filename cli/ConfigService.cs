using System.Diagnostics;
using System.Text.Json;

namespace TGit;

internal class ConfigService
{
    private readonly string _configDir;
    private readonly string _apiEndpoint;
    private readonly HttpClient _httpClient;
    private readonly GitOperationsService _gitOperations;

    public string ConfigFilePath { get; }

    public ConfigService(
        string configDir,
        string configFilePath,
        string apiEndpoint,
        HttpClient httpClient,
        GitOperationsService gitOperations
    )
    {
        _configDir = configDir;
        ConfigFilePath = configFilePath;
        _apiEndpoint = apiEndpoint;
        _httpClient = httpClient;
        _gitOperations = gitOperations;
    }

    public int HandleConfigCommand(string[] args)
    {
        if (args.Length == 0)
        {
            var config = LoadConfig();
            Console.WriteLine($"tenant = {config.Tenant}");
            return 0;
        }

        if (args.Length == 1 && args[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
        {
            var config = LoadConfig();
            Console.WriteLine(config.Tenant);
            return 0;
        }

        if (args.Length == 2 && args[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
        {
            var config = LoadConfig();
            config.Tenant = args[1].ToLowerInvariant().Trim();
            SaveConfig(config);
            Console.WriteLine($"Tenant set to: {config.Tenant}");
            return 0;
        }

        Console.WriteLine("Usage: tgit --config tenant [company-name]");
        Console.WriteLine("  tgit --config              - Show all config");
        Console.WriteLine("  tgit --config tenant       - Show current tenant");
        Console.WriteLine("  tgit --config tenant acme  - Set tenant to 'acme'");
        return 1;
    }

    public TGitConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<TGitConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.Tenant))
                {
                    return config;
                }
            }
        }
        catch { }

        var newConfig = new TGitConfig { Tenant = GenerateUniqueTenantId() };
        SaveConfig(newConfig);
        return newConfig;
    }

    public string GetTenant()
    {
        var envTenant = Environment.GetEnvironmentVariable("TGIT_TENANT");
        if (!string.IsNullOrEmpty(envTenant))
            return envTenant.ToLowerInvariant();

        return LoadConfig().Tenant;
    }

    public async Task<int> HandleClearCommandAsync()
    {
        var tenant = GetTenant();
        Console.WriteLine($"Deleting all data for tenant: {tenant}");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.DeleteAsync(
                $"{_apiEndpoint}?tenant={tenant}",
                cts.Token
            );

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Data deleted successfully.");
                return 0;
            }

            Console.Error.WriteLine($"Failed to delete data. Status: {response.StatusCode}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private string GenerateUniqueTenantId()
    {
        var email = GetGitEmailSync();
        if (!string.IsNullOrEmpty(email))
        {
            var sanitized = new string(
                email
                    .ToLowerInvariant()
                    .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '@')
                    .ToArray()
            );
            if (!string.IsNullOrEmpty(sanitized))
                return sanitized;
        }

        var machinePart = Environment.MachineName.ToLowerInvariant();
        machinePart = new string(
            machinePart.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()
        );
        if (machinePart.Length > 12)
            machinePart = machinePart[..12];

        var randomSuffix = Guid.NewGuid().ToString("N")[..6];
        return $"{machinePart}-{randomSuffix}";
    }

    private string? GetGitEmailSync()
    {
        try
        {
            var gitPath = _gitOperations.FindGitExecutable();
            if (gitPath == null)
                return null;

            var startInfo = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = "config user.email",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            var process = Process.Start(startInfo);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private void SaveConfig(TGitConfig config)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(ConfigFilePath, json);
    }
}
