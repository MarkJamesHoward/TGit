# TGit - Git Wrapper with Activity Tracking

TGit is a CLI application that wraps Git commands, passing them through to Git while also sending activity tracking information to a configurable API endpoint. It includes a web dashboard to visualize team activity.

## Project Structure

```
TGit/
├── Program.cs          # CLI wrapper application
├── TGit.csproj         # .NET project file
├── web/                # Astro.js web dashboard
│   ├── src/
│   │   ├── pages/      # Dashboard and API endpoints
│   │   ├── components/ # UI components
│   │   └── lib/        # Data store
│   └── package.json
└── README.md
```

## Features

- **Full Git Passthrough**: All Git commands work exactly as expected
- **Activity Tracking**: Sends information about file changes to your tracking API
- **Non-blocking**: API calls are async and won't slow down your workflow
- **Configurable**: Set your API endpoint via environment variable
- **Web Dashboard**: Visualize up to 100+ users and their git activity in real-time

## Installation

### CLI Tool

```bash
# Build and install as global tool
cd TGit
dotnet pack -c Release
dotnet tool install --global --add-source ./bin/Release TGit
```

### Web Dashboard

```bash
cd web
npm install
npm run dev      # Development server on http://localhost:4321
npm run build    # Production build
node dist/server/entry.mjs  # Run production server
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `TGIT_API_URL` | The API endpoint to send tracking data | `http://localhost:4321/api/git-activity` |
| `TGIT_DEBUG` | Set to `1` to enable debug output | Not set |

### Setting the API Endpoint

**Windows (PowerShell):**
```powershell
$env:TGIT_API_URL = "https://your-server.com/api/git-activity"
```

**Windows (Command Prompt):**
```cmd
set TGIT_API_URL=https://your-server.com/api/git-activity
```

**Linux/macOS:**
```bash
export TGIT_API_URL="https://your-server.com/api/git-activity"
```

## Usage

Use `tgit` exactly like you would use `git`:

```bash
tgit status
tgit add .
tgit commit -m "Your message"
tgit push
```

## Tracked Commands

The following commands trigger API notifications:
- `add`, `commit`, `checkout`, `switch`, `restore`, `reset`
- `merge`, `rebase`, `cherry-pick`, `revert`, `stash`
- `pull`, `push`, `fetch`, `clone`

## API Payload

The tracking API receives a JSON payload with the following structure:

```json
{
  "timestamp": "2026-01-18T12:00:00Z",
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
  "machineName": "DESKTOP-ABC123"
}
```

### File Status Values

- `Added` - New file added to the repository
- `Modified` - Existing file was modified
- `Deleted` - File was deleted
- `Renamed` - File was renamed
- `Copied` - File was copied
- `Unmerged` - File has merge conflicts
- `Untracked` - New file not yet tracked by Git

## Example API Server

Here's a minimal example of an API server to receive the tracking data:

### Node.js (Express)

```javascript
const express = require('express');
const app = express();
app.use(express.json());

app.post('/api/git-activity', (req, res) => {
  console.log('Git activity received:', req.body);
  // Store in database, send notifications, etc.
  res.status(200).json({ success: true });
});

app.listen(3000, () => console.log('Server running on port 3000'));
```

### ASP.NET Core

```csharp
app.MapPost("/api/git-activity", (GitTrackingInfo info) =>
{
    // Process the tracking info
    return Results.Ok(new { success = true });
});
```

## License

MIT
