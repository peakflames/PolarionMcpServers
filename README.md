# Polarion MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Polarion Application Lifecycle Management (ALM) integration.

MCP Tools are available for Polarion work items, including:

- `get_text_for_workitems_by_id`: Gets the main text content for specified WorkItem IDs.
- `get_text_for_workitem_at_revision`: Gets the text content for a single WorkItem at a specific revision.
- `get_details_for_workitems`: Gets detailed information for specified WorkItem IDs including status, type, assignee, custom fields, and linked work items.
- `get_documents`: Lists documents in the project, optionally filtered by title.
- `get_documents_by_space_names`: Lists documents within specified space names.
- `get_space_names`: Lists all available space names in the project.
- `get_sections_in_document`: Gets the list of sections in a document.
- `get_section_content_for_document`: Gets the content of a specific section in a document.
- `search_workitems_in_document`: Searches for WorkItems within a document based on text criteria.
- `list_available_custom_fields_for_workitem_types`: Lists all available custom fields for specific WorkItem types.
- `list_available_workitem_types`: Lists all WorkItem types available in the project.
- `get_revisions_list_for_workitem`: Gets the list of revision IDs for a specific work item, ordered from newest to oldest.
- `get_revisions_content_for_workitem`: Gets the content of a work item at different revisions, including title, status, description, and other standard fields.

## Projects

- **PolarionRemoteMcpServer**: Streamable HTTP MCP server for server-based installations. Stateless, with optional [OAuth 2.1 authentication](docs/authentication.md) and [per-caller RBAC](docs/rbac.md).
- **PolarionMcpServer**: Console-based MCP server for Polarion integration for local workstation installations

## Running via Docker & Linux Server (Recommended)

1. From your Linux server, create a directory for your configuration and logs:

   ```bash
   mkdir -p /opt/polarion-mcp-server
   cd /opt/polarion-mcp-server
   ```

1. Pull the Docker image:

   ```bash
   docker pull peakflames/polarion-remote-mcp-server
   ```

1. Create a tailored `/opt/polarion-mcp-server/appsettings.json` file to your Polarion configuration:

   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "AllowedHosts": "*",
     "ApiConsumers": {
       "Consumers": {
         "my_app": {
           "Name": "My Application",
           "ApplicationKey": "your-secure-api-key-here",
           "Active": true,
           "AllowedScopes": ["polarion:read"],
           "Description": "API consumer for my application"
         }
       }
     },
     "PolarionProjects": [
          {
              "ProjectUrlAlias": "starlight", 
              "Default": true,
              "SessionConfig": { 
                  "ServerUrl": "https://polarion.int.mycompany.com/",
                  "Username": "shared_user_read_only",
                  "Password": "linear-Vietnam-FLIP-212824", 
                  "ProjectId": "Starlight_Main", 
                  "TimeoutSeconds": 60
              },
              "PolarionWorkItemTypes": [
                {
                  "id": "requirement",
                  "fields": ["custom_field_1", "priority", "severity"]
                },
                {
                  "id": "defect",
                  "fields": ["defect_type", "found_in_build"]
                }
              ]
          },
          {
              "ProjectUrlAlias": "octopus", 
              "Default": false,
              "SessionConfig": { 
                  "ServerUrl": "https://polarion.int.mycompany.com/",
                  "Username": "some_other_user",
                  "Password": "linear-Vietnam-FLIP-212824", 
                  "ProjectId": "octopus_gov", 
                  "TimeoutSeconds": 60
              }
          },
          {
              "ProjectUrlAlias": "grogu", 
              "Default": false,
              "SessionConfig": { 
                  "ServerUrl": "https://polarion-dev.int.mycompany.com/",
                  "Username": "vader",
                  "Password": "12345", 
                  "ProjectId": "grogu_boss", 
                  "TimeoutSeconds": 60
              }
          }
      ]
   }
   ```

1. Run the Docker container:

   ```bash
   docker run -d \
     --name polarion-mcp-server \
     -p 8080:8080 \
     -v appsettings.json:/app/appsettings.json \
     peakflames/polarion-remote-mcp-server
   ```

1. The server should now be running. MCP clients will connect using a URL specific to the desired project configuration alias:
   1. Streamable HTTP Transport: `http://{{your-server-ip}}:8080/{ProjectUrlAlias}/mcp`.
2. The server also provides:
   - REST API: `http://{{your-server-ip}}:8080/polarion/rest/v1/projects/{ProjectId}/...` (uses `SessionConfig.ProjectId`)
     - **Note:** REST API endpoints require API key authentication via `X-API-Key` header
   - API Documentation: `http://{{your-server-ip}}:8080/scalar/v1` (includes authentication UI)
   - Health Check: `http://{{your-server-ip}}:8080/api/health`
3. 📢IMPORTANT - Do NOT run with replica instances of the server as the session connection will not be shared between replicas.

### Configuration Options

