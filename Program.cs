using System.Diagnostics;
using System.Text.Json;

namespace TGit;

class Program
{
    // Configure your API endpoint via TGIT_API_URL environment variable
    // Default points to local development server
    private static readonly string ApiEndpoint = Environment.GetEnvironmentVariable("TGIT_API_URL") 
        ?? "https://tgit.azurewebsites.net/api/git-activity";
        //"http://localhost:4321/api/git-activity";
    
    private static readonly HttpClient HttpClient = new();

    static async Task<int> Main(string[] args)
    {
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
            RedirectStandardInput = false
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
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('"') && !arg.Contains('\\')) return arg;
        
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static bool ShouldTrackCommand(string[] args)
    {
        if (args.Length == 0) return false;

        // Track commands that modify files or change state, plus status to capture current state
        var trackableCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "status", "add", "commit", "checkout", "switch", "restore", "reset",
            "merge", "rebase", "cherry-pick", "revert", "stash",
            "pull", "push", "fetch", "clone"
        };

        return trackableCommands.Contains(args[0]);
    }

    private static async Task SendTrackingInfoAsync()
    {
        try
        {
            var trackingInfo = await GatherTrackingInfo();
            if (trackingInfo == null) return;

            var json = JsonSerializer.Serialize(trackingInfo, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

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
        if (string.IsNullOrEmpty(repoRoot)) return null;

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
            MachineName = Environment.MachineName
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
            foreach (var line in stagedOutput.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2)
                {
                    files.Add(new FileEditInfo
                    {
                        FilePath = parts[1].Trim(),
                        Status = ParseStatus(parts[0]),
                        IsStaged = true
                    });
                }
            }
        }

        // Get unstaged modified files
        var unstagedOutput = await GetGitOutput("diff", "--name-status");
        if (!string.IsNullOrEmpty(unstagedOutput))
        {
            foreach (var line in unstagedOutput.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2)
                {
                    files.Add(new FileEditInfo
                    {
                        FilePath = parts[1].Trim(),
                        Status = ParseStatus(parts[0]),
                        IsStaged = false
                    });
                }
            }
        }

        // Get untracked files
        var untrackedOutput = await GetGitOutput("ls-files", "--others", "--exclude-standard");
        if (!string.IsNullOrEmpty(untrackedOutput))
        {
            foreach (var line in untrackedOutput.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                files.Add(new FileEditInfo
                {
                    FilePath = line.Trim(),
                    Status = "Untracked",
                    IsStaged = false
                });
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
            _ => statusCode
        };
    }

    private static async Task<string?> GetGitOutput(params string[] args)
    {
        try
        {
            var gitPath = FindGitExecutable();
            if (gitPath == null) return null;

            var startInfo = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

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
}

public class FileEditInfo
{
    public string FilePath { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsStaged { get; set; }
}
