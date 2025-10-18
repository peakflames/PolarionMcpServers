# Changelog


## 0.4.8

- TBD

## 0.4.7

- Upgrade Polarion package from 0.2.0 to 0.2.1 to allows get_documents to be case insensative
- Add case-insensitive space name filtering in GetDocumentsInSpace

## 0.4.6

- Update SearchWorkitemsInDocument to support the updated attribute to see last update timestamp

## 0.4.5

- Update SearchWorkitemsInDocument to accommodate cline using Lucene paraenthesis
- Migrate to the latest mcp sdk `0.4.0-preview.2` to support streamable http

## 0.4.4

- Add startup version detection and reporting to console and logs
- Add environment variables support for configuration overrides
- Enable detailed logging of loaded Polarion project configurations
- Add reflection and diagnostics imports for version detection

## 0.4.3

Refactor MCP server architecture and consolidate configuration

- Split PolarionClientFactory into stdio and remote implementations
- Move shared components to PolarionMcpTools library
- Replace polarion-mcp.config.json with appsettings.json
- Add IPolarionClientFactory interface for dependency injection
- Update VS Code launch configurations for new project structure
- Consolidate project configurations and field mappings

## 0.4.2

- Update Polarion package to version 0.2.0 and adapt API calls
  - Better support for LateX and polarion-rte-link cross references in the Markdown outputs
- In `get_details_for_workitems`, swap incoming/outgoing linked WorkItems sections

## 0.4.1

- Fix issue with `get_sections_in_document` tool

## 0.4.0

- Add WorkItemPrefix property and update space names description
- Add new tool `get_sections_in_document`
- Add new tool `get_section_content_for_document`
- Upgrade `Polarion` to 0.1.0

## 0.3.4

- Add support for User arrays in Utils

## 0.3.3

- Introduce `McpTools_GetDetailsForWorkItems`: A new tool to fetch comprehensive details for WorkItems, allowing users to specify fields or use a new default set.
- Add `PolarionWorkItemDefaultFields` to `PolarionProjectConfig`: Enables defining a default list of fields to retrieve for WorkItems when no specific fields are requested.
- Add `McpTools_ListAvaialbeCustomFieldsForWorkItemTypes`: A new tool to list all available custom fields for different WorkItem types.
- Remove deprecated tools: `McpTools_GetConfiguredCustomFields` and `McpTools_GetCustomFieldsForWorkItems`.
- Update `McpTools_ListConfiguredWorkItemTypes`, `Utils.cs`, `appsettings.json`, and `README.md` to support these enhancements and document the new functionality.

## 0.3.2

- Update parameter description for `GetDocuments` method
- Added per-project configuration (`PolarionWorkItemTypes` in `appsettings.json` under each project) to define specific custom fields to retrieve for different WorkItem types.
- Added MCP Tool: `get_configured_custom_fields` - Retrieves the list of custom fields configured for a specific WorkItem type ID, based on the current project's settings.
- Added MCP Tool: `list_configured_workitem_types` - Lists all WorkItem type IDs that have custom field configurations defined in the current project's settings.
- Added MCP Tool: `get_custom_fields_for_workitems` - Retrieves specified custom field values for a given list of WorkItem IDs.

## 0.3.1

- Add the following tools:
  - SearchWorkitemsInDocument

## 0.3.0

- Add the following tools:
  - GetDocuments
  - GetDocumentInSpaceNames
  - GetSpaceNames

- Add the following configuration options:
  - BlacklistSpaceContainingMatch

## 0.2.0

- Update to support multiple projects in the same server
  - URL Route Pattern: `https://{server}:{port}/{{ ProjectUrlAlias }}/sse`
  - Example appsettings.json entry that will support the following routes for MCP clients:
    - `https://{server}:{port}/starlight/sse`
    - `https://{server}:{port}/octopus/sse`
    - `https://{server}:{port}/grogu/sse`

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
                    }
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

## 0.1.3

- Upgrated to ModelContextProtocol 0.1.0-preview.12
- Migrate to creating the Polarion Client per Tool call to ensure the polarion client is always available and does not go stale.

## 0.1.2

- Remote server has ReadWorkItems tool prompt tweaked, added error codes and improved error messages.

## 0.1.1

- Now publishing under peakflames/polarion-remote-mcp-server on Docker Hub`
- Embed the image tag in the cspoj file
- Update the README

## 0.1.0

Initial release
