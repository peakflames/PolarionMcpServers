# PolarionMcpServers Developer Guidelines

This document outlines the essential rules and conventions for the PolarionMcpServers project. Follow these guidelines to maintain consistency and ensure proper functionality.

## Project Structure

- **PolarionMcpTools**: Core library with tools for interacting with Polarion
- **PolarionMcpServer**: Console application with stdio transport
- **PolarionRemoteMcpServer**: Web application with HTTP transport

## Build and Test

Use the Python build automation script for all build and run operations:

```bash
python build.py build    # Build solution (auto-stops running app)
python build.py start    # Build and start in background (port 5090)
python build.py stop     # Stop background application
python build.py status   # Check if application is running
python build.py run      # Run in foreground (blocks terminal)
```

### MCP Commands (Model Context Protocol)
```bash
python build.py mcp ping [--project <alias>]                    # Check MCP server connectivity
python build.py mcp info [--project <alias>]                    # Show MCP server information
python build.py mcp tools [--project <alias>]                   # List available MCP tools
python build.py mcp call <tool> '{"arg": "value"}' [--project <alias>]  # Call an MCP tool with JSON args
```

**MCP Examples:**
```bash
# Default project (midnight)
python build.py mcp call search_workitems '{"searchQuery": "rigging", "maxResults": 10}'

# Specific project
python build.py mcp call search_workitems '{"searchQuery": "advisory", "itemTypes": "advisory"}' --project midnight-limitations
python build.py mcp tools --project midnight-limitations

# Query current work items in module
python build.py mcp call get_workitems_in_module '{"space": "L1 - Aircraft", "documentId": "l1_aircraft_requirements"}'

# Query work items at specific document revision
python build.py mcp call get_workitems_in_module '{"space": "L1 - Aircraft", "documentId": "l1_aircraft_requirements", "revision": "618111"}'

# With type filtering (current revision only)
python build.py mcp call get_workitems_in_module '{"space": "L1 - Aircraft", "documentId": "l1_aircraft_requirements", "itemTypes": "aircraftRequirement"}'
```

**Available project aliases:** midnight (default), midnight-limitations, product-lifecycle, midnight-flight-test, blue-thunder, midnight-1-0, midnight-1-1

**Note:** Use double quotes for JSON argument keys/values on Windows. If tools error with authentication failures, check that credentials are configured in `PolarionRemoteMcpServer/appsettings.json`.

### REST API Commands

```bash
python build.py rest <method> <path> [options]
```

**REST Options:**

- `--project <alias>` - Project to use (default: midnight). Use `{project}` placeholder in path
- `--query <text>` - Search query
- `--types <types>` - Comma-separated work item types
- `--status <status>` - Comma-separated status values
- `--sort <field>` - Sort field (can prefix with `-` for descending)
- `--page-size <n>` - Results per page (for `page[size]` parameter)
- `--limit <n>` - Limit results
- `--format <fmt>` - Output format: `pretty` (default) or `raw`

**REST Examples:**

```bash
# Health check
python build.py rest GET api/health

# Search work items (new endpoint)
python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query rigging --project midnight
python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query advisory --types advisory,limitation --page-size 10 --project midnight-limitations

# Get specific work item
python build.py rest GET "polarion/rest/v1/projects/{project}/workitems/MD-12345" --project midnight

# Get work item revisions
python build.py rest GET "polarion/rest/v1/projects/{project}/workitems/MD-12345/revisions" --limit 5 --project midnight

# List spaces
python build.py rest GET "polarion/rest/v1/projects/{project}/spaces" --project midnight

# List documents in space
python build.py rest GET "polarion/rest/v1/projects/{project}/spaces/FCC_L4/documents" --project midnight

# Get work items in document
python build.py rest GET "polarion/rest/v1/projects/{project}/spaces/FCC_L4/documents/FCC_L4_Requirements/workitems" --project midnight
```

**Key Features:**

- Automatic API key authentication from appsettings
- `{project}` placeholder replaced with SessionConfig.ProjectId
- Pretty-printed JSON output by default
- Works with all REST API endpoints

### Log Commands (for debugging)
```bash
python build.py log                      # Show last 50 lines
python build.py log <pattern>            # Search for regex pattern
python build.py log --tail <n>           # Show last n lines
python build.py log --level error        # Filter by level (error/warn/info/debug)
python build.py log --tail 100 --level error  # Combine options
```

### URLs (when running)
- http://localhost:5090 - Landing page
- http://localhost:5090/mcp - MCP endpoint (for AI tool integration)

### Key Behaviors
- **`build`** auto-stops any running instance (prevents Windows file lock errors)
- **`start`** auto-builds before launching (always runs latest code)
- **`start`** runs in background, freeing terminal for mcp/verification commands
- **`stop`** gracefully terminates, then force-kills if needed

