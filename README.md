# Polarion MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Polarion Application Lifecycle Management (ALM) integration.

MCP Tools are available for Polarion work items, including:

- `get_text_for_workitems_by_id`: Gets the main text content for specified WorkItem IDs.
- `get_documents`: Lists documents in the project, optionally filtered by title.
- `get_documents_by_space_names`: Lists documents within specified space names.
- `get_space_names`: Lists all available space names in the project.
- `search_workitems_in_document`: Searches for WorkItems within a document based on text criteria.
- `get_configured_custom_fields`: Retrieves the list of custom fields configured for a specific WorkItem type ID, based on the current project's settings.
- `list_configured_workitem_types`: Lists all WorkItem type IDs that have custom field configurations defined in the current project's settings.
- `get_custom_fields_for_workitems`: Retrieves specified custom field values for a given list of WorkItem IDs.

## Projects

- **PolarionRemoteMcpServer**: SSE-based MCP server for server based installations
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

1. The server should now be running. MCP clients will connect using a URL specific to the desired project configuration alias: `http://{{your-server-ip}}:8080/{ProjectUrlAlias}/sse`.
1. 📢IMPORTANT - Do NOT run with replica instances of the server as the session connection will not be shared between replicas.

### Configuration Options (`appsettings.json`)

The server uses a `PolarionProjects` array in `appsettings.json` to define one or more Polarion instance configurations. Each object in the array represents a distinct configuration accessible via a unique URL alias.

| Top-Level Setting | Description                                                                 |
| ----------------- | --------------------------------------------------------------------------- |
| `PolarionProjects`  | (Array) Contains one or more Polarion project configuration objects.        |

**Each Project Configuration Object:**

| Setting                   | Description                                                                                                | Required | Default         |
| ------------------------- | ---------------------------------------------------------------------------------------------------------- | -------- | --------------- |
| `ProjectUrlAlias`         | A unique string used in the connection URL (`/{ProjectUrlAlias}/sse`) to identify this configuration.        | Yes      | N/A             |
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

*Note: It is strongly recommended to use more secure methods for storing credentials (like User Secrets, Azure Key Vault, etc.) rather than placing plain text passwords in `appsettings.json`.*

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

      "Polarion Starling": {
        "autoApprove": [],
        "disabled": true,
        "timeout": 60,
        "url": "http://{{your-server-ip}}:8080/starlight/sse",
        "transportType": "sse"
      },
      "Polarion Octopus": {
        "autoApprove": [],
        "disabled": true,
        "timeout": 60,
        "url": "http://{{your-server-ip}}:8080/octopus/sse",
      "transportType": "sse"
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
        "type": "sse",
        "url": "http://{{your-server-ip}}:8080/starlight/sse", // Replace with your alias
        "env": {}
    },
    "polarion-octopus": { 
        "type": "sse",
        "url": "http://{{your-server-ip}}:8080/octopus/sse", // Replace with your alias
        "env": {}
    }
    // Add entries for each ProjectUrlAlias
}
```

**To Claude Desktop:**

Claude Desktop currently doesn’t support SSE, but you can use a proxy with the following addition to the claude_desktop_config.json file:

```json
{
  "mcpServers": {
    "polarion-remote": {
      "command": "npx",
      "args": [
        "mcp-remote",
        "http://{{your-server-ip}}:8080/{ProjectUrlAlias}/sse" // Replace {ProjectUrlAlias}
      ]
    }
    // Add entries for each ProjectUrlAlias, potentially using different keys like "polarion-starlight"
  }
}
```

## Building the Projects

### Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)

### Building Locally

To build the projects locally:

```bash
dotnet build PolarionMcpServers.sln
```

### Building Docker Image

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
1. Build the project and image locally:

```bash
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
```

### Publishing to a Docker Registry

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
1. Build the project and image and publish to your Docker registry:

```bash
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
docker push peakflames/polarion-remote-mcp-server:{{VERSION}}
```
