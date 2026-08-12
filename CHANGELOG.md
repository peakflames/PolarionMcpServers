# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog 1.0.0](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [0.16.0] - 2026-04-28

### Added

- Add `PolarionRemoteMcpServer.Tests` project with xUnit v3 integration and snapshot tests
- Add `build.py test` command with `--filter` and log viewing support

### Changed

- Bump `PolarionApiClient` package dependency to 0.3.6

## [0.15.0] - 2026-03-26

### Added

- Add `POLARION_PASSWORD` environment variable support to `PolarionRemoteMcpServer` — when set, overrides `SessionConfig.Password` for all configured projects

### Changed

- Simplify `POLARION_PASSWORD` env var handling in `PolarionMcpServer` (stdio) to apply as a global fallback for all projects

## [0.14.0] - 2026-03-19

### Added

- Add linked work item REST endpoints:
  - `GET /polarion/rest/v1/projects/{projectId}/workitems/{workItemId}/linkedworkitems` — outgoing links (items this WI links TO)
  - `GET /polarion/rest/v1/projects/{projectId}/workitems/{workItemId}/backlinkedworkitems` — incoming links (items that link TO this WI)
  - Returns JSON:API document with `linkedworkitems` type resources containing `role`, `suspect`, and `workItemId` attributes
  - Filters out internal `subsection_of` Polarion document structure links
  - Returns empty array (not error) when no links exist

## [0.13.0] - 2026-02-08

### Added

- Add optional `revision` parameter to `get_workitems_in_module` MCP tool
  - Defaults to "-1" for latest revision (maintains backward compatibility)
  - Historical queries show revision metadata, historical/current counts, and revision status
  - Current queries maintain existing format
  - Type filtering only supported for current revision
  - Follows established pattern from `get_document_section` and `search_in_document` tools
- Add revision support to REST API document workitems endpoint: `GET /polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/workitems`
  - Add optional revision metadata fields to `WorkItemAttributes` (revision, headRevision, isHistorical)
  - Add revision summary statistics to response meta (historicalItemCount, currentItemCount)
  - Add `--revision` flag to `build.py rest` command for CLI support
  - Type filtering only supported for current queries; historical queries ignore types parameter with warning
  - Maintains full backward compatibility for existing API consumers

### Changed

- Improve `build.py` MCP command timeout and error reporting for better debugging experience
- Update `get_document_revision_history` description to reference current tool names
- Update CLAUDE.md with examples of revision parameter usage for MCP and REST API

### Deprecated

- Deprecate `get_workitems_in_branched_document` MCP tool
  - Now delegates to unified `get_workitems_in_module` with revision parameter
  - Shows deprecation warning when called
  - Will be removed in future version - use `get_workitems_in_module` with `revision` parameter instead

### Removed

- Remove `get_workitems_in_branched_document` from tool registry (tool still callable but deprecated, delegates to `get_workitems_in_module`)

### Fixed

- Fix `get_document_section` to correctly match dash-separated work items (e.g., 7.1.2-1) within sections, not just dot-separated sub-sections
- Fix revision parameter in `get_document_section` and `search_in_document` tools - now properly uses revision-aware API paths instead of silently ignoring the parameter
  - Uses `GetWorkItemsByModuleRevisionAsync` for historical revisions with proper `UnresolvableObjectException` handling
- Fixed build warnings: CS8629 nullable value access and IL2026 reflection warnings in SearchWorkitems tool

## [0.12.0] - 2026-02-03

### Added

- Add `search_workitems` MCP tool for project-wide work item search
  - Search across entire Polarion project without requiring document IDs, space names, or work item IDs
  - Uses native Polarion Lucene search API (`SearchWorkitemAsync`) for efficient queries
  - Supports flexible search patterns: simple terms, OR logic (default), AND logic, and exact phrase matching
  - Optional filters: `itemTypes` (comma-separated work item types), `statusFilter` (comma-separated statuses)
  - Configurable results: `sortBy` (created/updated/id/title), `maxResults` (default 50, max 500)
  - Returns markdown-formatted results with work item details including author, assignee, and description
  - Complements existing `search_in_document` tool (document-scoped vs project-scoped search)
- Add REST API endpoint for work item search: `GET /polarion/rest/v1/projects/{projectId}/workitems`
  - Query parameters: `query` (required), `types`, `status`, `sort`, `page[size]`
  - Returns JSON:API formatted results with metadata
  - Requires API key authentication (`polarion:read` scope)
  - Uses same Lucene search logic as `search_workitems` MCP tool
  - Enables standard HTTP/REST access for integration with CI/CD, scripts, and external tools
  - Examples:
    - `GET /polarion/rest/v1/projects/Starlight_Main/workitems?query=rigging`
    - `GET /polarion/rest/v1/projects/octopus_gov/workitems?query=advisory&types=advisory,limitation&page[size]=10`
