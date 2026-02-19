namespace TGit;

internal class HelpService
{
    private readonly ConfigService _configService;
    private readonly Func<string> _versionProvider;

    public HelpService(ConfigService configService, Func<string> versionProvider)
    {
        _configService = configService;
        _versionProvider = versionProvider;
    }

    public void PrintHelp()
    {
        var config = _configService.LoadConfig();
        var version = _versionProvider();

        Console.WriteLine(
            $@"
TGit - Git CLI wrapper with activity tracking

Version: {version}
Tenant:  {config.Tenant}

TGIT COMMANDS:
  tgit --config                  Show current configuration
  tgit --config tenant           Show current tenant ID
  tgit --config tenant <name>    Set tenant ID for data isolation

  tgit --clear                   Delete all tracking data for your tenant
  tgit --reset-config            Clear local config (tenant regenerated on next run)
  tgit --help, -h, help          Show this help message
  tgit --version, -v             Show version

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
