# TGit - Git Wrapper with Activity Tracking

[![CLI Tests](https://github.com/MarkJamesHoward/TGit/actions/workflows/cli-tests.yml/badge.svg)](https://github.com/MarkJamesHoward/TGit/actions/workflows/cli-tests.yml)

TGit is a CLI tool that wraps Git commands, passing them through to Git while sending activity tracking data to an API. It includes a real-time web dashboard to visualize team activity, with tenant isolation for multi-team support.

## Who Benefits from TGit

1. **Single user on a single machine with multiple repositories**
  - If you regularly switch between repos, TGit helps you stay tidy by showing repo state and recent activity.
  - This makes it easier to decide what to commit, what to clean up, and when to remove temporary/test branches.

2. **Single user across multiple machines**
  - TGit provides the same visibility across your devices (for example, laptop and desktop).
  - Before changing files, you can see where work already happened, reducing duplicate work and merge conflicts.

3. **Teams in a company (including managers)**
  - Developers can share a tenant and see who is working on what in real time.
  - Managers get a live, lightweight view of current team activity across repositories.

## Project Structure

```
TGit/
├── cli/                    # CLI application
│   ├── Program.cs          # CLI wrapper entrypoint
│   └── TGit.csproj         # .NET project file (CLI)
├── api/                    # ASP.NET Core Web API
│   ├── Controllers/        # API endpoints
│   ├── Services/           # Storage services (JSON, Cosmos DB)
│   ├── Models/             # Data models
│   └── TGitApi.csproj
├── dashboard/              # Astro.js static web dashboard
│   ├── src/pages/          # Dashboard UI
│   └── package.json
├── winget/                 # Winget package manifests
└── .github/workflows/     # CI/CD
    ├── release.yml         # CLI release (tag-triggered)
    ├── azure-api-deploy.yml    # API deploy to Azure App Service
    └── dashboard.yml       # Dashboard deploy to Azure Static Web App
```

## Architecture

```
┌─────────┐     POST /api/GitActivity     ┌─────────────┐
│  tgit   │ ──────────────────────────────>│   API       │
│  CLI    │                                │  (Azure     │
└─────────┘                                │  App Svc)   │
                                           └──────┬──────┘
┌─────────────┐  GET /api/Users                   │
│  Dashboard  │ <─────────────────────────────────┘
│  (Azure     │   (polls every 2 seconds)
│  Static App)│
└─────────────┘
```

- **CLI** (`tgit`) — wraps git commands, sends activity data to the API
- **API** (`tgit-api.azurewebsites.net`) — ASP.NET Core API, stores data in JSON files or Azure SQL
- **Dashboard** (`tgit.app`) — Astro site, polls the API and displays team activity

## Installation

### CLI Tool

#### Install via winget (recommended)

```powershell
winget install MarkJamesHoward.TGit
```

#### Or build and install manually

```bash
cd TGit/cli
dotnet pack -c Release -o ../nupkg
dotnet tool install --global --add-source ../nupkg TGit
```

## Usage

Use `tgit` exactly like you would use `git`:

```bash
tgit status
tgit add .
tgit commit -m "Your message"
tgit push
```

### TGit Commands

```
tgit --config                    Show current configuration
tgit --config tenant             Show current tenant ID
tgit --config tenant <name>      Set tenant ID for data isolation
tgit --clear                   Delete all tracking data for your tenant
tgit --help                    Show help message
tgit --version                 Show version information
```

### Tenant Configuration

Each TGit installation gets a unique tenant ID on first run. All users sharing the same tenant ID can see each other's activity on the dashboard.

```bash
# Set a shared tenant for your team
tgit --config tenant mycompany

# View your current tenant
tgit --config tenant
```

### Clearing Data

To delete all tracked data for your tenant:

```bash
tgit --clear
```

### Tracked Commands

The following git commands trigger activity tracking:
`status`, `add`, `commit`, `checkout`, `switch`, `restore`, `reset`, `merge`, `rebase`, `cherry-pick`, `revert`, `stash`, `pull`, `push`, `fetch`, `clone`

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `TGIT_API_URL` | API endpoint for tracking data | `https://tgit-api.azurewebsites.net/api/GitActivity` |
| `TGIT_TENANT` | Override tenant ID (takes precedence over config) | Auto-generated |
| `TGIT_DEBUG` | Set to `1` to enable debug output | Not set |

## Dashboard

View your team's activity at [tgit.app](https://tgit.app). Enter your tenant ID to see activity for your team.

The dashboard polls the API every 2 seconds and shows:
- Active users and their current branches
- Modified files per repository
- Time since last activity

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/GitActivity` | Record git activity |
| `DELETE` | `/api/GitActivity?tenant=xxx` | Delete all data for a tenant |
| `GET` | `/api/Users?tenant=xxx` | Get all users for a tenant |
| `GET` | `/api/Users?tenant=xxx&active=true` | Get active users only |
| `GET` | `/swagger` | Swagger UI |

### Activity Payload

```json
{
  "timestamp": "2026-01-30T12:00:00Z",
  "userName": "John Doe",
  "userEmail": "john@example.com",
  "repoName": "my-project",
  "branch": "main",
  "remoteUrl": "https://github.com/user/my-project.git",
  "modifiedFiles": [
    {
      "filePath": "src/file.cs",
      "status": "Modified",
      "isStaged": true
    }
  ],
  "machineName": "DESKTOP-ABC123",
  "tenant": "mycompany"
}
```

## Deployment

### API (Azure App Service)

Deployed automatically via GitHub Actions when files in `api/` change on `main`.

**App Settings:**
| Setting | Description |
|---------|-------------|
| `Storage__Type` | `json` (default) or `cosmos` |
| `Storage__DataDir` | Path for JSON storage (use `/home/data` for persistence on Azure) |
| `Cosmos__Endpoint` | Cosmos DB endpoint (when using Cosmos storage) |
| `Cosmos__Key` | Cosmos DB key (when using Cosmos storage) |

### Dashboard (Azure Static Web App)

Deployed automatically via GitHub Actions when files in `dashboard/` change on `main`.

**Build-time environment variable:**
- `PUBLIC_API_BASE_URL` — the API base URL (e.g., `https://tgit-api.azurewebsites.net`)

### CLI Release

Tag a version to trigger a release:

```bash
git tag v1.4.0
git push origin v1.4.0
```

This builds win-x64 and win-arm64 binaries, creates a GitHub release, and submits to winget.

## Terraform (Azure)

For Terraform in Docker, set these values in [.terraform/azure_secrets.txt](.terraform/azure_secrets.txt):

- `ARM_CLIENT_ID`
- `ARM_CLIENT_SECRET`
- `ARM_TENANT_ID`
- `ARM_SUBSCRIPTION_ID`

Then run the commands in [.terraform/docker run details.txt](.terraform/docker%20run%20details.txt) to execute `init`, `validate`, `plan`, and `apply` with `--env-file`.