- Add `rest` command to build.py for REST API testing
  - Generic command that works with all REST API endpoints
  - Syntax: `python build.py rest <method> <path> [options]`
  - Supports `{project}` placeholder that gets replaced with SessionConfig.ProjectId
  - Automatic API key authentication from appsettings
  - Query parameter shortcuts: `--query`, `--types`, `--status`, `--sort`, `--page-size`, `--limit`
  - Output formatting: `--format pretty` (default) or `raw`
  - Examples:
    - `python build.py rest GET api/health`
    - `python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query rigging --project starlight`
    - `python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query advisory --types advisory,limitation --project octopus`
    - `python build.py rest GET "polarion/rest/v1/projects/{project}/spaces" --project starlight`
- Add `--project` parameter to build.py MCP commands for project routing
  - Enables testing MCP tools against specific Polarion projects via command line
  - Defaults to "starlight" project when not specified
  - Syntax: `python build.py mcp <command> --project <alias>`
  - Supported projects: starlight (default), octopus, grogu, starlight-flight-test, nebula, starlight-1-0, starlight-1-1
  - Examples:
    - `python build.py mcp tools --project octopus`
    - `python build.py mcp call search_workitems '{"searchQuery": "advisory"}' --project octopus`
- Add `assignee` field to `WorkItemAttributes` REST API model for work item search results
- Add forwarded headers support for reverse proxy
  - Enable ForwardedHeaders middleware to correctly detect HTTPS, host, and client IP when running behind a reverse proxy. This ensures OpenAPI/Scalar displays the correct public URL instead of localhost.

## [0.11.0] - 2026-02-02

### Added

- Add API key authentication for REST API endpoints
  - REST API endpoints now require `X-API-Key` header for authentication
  - Configure API consumers in `appsettings.json` under `ApiConsumers` section
  - Scope-based authorization with `polarion:read` scope (additional scopes for future use)
  - MCP endpoints, health checks, and API documentation remain unauthenticated
  - OpenAPI/Scalar UI includes authentication support for interactive testing
- Add REST API endpoints compatible with Polarion REST API format (JSON:API)
  - `GET /polarion/rest/v1/projects/{projectId}/workitems/{workitemId}` - Get work item details
  - `GET /polarion/rest/v1/projects/{projectId}/workitems/{workitemId}/revisions` - Get work item revisions
  - `GET /polarion/rest/v1/projects/{projectId}/spaces` - List spaces
  - `GET /polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents` - List documents in space
  - `GET /polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}` - Get document details
  - `GET /polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/workitems` - Get work items in document
  - `GET /polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/revisions` - Get document revisions
  - Revision endpoints support `page[size]` query parameter (default: 100, max: 500)
  - Response meta uses `count` for items returned (not `totalCount`)
  - Note: REST API uses `SessionConfig.ProjectId` for project matching (not `ProjectUrlAlias`)
- Add Scalar API documentation UI at `/scalar/v1` for interactive API testing
- Add OpenAPI specification at `/openapi/v1.json`
- Add health check endpoint at `/api/health`
- Add version endpoint at `/api/version`

## [0.10.0] - 2026-02-02

### Added

- Add `/{projectId}/mcp` endpoint for Streamable HTTP transport
  - Provides alternative URL pattern alongside existing `/{projectId}` endpoint
  - Both endpoints support the same MCP tools and project routing

## [0.9.0] - 2026-02-01

### Added

