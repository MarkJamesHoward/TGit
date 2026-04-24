using System.Net.Http;

namespace TGit;

class Program
{
    // Configure your API base URL via TGIT_CLI_API_URL environment variable
    // Default points to Azure production
    private static readonly string ApiBaseUrl =
        Environment.GetEnvironmentVariable("TGIT_CLI_API_URL")
        ?? "https://tgit-api.azurewebsites.net";

    private static readonly string ApiEndpoint = $"{ApiBaseUrl.TrimEnd('/')}/api/GitActivity";

    private static readonly HttpClient HttpClient = new();

    // Config file location
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".tgit"
    );
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    private static readonly GitOperationsService GitOperations = new();
    private static readonly ConfigService ConfigService = new(
        ConfigDir,
        ConfigFile,
        ApiEndpoint,
        HttpClient,
        GitOperations
    );
    private static readonly TrackingService TrackingService = new(
        ApiEndpoint,
        HttpClient,
        GitOperations,
        ConfigService
    );
    private static readonly HelpService HelpService = new(
        ConfigService,
        () => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.1.1"
    );

    static async Task<int> Main(string[] args)
    {
        // Handle tgit --version
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            Console.WriteLine($"tgit {version}");
            return 0;
        }

        // Handle tgit --help or tgit help
        if (
            args.Length == 0
            || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h" || args[0] == "help"))
        )
        {
            HelpService.PrintHelp();
            return 0;
        }

        // Handle tgit --clear
        if (args.Length == 1 && args[0] == "--clear")
        {
            return await ConfigService.HandleClearCommandAsync();
        }

        // Handle tgit --reset-config
        if (args.Length == 1 && args[0] == "--reset-config")
        {
            if (File.Exists(ConfigService.ConfigFilePath))
            {
                File.Delete(ConfigService.ConfigFilePath);
                Console.WriteLine("Config cleared. A new config will be generated on next run.");
            }
            else
            {
                Console.WriteLine("No config file found.");
            }
            return 0;
        }

        // Handle tgit --config commands
        if (args.Length >= 1 && args[0].Equals("--config", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigService.HandleConfigCommand(args.Skip(1).ToArray());
        }

        // Pass through all arguments to git
        var exitCode = await GitOperations.ExecuteGitCommandAsync(args);

        // After git command completes, send tracking info for relevant commands
        if (GitOperations.ShouldTrackCommand(args))
        {
            await TrackingService.SendTrackingInfoAsync();
        }

        return exitCode;
    }
}