### Prerequisites
```bash
pip install psutil fastmcp
```

## CRITICAL: Verification Before Commit Rule

**NEVER commit code changes before the user has verified them!**

A successful build (compile) does NOT equal working code. The workflow MUST be:

1. **Implement** - Make the code changes
2. **Build** - Run `python build.py build` to verify compilation
3. **Start App** - Run `python build.py start` to launch in background
4. **Verify** - Use MCP tools or manual testing to confirm functionality
5. **Commit** - ONLY after verification passed

**Why this matters:**
- Compiled code ≠ correct behavior
- API changes need endpoint verification
- Business logic needs functional testing
- Committing untested code pollutes git history with potential bugs

**Verification Workflow Example:**
```bash
python build.py start                           # Build & start in background
python build.py mcp ping                        # Verify MCP connectivity
python build.py mcp tools                       # Verify tools are registered
python build.py mcp call get_space_names       # Test a tool
python build.py log --level error               # Check for errors
# ... additional verification ...
git add <specific files> && git commit -m "feat: ..."  # Commit after verification
python build.py stop                            # Stop when done (optional)
```

## Git Workflow

- Prefer `--no-ff` when merging for any branches to preserve commit history
- Prefer to ask user for approval before pushing to origin unless explicitly requested
- Always explicitly exclude `appsettings.json` from staging (see Security Rule below)
- Use explicit file paths in `git add` commands rather than wildcards

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

3. **Polarion Lucene Query Syntax**:

   When building Lucene queries for `SearchWorkitemAsync` or `SearchWorkitemInBaselineAsync`:

   - **Valid field names**: `document.title`, `document.id`, `type`, `status`, `title`, `id`, `outlineNumber`
   - **Do NOT use**: `description:(...)` or `description.content:(...)` - these cause query parse failures
   - **For text search**: Pass search terms directly without a field qualifier - Polarion searches all indexed text fields by default

   **Working query patterns:**
   ```csharp
   // Filter by document and search text (searches all text fields)
   var query = $"document.title:\"{docTitle}\" AND ({searchTerms})";

   // Filter by document and type
   var query = $"(type:testCase OR type:requirement) AND document.title:\"{docTitle}\"";

   // Filter by document ID
   var query = $"document.id:\"{docId}\" AND ({searchTerms})";
   ```

   **Query syntax rules:**
   - Wrap field values containing spaces in double quotes: `document.title:"My Document"`
   - Use AND/OR for boolean logic: `rigging AND timeout`
   - Use quotes for phrase search: `"rigging timeout"` (exact phrase)
   - Parentheses for grouping: `(type:testCase OR type:testStep) AND document.title:"..."`
   - Leading wildcards (`*term`) are NOT supported and will cause parse errors

4. **API Documentation Reference**:
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

4. **Use build.py for all operations**:
   - PREFER to use `build.py` for nearly all build, test, and verification activities
   - Always verify changes using MCP tools before considering work complete

## C# Coding Conventions

- Use `var` for all variables
- Use curly braces for all blocks
- Prefer Global Using Statements over Local Using Statements
- Prefer FluentResults over null handling or Exceptions for error handling

## MCP Tool Attribute Syntax

- Use multi-line format for MCP tool attributes:
  ```csharp
  [McpServerTool(Name = "tool_name"),
   Description("Tool description")]
  ```
- Do NOT use `[McpServerTool(Name = "...", Description = "...")]` - Description must be a separate attribute on its own line
- Always check existing tool files for the exact syntax pattern

## Documentation Guidelines

- Do NOT create summary/setup/guide markdown files unless explicitly requested
- Complete the task and use attempt_completion to explain what was done
- Keep explanations concise in the attempt_completion result

## ⚠️ CRITICAL: appsettings.json Security Rule

**NEVER commit, add, reset, checkout, discard, or modify `PolarionRemoteMcpServer/appsettings.json` in any git operation.**

This file contains sensitive credentials (usernames, passwords, server URLs) that must be protected at all costs. The file should be treated as if it doesn't exist when performing any git operations:

- ❌ NEVER use `git add PolarionRemoteMcpServer/appsettings.json`
- ❌ NEVER include it in commits
- ❌ NEVER stage changes to this file
- ❌ NEVER reset or checkout this file
- ❌ NEVER discard changes to this file through git

**During release processes and any git operations:**
- Always explicitly exclude this file from staging
- If git status shows it as modified, ignore it completely
- Only stage and commit the specific files needed for the task
- Use explicit file paths in `git add` commands rather than wildcards that might accidentally include it

**Violation of this rule could expose sensitive credentials and compromise security.**