- Add middleware workaround for Cline/TypeScript MCP SDK SSE stream disconnection issues
  - Intercepts GET requests for streamableHttp transport and returns dummy SSE response
  - Addresses timeout errors reported in [cline/cline#8367](https://github.com/cline/cline/issues/8367) and [typescript-sdk#1211](https://github.com/modelcontextprotocol/typescript-sdk/issues/1211)
  - Recommended: Use `streamableHttp` transport instead of `sse` for better stability

## [0.8.0] - 2026-01-30

### Changed

- Upgrade ModelContextProtocol SDK from 0.4.0-preview.2 to 0.7.0-preview.1
  - Enables Streamable HTTP transport (replaces legacy HTTP+SSE)
  - Support for MCP protocol version 2025-11-25

## [0.7.0] - 2026-01-22

### Changed

- **BREAKING:** `get_document_outline`: Replace `documentTitle` parameter with `space` and `documentId` parameters
- Upgrade Polarion package from 0.3.3 to 0.3.4

### Fixed

- Fix revision URI extraction to handle percent-encoded format (e.g., `...%611906`) in addition to query format (`?revision=XXXXX`)
- Fix Lucene query handling that broke phrase searches due to incorrect quote escaping

## [0.6.0] - 2026-01-16

Major API reorganization for improved LLM workflow support. Reduces tool count from 16 to 11 through consolidation and standardizes parameter naming across all tools.

### Added

- `get_workitem_details`: New traceability parameters (`linkDirection`, `linkTypeFilter`, `followLevels`) for recursive link traversal
- `list_documents`: Consolidated tool with optional `space` and `titleFilter` parameters
- `get_workitem`: Consolidated tool with optional `revision` parameter

### Changed

- **BREAKING:** Tool renames:
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
- **BREAKING:** Parameter renames:
  - `documentName` → `documentTitle` (document tools)
  - `documentRevision` → `revision` (document tools)
  - `documentNumber` → `sectionNumber` (`get_document_section`)
  - `textSearchTerms` → `searchQuery` (`search_in_document`)
  - `moduleFolder` → `space` (module tools)
  - `workItemId` → `workitemId` (workitem tools)
  - `customFieldWhitelist` → `customFields` (detail tools)

### Removed

- `get_documents_by_space_names` (merged into `list_documents`)
- `get_text_for_workitem_at_revision` (merged into `get_workitem`)
- `get_revisions_list_for_workitem` (merged into `get_workitem_history`)

## [0.5.3] - 2025-12-02

### Added

- Add document branching support with two new MCP tools:
  - `get_workitems_in_branched_document` - Retrieves work items from a branched document at a specific revision using a 4-step revision-aware algorithm that correctly fetches historical versions when they differ from HEAD
  - `get_workitems_in_module` - Queries work items using SQL against the `REL_MODULE_WORKITEM` relationship table for fast retrieval of module contents

### Changed

- Upgrade Polarion package from 0.3.2 to 0.3.3 to fix URI format issues in `getModuleWorkItemUris` API

## [0.5.2] - 2025-12-02

### Added

- add Id column to get_documents tool output

## [0.5.1] - 2025-12-02

### Changed

- Rename and clean up GetDetailsForDocuments tool

## [0.5.0] - 2025-12-02

### Added

- Add new tool `get_details_for_documents` to retrieve comprehensive details for Polarion documents/modules
  - Supports retrieving standard fields and custom fields
  - Allows filtering custom fields with whitelist, 'all', or 'none' options
  - Uses configurable default fields from project configuration
- Add `PolarionDocumentDefaultFields` property to `PolarionProjectConfig` to define default document fields to retrieve when no specific fields are requested

## [0.4.9] - 2025-10-18

### Added

- Add new tool `get_text_for_workitem_at_revision` to retrieve a single work item at a specific revision

### Changed

- Upgrade Polarion package from 0.3.1 to 0.3.2 to support updated API interfaces
- Update `get_revisions_content_for_workitem` tool to handle dictionary return type with revision IDs as keys

## [0.4.8] - 2025-10-18

### Added

- Add `get_revisions_list_for_workitem` tool to retrieve revision IDs for a work item
- Add `get_revisions_content_for_workitem` tool to retrieve detailed content at each revision

### Changed

- Upgrade Polarion package from 0.2.1 to 0.3.1 for access to new revision-oriented apis

## [0.4.7] - 2025-10-15

### Added

- Add case-insensitive space name filtering in GetDocumentsInSpace

### Changed

- Upgrade Polarion package from 0.2.0 to 0.2.1 to allow get_documents to be case insensative

## [0.4.6] - 2025-10-13

### Changed

- Update SearchWorkitemsInDocument to support the updated attribute to see last update timestamp

## [0.4.5] - 2025-10-13

### Changed

- Update SearchWorkitemsInDocument to accommodate cline using Lucene paraenthesis
- Migrate to the latest mcp sdk `0.4.0-preview.2` to support streamable http

## [0.4.4] - 2025-10-10

### Added

- Add startup version detection and reporting to console and logs
- Add environment variables support for configuration overrides
- Enable detailed logging of loaded Polarion project configurations
- Add reflection and diagnostics imports for version detection

## [0.4.3] - 2025-09-22

Refactor MCP server architecture and consolidate configuration

### Changed

- Split PolarionClientFactory into stdio and remote implementations
- Move shared components to PolarionMcpTools library
- Replace polarion-mcp.config.json with appsettings.json
- Add IPolarionClientFactory interface for dependency injection
- Update VS Code launch configurations for new project structure
- Consolidate project configurations and field mappings

## [0.4.2] - 2025-06-18

### Changed

- Update Polarion package to version 0.2.0 and adapt API calls
  - Better support for LateX and polarion-rte-link cross references in the Markdown outputs
- In `get_details_for_workitems`, swap incoming/outgoing linked WorkItems sections

## [0.4.1] - 2025-06-03

### Fixed

- Fix issue with `get_sections_in_document` tool

## [0.4.0] - 2025-06-03

### Added

- Add WorkItemPrefix property and update space names description
- Add new tool `get_sections_in_document`
- Add new tool `get_section_content_for_document`

### Changed

- Upgrade `Polarion` to 0.1.0

## [0.3.4] - 2025-05-21

### Added

- Add support for User arrays in Utils

## [0.3.3] - 2025-05-21

### Added

- Introduce `McpTools_GetDetailsForWorkItems`: A new tool to fetch comprehensive details for WorkItems, allowing users to specify fields or use a new default set.
- Add `PolarionWorkItemDefaultFields` to `PolarionProjectConfig`: Enables defining a default list of fields to retrieve for WorkItems when no specific fields are requested.
- Add `McpTools_ListAvaialbeCustomFieldsForWorkItemTypes`: A new tool to list all available custom fields for different WorkItem types.

### Changed

- Update `McpTools_ListConfiguredWorkItemTypes`, `Utils.cs`, `appsettings.json`, and `README.md` to support these enhancements and document the new functionality.

### Removed

- Remove deprecated tools: `McpTools_GetConfiguredCustomFields` and `McpTools_GetCustomFieldsForWorkItems`.

## [0.3.2] - 2025-05-21

### Added

- Added per-project configuration (`PolarionWorkItemTypes` in `appsettings.json` under each project) to define specific custom fields to retrieve for different WorkItem types.
- Added MCP Tool: `get_configured_custom_fields` - Retrieves the list of custom fields configured for a specific WorkItem type ID, based on the current project's settings.
- Added MCP Tool: `list_configured_workitem_types` - Lists all WorkItem type IDs that have custom field configurations defined in the current project's settings.
- Added MCP Tool: `get_custom_fields_for_workitems` - Retrieves specified custom field values for a given list of WorkItem IDs.

### Changed

- Update parameter description for `GetDocuments` method

## [0.3.1] - 2025-05-16

### Added

- Add the following tools:
  - SearchWorkitemsInDocument

## [0.3.0] - 2025-05-14

### Added

- Add the following tools:
  - GetDocuments
  - GetDocumentInSpaceNames
  - GetSpaceNames
- Add the following configuration options:
  - BlacklistSpaceContainingMatch

## [0.2.0] - 2025-05-12

### Changed

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

## [0.1.3] - 2025-05-08

### Changed

- Upgrated to ModelContextProtocol 0.1.0-preview.12
- Migrate to creating the Polarion Client per Tool call to ensure the polarion client is always available and does not go stale.

## [0.1.2] - 2025-05-07

### Changed

- Remote server has ReadWorkItems tool prompt tweaked, added error codes and improved error messages.

## [0.1.1] - 2025-05-07

### Added

- Now publishing under peakflames/polarion-remote-mcp-server on Docker Hub

### Changed

- Embed the image tag in the cspoj file
- Update the README

## [0.1.0] - 2025-04-11

Initial release

[Unreleased]: https://github.com/peakflames/PolarionMcpServers/compare/v0.16.0...HEAD
[0.16.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.15.0...v0.16.0
[0.15.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/peakflames/PolarionMcpServers/compare/ec11d99...v0.6.0
[0.5.3]: https://github.com/peakflames/PolarionMcpServers/compare/v0.5.2...ec11d99
[0.5.2]: https://github.com/peakflames/PolarionMcpServers/compare/v0.5.1...v0.5.2
[0.5.1]: https://github.com/peakflames/PolarionMcpServers/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.9...v0.5.0
[0.4.9]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.8...v0.4.9
[0.4.8]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.7...v0.4.8
[0.4.7]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.6...v0.4.7
[0.4.6]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.5...v0.4.6
[0.4.5]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.4...v0.4.5
[0.4.4]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.3...v0.4.4
[0.4.3]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.2...v0.4.3
[0.4.2]: https://github.com/peakflames/PolarionMcpServers/compare/9193664...v0.4.2
[0.4.1]: https://github.com/peakflames/PolarionMcpServers/compare/v0.4.0...9193664
[0.4.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.3.4...v0.4.0
[0.3.4]: https://github.com/peakflames/PolarionMcpServers/compare/v0.3.3...v0.3.4
[0.3.3]: https://github.com/peakflames/PolarionMcpServers/compare/v0.3.2...v0.3.3
[0.3.2]: https://github.com/peakflames/PolarionMcpServers/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/peakflames/PolarionMcpServers/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/peakflames/PolarionMcpServers/compare/v0.1.3...v0.2.0
[0.1.3]: https://github.com/peakflames/PolarionMcpServers/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/peakflames/PolarionMcpServers/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/peakflames/PolarionMcpServers/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/peakflames/PolarionMcpServers/releases/tag/v0.1.0
