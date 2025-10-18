# PolarionMcpServers Developer Guidelines

This document outlines the essential rules and conventions for the PolarionMcpServers project. Follow these guidelines to maintain consistency and ensure proper functionality.

## Project Structure

- **PolarionMcpTools**: Core library with tools for interacting with Polarion
- **PolarionMcpServer**: Console application with stdio transport
- **PolarionRemoteMcpServer**: Web application with HTTP transport

## Adding New Configuration

1. **Update appsettings.json**:
   - Add new configuration properties to the appropriate section in `PolarionRemoteMcpServer/appsettings.json`
   - For new Polarion projects, add them to the `PolarionProjects` array
   - Include any necessary filters (e.g., `Spaces`) for the project

2. **Update PolarionProjectConfig**:
   - If adding new configuration properties, update the `PolarionProjectConfig` class in `PolarionMcpTools/PolarionProjectConfig.cs`
   - Ensure properties have proper XML documentation

3. **Update PolarionConfigJsonContext**:
   - If adding new configuration types, add a `[JsonSerializable(typeof(YourNewType))]` attribute to the `PolarionConfigJsonContext` class in `PolarionRemoteMcpServer/PolarionConfigJsonContext.cs`
   - This is required for source generation in AOT/trimmed applications

## Adding New MCP Tools

1. **Create a new partial class file**:
   - Create a new file in `PolarionMcpTools/Tools/` named `McpTools_YourToolName.cs`
   - Follow the naming convention of existing tool files
   - Follow namespace conventions of existing tool files
   - Place using statements in GlobalUsing.cs file of the project

2. **Implement the tool method**:
   ```csharp
   public sealed partial class McpTools
   {
       [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
       [McpServerTool(Name = "your_tool_name"), Description("Description of your tool")]
       public async Task<string> YourToolName(
           [Description("Description of parameter")] string parameterName)
       {
           await using (var scope = _serviceProvider.CreateAsyncScope())
           {
               var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
               var clientResult = await clientFactory.CreateClientAsync();
               if (clientResult.IsFailed)
               {
                   return clientResult.Errors.First().ToString() ?? "Internal Error unknown error when creating Polarion client";
               }

               var polarionClient = clientResult.Value;

               // Get the current project configuration
               var projectConfig = GetCurrentProjectConfig();

               try
               {
                   // Implement your tool logic here
                   // ...

                   return "Your result in markdown format";
               }
               catch (Exception ex)
               {
                   return $"ERROR: Failed due to exception '{ex.Message}'";
               }
           }
       }
   }
   ```

3. **Error Handling Conventions**:
   - Prefer the use of FluentResult then using try/catch block
   - Return error messages with an "ERROR:" prefix
   - Include error codes for easier troubleshooting (e.g., "ERROR: (1234)")
   - Return results in markdown format

## Polarion Client Usage

1. **Creating a client**:
   - Always use the `IPolarionClientFactory` from dependency injection
   - Create clients within a service scope using `_serviceProvider.CreateAsyncScope()`
   - Check for failures with `clientResult.IsFailed`

2. **Project Selection**:
   - The `PolarionRemoteMcpServer` supports multiple projects via URL routing
   - The project ID is extracted from the route parameter
   - If no project ID is provided, the default project is used

3. **API Documentation Reference**:
   - Complete API documentation is available at: https://github.com/peakflames/PolarionApiClient/blob/main/api.md
   - **For Cline/AI Assistants**: When you need detailed information about available Polarion client methods, their signatures, parameters, or usage, use your web_fetch or similar tools to retrieve the API documentation from the above URL
   - The documentation includes all available methods with complete signatures, parameter descriptions, return types, and usage examples
   - Key method categories include:
     - Work Item Operations (GetWorkItemByIdAsync, SearchWorkitemAsync, etc.)
     - Module Operations (GetModulesInSpaceThinAsync, GetModuleByLocationAsync, etc.)
     - Markdown Export Operations (ExportModuleToMarkdownAsync, ConvertWorkItemToMarkdown, etc.)
     - Revision Operations (GetRevisionIdsAsync, GetWorkItemRevisionsByIdAsync, etc.)

## Logging Conventions

- Use Serilog for logging
- Log appropriate information at appropriate levels:
  - `LogDebug` for detailed troubleshooting
  - `LogInformation` for general operational information
  - `LogError` for errors that require attention

## Return Format Conventions

- Return results in markdown format
- For lists, use bullet points with `- item` syntax
- For work items, include headers with `## WorkItem (id=XXX, type=XXX, lastUpdated=XXX)` format

## Input Validation

- Validate all input parameters at the beginning of the method
- Use descriptive error messages for invalid inputs
- Return early if validation fails

## Deployment

- **PolarionMcpServer**: Deploy as a standalone executable
- **PolarionRemoteMcpServer**: 
  - Can be deployed as a standalone web application
  - Container support is enabled with the following properties in the project file:
    ```xml
    <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
    <ContainerRepository>peakflames/polarion-remote-mcp-server</ContainerRepository>
    ```

## AI Assistant Guidelines (Cline Rules)

When working on this project as an AI assistant:

1. **Always fetch API documentation when needed**:
   - If you need to understand available Polarion client methods, use `web_fetch` to retrieve: https://github.com/peakflames/PolarionApiClient/blob/main/api.md
   - Don't guess method signatures or parameters - always reference the official documentation
   - The API documentation includes complete method signatures, parameter types, return types, and descriptions

2. **Follow established patterns**:
   - Look at existing tool implementations in `PolarionMcpTools/Tools/` for patterns
   - Use the same error handling, logging, and return format conventions
   - Follow the dependency injection patterns shown in existing code

3. **Validate your understanding**:
   - When implementing new tools, cross-reference the API documentation to ensure correct usage
   - Pay attention to async/await patterns and Result<T> return types
   - Use the `[RequiresUnreferencedCode]` attribute for methods that call Polarion APIs

## C# Coding Conventions

- Use `var` for all variables
- Use curly braces for all blocks

## MCP Tool Attribute Syntax

- Use multi-line format for MCP tool attributes:
  ```csharp
  [McpServerTool(Name = "tool_name"),
   Description("Tool description")]
  ```
- Do NOT use `[McpServerTool(Name = "...", Description = "...")]` - Description must be a separate attribute on its own line
- Always check existing tool files for the exact syntax pattern
