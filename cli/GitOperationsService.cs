using System.Diagnostics;

namespace TGit;

internal class GitOperationsService
{
    public async Task<int> ExecuteGitCommandAsync(string[] args)
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

    public string? FindGitExecutable()
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

    public string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";
        if (!arg.Contains(' ') && !arg.Contains('"') && !arg.Contains('\\'))
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    public bool ShouldTrackCommand(string[] args)
    {
        if (args.Length == 0)
            return false;

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

    public async Task<string?> GetRepoNameAsync()
    {
        var remoteUrl = await GetGitOutputAsync("config", "--get", "remote.origin.url");
        if (!string.IsNullOrEmpty(remoteUrl))
        {
            return ExtractRepoNameFromUrl(remoteUrl);
        }

        var repoRoot = await GetGitOutputAsync("rev-parse", "--show-toplevel");
        if (!string.IsNullOrEmpty(repoRoot))
        {
            return Path.GetFileName(repoRoot.TrimEnd('/', '\\'));
        }

        return null;
    }

    public string ExtractRepoNameFromUrl(string url)
    {
        var name = url.TrimEnd('/');

        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        var lastSlash = name.LastIndexOfAny(['/', ':']);
        if (lastSlash >= 0)
        {
            name = name[(lastSlash + 1)..];
        }

        return name;
    }

    public async Task<List<FileEditInfo>> GetModifiedFilesAsync()
    {
        var files = new List<FileEditInfo>();

        var stagedOutput = await GetGitOutputAsync("diff", "--cached", "--name-status");
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

        var unstagedOutput = await GetGitOutputAsync("diff", "--name-status");
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

        var untrackedOutput = await GetGitOutputAsync("ls-files", "--others", "--exclude-standard");
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

    public string ParseStatus(string statusCode)
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

    public async Task<string?> GetGitOutputAsync(params string[] args)
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
