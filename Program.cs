using System.Diagnostics;
using System.Text.Json;

namespace TGit;

partial class Program
{
    // Configure your API endpoint via TGIT_API_URL environment variable
    // Default points to Azure production
    private static readonly string ApiEndpoint =
        Environment.GetEnvironmentVariable("TGIT_API_URL")
        // ?? "https://tgit-cjcgafe3fbbgb3d3.newzealandnorth-01.azurewebsites.net/api/git-activity";
        ?? "https://tgit.app/api/git-activity";

    private static readonly HttpClient HttpClient = new();

    // Config file location
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".tgit"
    );
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    static async Task<int> Main(string[] args)
    {
        // Handle tgit --help or tgit help
        if (
            args.Length == 0
            || (
                args.Length == 1
                && (
                    args[0] == "--help"
                    || args[0] == "-h"
                    || args[0] == "help"
                    || args[0] == "--version"
                    || args[0] == "-v"
                )
            )
        )
        {
            PrintHelp();
            return 0;
        }

        // Handle tgit config commands
        if (args.Length >= 1 && args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            return HandleConfigCommand(args.Skip(1).ToArray());
        }

        // Pass through all arguments to git
        var exitCode = await ExecuteGitCommand(args);

        // After git command completes, send tracking info for relevant commands
        if (ShouldTrackCommand(args))
        {
            await SendTrackingInfoAsync();
        }

        return exitCode;
    }

    private static async Task<int> ExecuteGitCommand(string[] args)
    {
        var gitPath = FindGitExecutable();
        if (gitPath == null)
        {
            Console.Error.WriteLine("Error: Git executable not found in PATH");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = gitPath,
            Arguments = string.Join(" ", args.Select(EscapeArgument)),
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            RedirectStandardInput = false,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Console.Error.WriteLine("Error: Failed to start git process");
            return 1;
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string? FindGitExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);

        var gitNames = OperatingSystem.IsWindows()
            ? new[] { "git.exe", "git.cmd", "git.bat" }
            : new[] { "git" };

        foreach (var path in paths)
        {
            foreach (var gitName in gitNames)
            {
                var fullPath = Path.Combine(path, gitName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('"') && !arg.Contains('\\'))
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static bool ShouldTrackCommand(string[] args)
    {
        if (args.Length == 0)
            return false;

        // Track commands that modify files or change state, plus status to capture current state
        var trackableCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "status",
            "add",
            "commit",
            "checkout",
            "switch",
            "restore",
            "reset",
            "merge",
            "rebase",
            "cherry-pick",
            "revert",
            "stash",
            "pull",
            "push",
            "fetch",
            "clone",
        };

        return trackableCommands.Contains(args[0]);
    }

    private static async Task SendTrackingInfoAsync()
    {
        try
        {
            var trackingInfo = await GatherTrackingInfo();
            if (trackingInfo == null)
                return;

            var json = JsonSerializer.Serialize(
                trackingInfo,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                }
            );

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Send async and don't wait too long - we don't want to block the user
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                await HttpClient.PostAsync(ApiEndpoint, content, cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Timeout - silently ignore to not affect user experience
            }
            catch (HttpRequestException)
            {
                // Network error - silently ignore
            }
        }
        catch (Exception ex)
        {
            // Log errors but don't fail the git command
            if (Environment.GetEnvironmentVariable("TGIT_DEBUG") == "1")
            {
                Console.Error.WriteLine($"[TGit Debug] Error sending tracking info: {ex.Message}");
            }
        }
    }

    private static async Task<GitTrackingInfo?> GatherTrackingInfo()
    {
        var repoRoot = await GetGitOutput("rev-parse", "--show-toplevel");
        if (string.IsNullOrEmpty(repoRoot))
            return null;

        var userName = await GetGitOutput("config", "user.name");
        var userEmail = await GetGitOutput("config", "user.email");
        var repoName = await GetRepoName();
        var branch = await GetGitOutput("rev-parse", "--abbrev-ref", "HEAD");
        var modifiedFiles = await GetModifiedFiles();
        var remoteUrl = await GetGitOutput("config", "--get", "remote.origin.url");

        return new GitTrackingInfo
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName ?? "unknown",
            UserEmail = userEmail ?? "unknown",
            RepoName = repoName ?? "unknown",
            Branch = branch ?? "unknown",
            RemoteUrl = remoteUrl,
            ModifiedFiles = modifiedFiles,
            MachineName = Environment.MachineName,
            Tenant = GetTenant(),
        };
    }

    private static async Task<string?> GetRepoName()
    {
        // Try to get repo name from remote URL first
        var remoteUrl = await GetGitOutput("config", "--get", "remote.origin.url");
        if (!string.IsNullOrEmpty(remoteUrl))
        {
            return ExtractRepoNameFromUrl(remoteUrl);
        }

        // Fall back to directory name
        var repoRoot = await GetGitOutput("rev-parse", "--show-toplevel");
        if (!string.IsNullOrEmpty(repoRoot))
        {
            return Path.GetFileName(repoRoot.TrimEnd('/', '\\'));
        }

        return null;
    }

    private static string ExtractRepoNameFromUrl(string url)
    {
        // Handle various git URL formats:
        // https://github.com/user/repo.git
        // git@github.com:user/repo.git
        // ssh://git@github.com/user/repo.git

        var name = url.TrimEnd('/');

        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        // Get the last path segment
        var lastSlash = name.LastIndexOfAny(['/', ':']);
        if (lastSlash >= 0)
        {
            name = name[(lastSlash + 1)..];
        }

        return name;
    }

    private static async Task<List<FileEditInfo>> GetModifiedFiles()
    {
        var files = new List<FileEditInfo>();

        // Get staged files
        var stagedOutput = await GetGitOutput("diff", "--cached", "--name-status");
        if (!string.IsNullOrEmpty(stagedOutput))
        {
            foreach (
                var line in stagedOutput.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            )
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2)
                {
                    files.Add(
                        new FileEditInfo
                        {
                            FilePath = parts[1].Trim(),
                            Status = ParseStatus(parts[0]),
                            IsStaged = true,
                        }
                    );
                }
            }
        }

        // Get unstaged modified files
        var unstagedOutput = await GetGitOutput("diff", "--name-status");
        if (!string.IsNullOrEmpty(unstagedOutput))
        {
            foreach (
                var line in unstagedOutput.Split(
                    ['\n', '\r'],
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2)
                {
                    files.Add(
                        new FileEditInfo
                        {
                            FilePath = parts[1].Trim(),
                            Status = ParseStatus(parts[0]),
                            IsStaged = false,
                        }
                    );
                }
            }
        }

        // Get untracked files
        var untrackedOutput = await GetGitOutput("ls-files", "--others", "--exclude-standard");
        if (!string.IsNullOrEmpty(untrackedOutput))
        {
            foreach (
                var line in untrackedOutput.Split(
                    ['\n', '\r'],
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                files.Add(
                    new FileEditInfo
                    {
                        FilePath = line.Trim(),
                        Status = "Untracked",
                        IsStaged = false,
                    }
                );
            }
        }

        return files;
    }

    private static string ParseStatus(string statusCode)
    {
        return statusCode.Trim() switch
        {
            "A" => "Added",
            "M" => "Modified",
            "D" => "Deleted",
            "R" => "Renamed",
            "C" => "Copied",
            "U" => "Unmerged",
            "?" => "Untracked",
            _ => statusCode,
        };
    }

    private static async Task<string?> GetGitOutput(params string[] args)
    {
        try
        {
            var gitPath = FindGitExecutable();
            if (gitPath == null)
                return null;

            var startInfo = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}

public class GitTrackingInfo
{
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string RepoName { get; set; } = "";
    public string Branch { get; set; } = "";
    public string? RemoteUrl { get; set; }
    public List<FileEditInfo> ModifiedFiles { get; set; } = [];
    public string MachineName { get; set; } = "";
    public string Tenant { get; set; } = "default";
}

public class FileEditInfo
{
    public string FilePath { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsStaged { get; set; }
}

public class TGitConfig
{
    public string Tenant { get; set; } = "";
}

partial class Program
{
    private static int HandleConfigCommand(string[] args)
    {
        if (args.Length == 0)
        {
            // Show current config
            var config = LoadConfig();
            Console.WriteLine($"tenant = {config.Tenant}");
            return 0;
        }

        if (args.Length == 1 && args[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
        {
            // Show current tenant
            var config = LoadConfig();
            Console.WriteLine(config.Tenant);
            return 0;
        }

        if (args.Length == 2 && args[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
        {
            // Set tenant
            var config = LoadConfig();
            config.Tenant = args[1].ToLowerInvariant().Trim();
            SaveConfig(config);
            Console.WriteLine($"Tenant set to: {config.Tenant}");
            return 0;
        }

        Console.WriteLine("Usage: tgit config tenant [company-name]");
        Console.WriteLine("  tgit config              - Show all config");
        Console.WriteLine("  tgit config tenant       - Show current tenant");
        Console.WriteLine("  tgit config tenant acme  - Set tenant to 'acme'");
        return 1;
    }

    private static TGitConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<TGitConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.Tenant))
                {
                    return config;
                }
            }
        }
        catch { }

        // First run - generate a unique tenant ID and save it
        var newConfig = new TGitConfig { Tenant = GenerateUniqueTenantId() };
        SaveConfig(newConfig);
        return newConfig;
    }

    private static string GenerateUniqueTenantId()
    {
        // Generate a unique tenant ID based on machine name and a random suffix
        var machinePart = Environment.MachineName.ToLowerInvariant();
        // Sanitize machine name to only allow alphanumeric and hyphens
        machinePart = new string(
            machinePart.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()
        );
        if (machinePart.Length > 12)
            machinePart = machinePart[..12];

        // Add random suffix for uniqueness
        var randomSuffix = Guid.NewGuid().ToString("N")[..6];

        return $"{machinePart}-{randomSuffix}";
    }

    private static void SaveConfig(TGitConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(ConfigFile, json);
    }

    private static string GetTenant()
    {
        // Environment variable takes precedence
        var envTenant = Environment.GetEnvironmentVariable("TGIT_TENANT");
        if (!string.IsNullOrEmpty(envTenant))
            return envTenant.ToLowerInvariant();

        // Fall back to config file
        return LoadConfig().Tenant;
    }

    private static void PrintHelp()
    {
        var config = LoadConfig();
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.1.1";
        Console.WriteLine(
            $@"
TGit - Git CLI wrapper with activity tracking

Version: {version}
Tenant:  {config.Tenant}

TGIT COMMANDS:
  tgit config                    Show current configuration
  tgit config tenant             Show current tenant ID
  tgit config tenant <name>      Set tenant ID for data isolation

  tgit --help, -h, help          Show this help message
  tgit --version, -v             Show version information

GIT PASSTHROUGH:
  All other commands are passed directly to git with activity tracking.
  
  Examples:
    tgit status                  Run 'git status' and track activity
    tgit commit -m ""message""     Run 'git commit' and track activity
    tgit push                    Run 'git push' and track activity

ENVIRONMENT VARIABLES:
  TGIT_TENANT                    Override tenant ID (takes precedence over config)
  TGIT_API_URL                   Override API endpoint URL
  TGIT_DEBUG=1                   Enable debug output

DASHBOARD:
  View your activity at https://tgit.app
  Enter your tenant ID: {config.Tenant}

TRACKED COMMANDS:
  status, add, commit, checkout, switch, restore, reset,
  merge, rebase, cherry-pick, revert, stash, pull, push, fetch, clone
"
        );
    }
}
