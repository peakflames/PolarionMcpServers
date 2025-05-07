# Polarion MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Polarion Application Lifecycle Management (ALM) integration.

MCP Tools are available for Polarion work items, including:

- Reading Polarion work items

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
     "PolarionClientConfiguration": {
       "ServerUrl": "https://your-polarion-server/",
       "Username": "your-username",
       "Password": "your-password",
       "ProjectId": "your-project-id",
       "TimeoutSeconds": 60
     }
   }
   ```

1. Run the Docker container:

   ```bash
   docker run -d \
     --name polarion-mcp-server \
     -p 8080:8080 \
     -v appsettings.json:/app/appsettings.json \
     tizzolicious/polarion-remote-mcp-server
   ```

1. The server should now be running. You can access it at `http://{{your-server-ip}}:5090/sse`.
1. IMPORTANT - Do NOT run with replica instances of the server as the session connection will not be shared between replicas.

### Configuration Options

The `PolarionClientConfiguration` section in `appsettings.json` requires the following settings:

| Setting        | Description                                                         |
| -------------- | ------------------------------------------------------------------- |
| ServerUrl      | URL of your Polarion server (e.g., "https://polarion.example.com/") |
| Username       | Polarion username with appropriate permissions                      |
| Password       | Password for the Polarion user                                      |
| ProjectId      | ID of the Polarion project to access                                |
| TimeoutSeconds | Connection timeout in seconds (default: 60)                         |

## Configuring MCP Clients

**To configure Cline:**

1. Open Cline's MCP settings UI
1. Click the "Remote Servers" tab
1. Set the Server name to "Polarion"
1. Set the Server URL to "http://{{your-server-ip}}:5090/sse"
1. Click "Add Server"

**To configure Visual Studio Code:**

Add the following configuration to your settings.json file:

```json
"servers": {
    "polarion-remote": {
        "type": "sse",
        "url": "ttp://{{your-server-ip}}:8080/sse",
        "env": {}
    }    
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
        "http://{{your-server-ip}}:8080/sse"
      ]
    }
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
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer
```

### Publishing to a Docker Registry

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj`
1. Build the project and image and publish to your Docker registry:

```bash
dotnet publish PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj /t:PublishContainer -p ContainerRegistry=your-registery
```
