# Contributing to Polarion MCP Servers

This guide is for developers who want to contribute to or build the Polarion MCP Servers project.

## Project Structure

- **PolarionMcpTools**: Core library with tools for interacting with Polarion
- **PolarionMcpServer**: Console application with stdio transport
- **PolarionRemoteMcpServer**: Web application with HTTP transport

## Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)
- Python 3.x with `psutil` and `fastmcp` packages (for build automation)

## Configuration

- Copy `.env.example` to `.env` and set `POLARION_DEFAULT_PROJECT` to your default project alias
- Configure Polarion projects in `appsettings.json` (base configuration)
- Override settings locally using `appsettings.Development.json` (takes precedence in Development mode)
- The three tracked `PolarionRemoteMcpServer/appsettings*.json` files (`appsettings.json`,
  `appsettings.Development.json`, `appsettings.Test.json`) carry **non-secret defaults only** and
  are the safe copyable reference. Every other `appsettings*.json` variant you create locally with
  real credentials stays uncommitted — see `.gitignore`'s re-include list
- For `McpAuth`, `Rbac`, or `Credentials` configuration, see
  [docs/authentication.md](docs/authentication.md) and [docs/rbac.md](docs/rbac.md)

## Building the Projects

### Building Locally

To build the projects locally:

```bash
dotnet build PolarionMcpServers.sln
```

### Building Docker Image

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
2. Build the project and image locally:

```bash
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
```

### Publishing to a Docker Registry

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
2. Build the project and image and publish to your Docker registry:

```bash
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
docker push peakflames/polarion-remote-mcp-server:{{VERSION}}
```

## Debugging the MCP Server

1. Start the MCP Server project
2. From a terminal, run `npx @modelcontextprotocol/inspector`
3. From your browser, navigate to `http://localhost:{{PORT}}`
4. Configure the inspector to connect to the server:
   - TransportType: Streamable HTTP
   - URL: http://{{your-server-ip}}:5090/{ProjectUrlAlias}/mcp

## Testing REST API Endpoints

The server includes REST API endpoints compatible with Polarion REST API format. **REST API endpoints require API key authentication.**

### Using Scalar UI (Recommended)

1. Start the server: `python build.py start`
2. Open the Scalar API documentation: http://localhost:5090/scalar/v1
3. Click the "Authenticate" button and enter your API key
4. Browse and test available endpoints interactively

### Using curl

REST API endpoints require the `X-API-Key` header:

```bash
# Check server health (no auth required)
curl http://localhost:5090/api/health

# Get version info (no auth required)
curl http://localhost:5090/api/version

# Get OpenAPI spec (no auth required)
curl http://localhost:5090/openapi/v1.json

# Test REST endpoints (API key required - use SessionConfig.ProjectId as projectId)
curl -H "X-API-Key: your-api-key" http://localhost:5090/polarion/rest/v1/projects/{projectId}/spaces
curl -H "X-API-Key: your-api-key" http://localhost:5090/polarion/rest/v1/projects/{projectId}/workitems/{workitemId}
```

### Configuring Test API Keys

Add API consumers to `appsettings.json`:

```json
{
  "ApiConsumers": {
    "Consumers": {
      "dev_testing": {
        "Name": "Development Testing",
        "ApplicationKey": "dev-test-key-12345",
        "Active": true,
        "AllowedScopes": ["polarion:read"],
        "Description": "API key for local development testing"
      }
    }
  }
}
```

**Note:** REST API endpoints use `SessionConfig.ProjectId` for project matching, not `ProjectUrlAlias`. This differs from MCP endpoints which use `ProjectUrlAlias`.

## Development Guidelines

For detailed development guidelines including coding conventions, tool implementation patterns, and best practices, see [CLAUDE.md](CLAUDE.md).

## Testing

Run the automated test suite with:

```bash
python build.py test
```

This includes `PolarionRemoteMcpServer.Tests/Auth`, a stub-authorization-server-backed suite
covering client-id binding, options validation, email guardrails, `/userinfo` identity resolution,
discovery/challenge responses, and token validation for the `McpAuth`/`Rbac` features documented in
[docs/authentication.md](docs/authentication.md) and [docs/rbac.md](docs/rbac.md).

## Documenting `McpAuth`, `Rbac`, and `Credentials` changes

When adding or changing an `McpAuth:`, `Rbac:`, or `Credentials:` configuration key:

- Update [docs/authentication.md](docs/authentication.md) or [docs/rbac.md](docs/rbac.md) —
  whichever section owns the key — including its reference table and, if applicable, its
  startup-validation-error table.
- Update `CHANGELOG.md` under `[Unreleased]` per
  [docs/changelog-generation-rules.md](docs/changelog-generation-rules.md). In-repo documentation
  changes themselves are excluded from the changelog by those same rules.
