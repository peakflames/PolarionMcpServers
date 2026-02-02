# Changelog

## 0.11.0

TBD

## 0.10.0

### Added

- Add `/{projectId}/mcp` endpoint for Streamable HTTP transport
  - Provides alternative URL pattern alongside existing `/{projectId}` endpoint
  - Both endpoints support the same MCP tools and project routing

## 0.9.0

### Added

- Add middleware workaround for Cline/TypeScript MCP SDK SSE stream disconnection issues
  - Intercepts GET requests for streamableHttp transport and returns dummy SSE response
  - Addresses timeout errors reported in [cline/cline#8367](https://github.com/cline/cline/issues/8367) and [typescript-sdk#1211](https://github.com/modelcontextprotocol/typescript-sdk/issues/1211)
  - Recommended: Use `streamableHttp` transport instead of `sse` for better stability

## 0.8.0

### Changed

- Upgrade ModelContextProtocol SDK from 0.4.0-preview.2 to 0.7.0-preview.1
  - Enables Streamable HTTP transport (replaces legacy HTTP+SSE)
  - Support for MCP protocol version 2025-11-25

## 0.7.0

### Breaking Changes

- `get_document_outline`: Replace `documentTitle` parameter with `space` and `documentId` parameters

### Changed

- Upgrade Polarion package from 0.3.3 to 0.3.4

### Fixed

- Fix revision URI extraction to handle percent-encoded format (e.g., `...%611906`) in addition to query format (`?revision=XXXXX`)
- Fix Lucene query handling that broke phrase searches due to incorrect quote escaping

## 0.6.0

Major API reorganization for improved LLM workflow support. Reduces tool count from 16 to 11 through consolidation and standardizes parameter naming across all tools.

### Breaking Changes

**Tool Renames:**

- `get_space_names` → `list_spaces`
- `get_documents` + `get_documents_by_space_names` → `list_documents` (consolidated)
- `list_available_workitem_types` → `list_workitem_types`
- `list_available_custom_fields_for_workitem_types` → `list_custom_fields`
- `get_details_for_document` → `get_document_info`
- `get_sections_in_document` → `get_document_outline`
- `get_section_content_for_document` → `get_document_section`
- `search_workitems_in_document` → `search_in_document`
- `get_text_for_workitems_by_id` + `get_text_for_workitem_at_revision` → `get_workitem` (consolidated)
- `get_details_for_workitems` → `get_workitem_details` (enhanced)
- `get_revisions_list_for_workitem` + `get_revisions_content_for_workitem` → `get_workitem_history` (consolidated)

**Parameter Renames:**

- `documentName` → `documentTitle` (document tools)
- `documentRevision` → `revision` (document tools)
- `documentNumber` → `sectionNumber` (`get_document_section`)
- `textSearchTerms` → `searchQuery` (`search_in_document`)
- `moduleFolder` → `space` (module tools)
- `workItemId` → `workitemId` (workitem tools)
- `customFieldWhitelist` → `customFields` (detail tools)

### Added

- `get_workitem_details`: New traceability parameters (`linkDirection`, `linkTypeFilter`, `followLevels`) for recursive link traversal
- `list_documents`: Consolidated tool with optional `space` and `titleFilter` parameters
- `get_workitem`: Consolidated tool with optional `revision` parameter

### Removed

- `get_documents_by_space_names` (merged into `list_documents`)
- `get_text_for_workitem_at_revision` (merged into `get_workitem`)
- `get_revisions_list_for_workitem` (merged into `get_workitem_history`)

## 0.5.3

- Add document branching support with two new MCP tools:
  - `get_workitems_in_branched_document` - Retrieves work items from a branched document at a specific revision using a 4-step revision-aware algorithm that correctly fetches historical versions when they differ from HEAD
  - `get_workitems_in_module` - Queries work items using SQL against the `REL_MODULE_WORKITEM` relationship table for fast retrieval of module contents
- Upgrade Polarion package from 0.3.2 to 0.3.3 to fix URI format issues in `getModuleWorkItemUris` API

## 0.5.2

- add Id column to get_documents tool output

## 0.5.1

- Rename and clean up GetDetailsForDocuments tool

## 0.5.0

- Add new tool `get_details_for_documents` to retrieve comprehensive details for Polarion documents/modules
  - Supports retrieving standard fields and custom fields
  - Allows filtering custom fields with whitelist, 'all', or 'none' options
  - Uses configurable default fields from project configuration
- Add `PolarionDocumentDefaultFields` property to `PolarionProjectConfig` to define default document fields to retrieve when no specific fields are requested

## 0.4.9

- Upgrade Polarion package from 0.3.1 to 0.3.2 to support updated API interfaces
- Update `get_revisions_content_for_workitem` tool to handle dictionary return type with revision IDs as keys
- Add new tool `get_text_for_workitem_at_revision` to retrieve a single work item at a specific revision

## 0.4.8

- Upgrade Polarion package from 0.2.1 to 0.3.1 for access to new revision-oriented apis
- Add `get_revisions_list_for_workitem` tool to retrieve revision IDs for a work item
- Add `get_revisions_content_for_workitem` tool to retrieve detailed content at each revision

## 0.4.7

- Upgrade Polarion package from 0.2.0 to 0.2.1 to allow get_documents to be case insensative
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