**Configuration Files:**
- `appsettings.json` - Base configuration for production/server deployments. Tracked in git, and carries non-secret defaults only.
- `appsettings.Development.json` - Overrides base settings for local development. Also tracked in git (non-secret defaults only) — takes precedence in Development mode. Any variant you fill in with real credentials stays uncommitted; see `.gitignore`.
- `.env` - Optional environment variables (copy from `.env.example`), can set `POLARION_DEFAULT_PROJECT`

The server uses a `PolarionProjects` array in `appsettings.json` to define one or more Polarion instance configurations. Each object in the array represents a distinct configuration accessible via a unique URL alias.

| Top-Level Setting | Description                                                                 |
| ----------------- | --------------------------------------------------------------------------- |
| `PolarionProjects`  | (Array) Contains one or more Polarion project configuration objects.        |
| [`McpAuth`](docs/authentication.md) | (Object, optional) OAuth 2.1 resource-server authentication for the MCP endpoint. Off by default. |
| [`Rbac`](docs/rbac.md) | (Object, optional) Per-caller Polarion project-visibility gate for MCP tool calls. Off by default; requires `McpAuth`. |
| [`Credentials`](docs/authentication.md#upstream-polarion-credentials) | (Object, optional) Controls whose Polarion credential an MCP call uses upstream. Defaults to today's shared-service-account behavior. |

**Each Project Configuration Object:**

| Setting                   | Description                                                                                                | Required | Default         |
| ------------------------- | ---------------------------------------------------------------------------------------------------------- | -------- | --------------- |
| `ProjectUrlAlias`         | A unique string used in the connection URL (`/{ProjectUrlAlias}/mcp`) to identify this configuration.        | Yes      | N/A             |
| `Default`                 | (boolean) If `true`, this configuration is used if the client connects without specifying a `ProjectUrlAlias`. Only one entry can be `true`. | No       | `false`         |
| `SessionConfig`           | (Object) Contains the specific connection details for this Polarion instance.                              | Yes      | N/A             |
| `PolarionWorkItemTypes`   | (Array, Optional) Defines custom fields to retrieve for specific WorkItem types within this project. Each object in the array should have an `id` (string, WorkItem type ID) and `fields` (array of strings, custom field names). | No       | Empty List      |

**`SessionConfig` Object Details:**

| Setting        | Description                                                         | Required | Default |
| -------------- | ------------------------------------------------------------------- | -------- | ------- |
| `ServerUrl`    | URL of the Polarion server (e.g., "https://polarion.example.com/")  | Yes      | N/A     |
| `Username`     | Polarion username with appropriate permissions.                     | Yes      | N/A     |
| `Password`     | Password for the Polarion user. **(Consider secure alternatives)**    | Yes      | N/A     |
| `ProjectId`    | The *actual* ID of the Polarion project to interact with.           | Yes      | N/A     |
| `TimeoutSeconds` | Connection timeout in seconds.                                      | No       | `60`    |

### Environment Variable Password Override

Instead of placing passwords in configuration files, set the `POLARION_PASSWORD` environment variable. When set, it overrides `SessionConfig.Password` for all configured projects.

**Docker example:**
```bash
docker run -d \
  --name polarion-mcp-server \
  -p 8080:8080 \
  -e POLARION_PASSWORD=your-secret-password \
  -v appsettings.json:/app/appsettings.json \
  peakflames/polarion-remote-mcp-server
```

This works for both `PolarionRemoteMcpServer` (HTTP) and `PolarionMcpServer` (stdio).

*Note: It is strongly recommended to use the `POLARION_PASSWORD` environment variable or more secure methods for storing credentials (like User Secrets, Azure Key Vault, etc.) rather than placing plain text passwords in `appsettings.json`.*

### REST API Specification Alignment

The REST API is designed to align with the **official Polarion REST API** specification available at `https://testdrive.polarion.com/polarion/rest/v1/definition`. A local copy of this definition is maintained at `docs/polarion-rest-vq-definition.json` for reference when implementing or extending endpoints.

### API Key Authentication (REST API Only)

REST API endpoints require authentication via API key. Configure API consumers in the `ApiConsumers` section of `appsettings.json`:

| Setting | Description | Required |
| ------- | ----------- | -------- |
| `ApiConsumers.Consumers` | Dictionary of consumer configurations keyed by consumer ID | Yes |
| `Name` | Display name for the API consumer | Yes |
| `ApplicationKey` | The API key used for authentication | Yes |
| `Active` | Whether the consumer is allowed to authenticate | Yes |
| `AllowedScopes` | List of scopes (e.g., `["polarion:read"]`) | Yes |
| `Description` | Optional description of the consumer | No |

**Available Scopes:**
- `polarion:read` - Read access to all REST API endpoints

**Usage:**
```bash
curl -H "X-API-Key: your-api-key" http://localhost:8080/polarion/rest/v1/projects/{projectId}/spaces
```

**Note:** Health checks (`/api/health`, `/api/version`) and API documentation (`/scalar/v1`) do not require authentication. MCP endpoints do not require authentication either, **only while `McpAuth:Enabled` is `false`** (the default) — see the next section.

## Optional: MCP Authentication and Per-Caller Access Control

`PolarionRemoteMcpServer` supports two independent, off-by-default features for the MCP endpoint:

- **[OAuth 2.1 authentication](docs/authentication.md)** (`McpAuth:Enabled`) — requires a bearer
  token from an external authorization server before a `tools/call` is allowed to run. Each served
  project alias publishes its own [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728)
  protected-resource metadata at `/.well-known/oauth-protected-resource/{alias}/mcp`, so a
  conforming MCP client can discover how to authenticate automatically.
- **[Per-caller RBAC](docs/rbac.md)** (`Rbac:Enabled`) — additionally authorizes each `tools/call`
  against the *calling* user's own Polarion project membership, instead of every authenticated
  caller sharing the same access. Requires `McpAuth:Enabled=true`.

Both are `false` by default, and the server's behavior is unchanged from prior releases unless you
configure them. See [docs/authentication.md](docs/authentication.md) and
[docs/rbac.md](docs/rbac.md) for setup, configuration reference tables, and startup validation
errors.

## Configuring MCP Clients

**To configure Cline:**

1. Open Cline's MCP settings UI
1. Click the "Remote Servers" tab
1. For each `ProjectUrlAlias` in your `appsettings.json` that the user wants to connect to:

  ```json
  {
    "mcpServers": {
      ...
      ...

      "Polarion Starlight": {
        "autoApprove": [],
        "disabled": true,
        "timeout": 60,
        "url": "http://{{your-server-ip}}:8080/starlight/mcp",
        "transportType": "streamableHttp"
      },
      "Polarion Octopus": {
        "autoApprove": [],
        "disabled": true,
        "timeout": 60,
        "url": "http://{{your-server-ip}}:8080/octopus/mcp",
      "transportType": "streamableHttp"
      }
    ...
    ...
  }
   ```

1. Repeat for each `ProjectUrlAlias` you want to connect to.

**To configure Visual Studio Code:**

Add the following configuration to your settings.json file:

```json
"servers": {
    "polarion-starlight": { // Use a descriptive key
        "type": "http",
        "url": "http://{{your-server-ip}}:8080/starlight/mcp", // Replace with your alias
        "env": {}
    },
    "polarion-octopus": { 
        "type": "http",
        "url": "http://{{your-server-ip}}:8080/octopus/mcp", // Replace with your alias
        "env": {}
    }
    // Add entries for each ProjectUrlAlias
}
```

Or from the CLI:

```bash
claude mcp add --transport http polarion-starlight http://{{your-server-ip}}:8080/starlight/mcp
```

**To Claude Desktop:**

Claude Desktop does not yet support streamable HTTP natively, but you can use a proxy with the
following addition to the claude_desktop_config.json file:

```json
{
  "mcpServers": {
    "polarion-remote": {
      "command": "npx",
      "args": [
        "mcp-remote",
        "http://{{your-server-ip}}:8080/{ProjectUrlAlias}/mcp" // Replace {ProjectUrlAlias}
      ]
    }
    // Add entries for each ProjectUrlAlias, potentially using different keys like "polarion-starlight"
  }
}
```

## Running Locally (stdio)

For local development or workstation use, you can run the stdio-based MCP server:

1. Download the appropriate executable for your platform from the [releases page](https://github.com/peakflames/PolarionMcpServers/releases)
2. Configure your MCP client to use the stdio transport with the executable path

## Troubleshooting

**`POST /{alias}/mcp` returns 401 and the client never prompts to log in** — confirm the client
implements MCP's OAuth discovery flow ([RFC 9728](https://www.rfc-editor.org/rfc/rfc9728)). Fetch
`/.well-known/oauth-protected-resource/{alias}/mcp` yourself; if that 404s, `McpAuth:Enabled` isn't
actually `true` on the running server. See [docs/authentication.md](docs/authentication.md).

**`POST /{alias}/mcp` returns 403** — the caller authenticated, but is missing the required scope
or its OAuth client ID isn't on `McpAuth:AllowedClientIds`. See
[401 vs 403](docs/authentication.md#401-vs-403).

**Server refuses to start with an `McpAuth` or `Rbac` validation error** — every message names the
exact key to fix; see the validation-error tables in
[docs/authentication.md](docs/authentication.md#startup-validation-errors) and
[docs/rbac.md](docs/rbac.md#startup-validation-errors).

**Every MCP tool call is denied under RBAC** — RBAC fails closed by design. Set
`Rbac:AuditOnly=true` and read the access-audit records' `DecisionReason` field; see
[docs/rbac.md#troubleshooting](docs/rbac.md#troubleshooting).

## Contributing

For developers who want to contribute or build from source, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

See [LICENSE](LICENSE) for details.
