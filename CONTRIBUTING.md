# Contributing to Polarion MCP Servers

This guide is for developers who want to contribute to or build the Polarion MCP Servers project.

## Project Structure

- **PolarionMcpTools**: Core library with tools for interacting with Polarion
- **PolarionMcpServer**: Console application with stdio transport
- **PolarionRemoteMcpServer**: Web application with HTTP transport

## Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)

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

## Debugging the SSE MCP Server

1. Start the MCP Server project
2. From a terminal, run `npx @modelcontextprotocol/inspector`
3. From your browser, navigate to `http://localhost:{{PORT}}`
4. Configure the inspector to connect to the server:
   - TransportType: SSE
   - URL: http://{{your-server-ip}}:5090/{ProjectUrlAlias}/sse

## Development Guidelines

For detailed development guidelines including coding conventions, tool implementation patterns, and best practices, see [.clinerules/DEVELOPER_GUIDELINES.md](.clinerules/DEVELOPER_GUIDELINES.md).
