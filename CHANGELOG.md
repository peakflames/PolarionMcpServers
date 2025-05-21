# Changelog

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
