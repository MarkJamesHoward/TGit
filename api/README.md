# TGit API - .NET C# WebAPI

A .NET C# WebAPI implementation for tracking Git activity across users and repositories.

## Features

- **POST /api/git-activity** - Record git activity for a user
- **GET /api/users** - Get all users or active users (with `?active=true` query parameter)
- Supports JSON file storage and Azure SQL Database
- CORS enabled for cross-origin requests
- OpenAPI/Swagger support in development

## Prerequisites

- .NET 9.0 SDK or later

## Configuration

The API can be configured via `appsettings.json` or environment variables.

### Storage Options

#### JSON File Storage (Default)

```json
{
  "Storage": {
    "Type": "json",
    "DataDir": "./storage"
  }
}
```

On **Azure App Service**, the storage path automatically defaults to `D:\home\data\tgit` (persistent across deploys).

#### Azure SQL Database Storage

```json
{
  "Storage": {
    "Type": "sql"
  },
  "Sql": {
    "ConnectionString": "Server=your-server.database.windows.net;Database=tgit;Authentication=Active Directory Default;Encrypt=true;TrustServerCertificate=false;"
  }
}
```

Tables are created automatically on first startup via EF Core.

### Environment Variables

You can also configure via environment variables:

- `Storage__Type` - "json" or "sql"
- `Storage__DataDir` - Directory for JSON files (default: "./storage" locally, `D:\home\data\tgit` on Azure App Service)
- `Sql__ConnectionString` - Azure SQL connection string

## Running the API

### Development

```bash
cd api
dotnet run
```

The API will be available at `http://localhost:5000`

### Production

```bash
cd api
dotnet build -c Release
dotnet run -c Release
```

## API Endpoints

### POST /api/git-activity

Records git activity for a user.

**Request Body:**
```json
{
  "timestamp": "2024-01-30T10:30:00Z",
  "userName": "John Doe",
  "userEmail": "john@example.com",
  "repoName": "my-project",
  "branch": "main",
  "remoteUrl": "https://github.com/user/repo.git",
  "modifiedFiles": [
    {
      "filePath": "src/index.js",
      "status": "modified",
      "isStaged": true
    }
  ],
  "machineName": "laptop-123",
  "tenant": "default"
}
```

**Response:**
```json
{
  "success": true
}
```

### GET /api/users

Gets all users or active users.

**Query Parameters:**
- `active` (boolean, optional) - Filter to show only active users (default: false)
- `tenant` (string, optional) - Filter by tenant

**Response:**
```json
{
  "users": [
    {
      "userName": "John Doe",
      "userEmail": "john@example.com",
      "lastActivity": "2024-01-30T10:30:00Z",
      "isActive": true,
      "activities": [
        {
          "repoName": "my-project",
          "branch": "main",
          "remoteUrl": "https://github.com/user/repo.git",
          "modifiedFiles": [...],
          "lastUpdated": "2024-01-30T10:30:00Z",
          "machineName": "laptop-123"
        }
      ]
    }
  ],
  "totalCount": 1,
  "activeCount": 1
}
```

## Project Structure

```
api/
├── Controllers/
│   ├── GitActivityController.cs
│   └── UsersController.cs
├── Models/
│   ├── ApiResponse.cs
│   ├── FileEditInfo.cs
│   ├── GitActivity.cs
│   ├── RepoActivity.cs
│   ├── UserStatus.cs
│   └── UsersResponse.cs
├── Data/
│   ├── TGitDbContext.cs
│   └── Entities/
│       ├── UserEntity.cs
│       ├── RepoActivityEntity.cs
│       └── FileEditEntity.cs
├── Services/
│   ├── IStorageService.cs
│   ├── JsonStorageService.cs
│   └── SqlStorageService.cs
├── Program.cs
└── appsettings.json
```

## Activity Expiry

- Users are considered **active** if they have had activity within the last **30 minutes**
- Old entries (older than 7 days) can be cleaned up (cleanup logic not yet implemented in this version)

## Development

### Build

```bash
dotnet build
```

### Test Endpoints

You can use the included `TGitApi.http` file with REST Client extensions, or use curl:

```bash
# Get all users
curl http://localhost:5000/api/users

# Get active users only
curl http://localhost:5000/api/users?active=true

# Record activity
curl -X POST http://localhost:5000/api/git-activity \
  -H "Content-Type: application/json" \
  -d '{
    "timestamp": "2024-01-30T10:30:00Z",
    "userName": "Test User",
    "userEmail": "test@example.com",
    "repoName": "test-repo",
    "branch": "main",
    "modifiedFiles": [],
    "machineName": "test-machine"
  }'
```
