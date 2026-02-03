using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Models.JsonApi;
using PolarionRemoteMcpServer.Services;
using Serilog;

namespace PolarionRemoteMcpServer.Endpoints;

/// <summary>
/// REST API endpoints for WorkItem operations, compatible with Polarion REST API format.
/// Uses SessionConfig.ProjectId for project matching (not ProjectUrlAlias).
/// </summary>
public static class WorkItemsEndpoints
{
    /// <summary>
    /// Maps WorkItem REST endpoints to the application.
    /// </summary>
    public static void MapWorkItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/polarion/rest/v1/projects/{projectId}");

        group.MapGet("/workitems", SearchWorkItems)
            .RequireAuthorization(ApiScopes.PolarionRead);
        group.MapGet("/workitems/{workitemId}", GetWorkItem)
            .RequireAuthorization(ApiScopes.PolarionRead);
        group.MapGet("/workitems/{workitemId}/revisions", GetWorkItemRevisions)
            .RequireAuthorization(ApiScopes.PolarionRead);
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetWorkItem(
        string projectId,
        string workitemId,
        RestApiProjectResolver projectResolver)
    {
        Log.Debug("REST API: GetWorkItem called for project={ProjectId}, workitemId={WorkitemId}",
            projectId, workitemId);

        if (string.IsNullOrWhiteSpace(workitemId))
        {
            return CreateErrorResponse("400", "Bad Request", "workitemId parameter cannot be empty.");
        }

        // Get project config - matches against SessionConfig.ProjectId, no fallback
        var projectConfig = projectResolver.GetProjectConfig(projectId);
        if (projectConfig == null)
        {
            return CreateNotFoundResponse(projectId, projectResolver.GetConfiguredProjectIds());
        }

        // Create client for this project
        var clientResult = await projectResolver.CreateClientAsync(projectId);
        if (clientResult.IsFailed)
        {
            var errorMsg = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            Log.Error("REST API: Failed to create Polarion client: {Error}", errorMsg);
            return CreateErrorResponse("500", "Internal Server Error", errorMsg);
        }

        var polarionClient = clientResult.Value;

        try
        {
            var workItemResult = await polarionClient.GetWorkItemByIdAsync(workitemId);
            if (workItemResult.IsFailed)
            {
                var errorMsg = workItemResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get work item {WorkitemId}: {Error}", workitemId, errorMsg);
                return CreateErrorResponse("404", "Not Found", $"WorkItem '{workitemId}' not found: {errorMsg}");
            }

            var workItem = workItemResult.Value;
            if (workItem == null)
            {
                return CreateErrorResponse("404", "Not Found", $"WorkItem '{workitemId}' not found.");
            }

            var resource = new WorkItemResource
            {
                Id = $"{projectId}/{workitemId}",
                Attributes = new WorkItemAttributes
                {
                    Title = workItem.title,
                    Type = workItem.type?.id,
                    Status = workItem.status?.id,
                    OutlineNumber = workItem.outlineNumber,
                    Created = workItem.createdSpecified ? workItem.created : null,
                    Updated = workItem.updatedSpecified ? workItem.updated : null,
                    Author = workItem.author?.id,
                    Severity = workItem.severity?.id,
                    Priority = workItem.priority?.id,
                    Description = workItem.description?.content
                },
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{workitemId}"
                }
            };

            var response = new JsonApiDocument<WorkItemResource>
            {
                Data = resource,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{workitemId}"
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentWorkItemResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting work item {WorkitemId}", workitemId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetWorkItemRevisions(
        string projectId,
        string workitemId,
        RestApiProjectResolver projectResolver,
        [FromQuery(Name = "page[size]")] int pageSize = 100)
    {
        // Clamp pageSize: min 1, max 500
        if (pageSize < 1)
        {
            pageSize = 1;
        }
        else if (pageSize > 500)
        {
            pageSize = 500;
        }

        Log.Debug("REST API: GetWorkItemRevisions called for project={ProjectId}, workitemId={WorkitemId}, pageSize={PageSize}",
            projectId, workitemId, pageSize);

        if (string.IsNullOrWhiteSpace(workitemId))
        {
            return CreateErrorResponse("400", "Bad Request", "workitemId parameter cannot be empty.");
        }

        // Get project config - matches against SessionConfig.ProjectId, no fallback
        var projectConfig = projectResolver.GetProjectConfig(projectId);
        if (projectConfig == null)
        {
            return CreateNotFoundResponse(projectId, projectResolver.GetConfiguredProjectIds());
        }

        // Create client for this project
        var clientResult = await projectResolver.CreateClientAsync(projectId);
        if (clientResult.IsFailed)
        {
            var errorMsg = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            Log.Error("REST API: Failed to create Polarion client: {Error}", errorMsg);
            return CreateErrorResponse("500", "Internal Server Error", errorMsg);
        }

        var polarionClient = clientResult.Value;

        try
        {
            var revisionsResult = await polarionClient.GetWorkItemRevisionsByIdAsync(workitemId, pageSize);
            if (revisionsResult.IsFailed)
            {
                var errorMsg = revisionsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get revisions for {WorkitemId}: {Error}", workitemId, errorMsg);
                return CreateErrorResponse("404", "Not Found", $"Revisions for WorkItem '{workitemId}' not found: {errorMsg}");
            }

            var revisionsDict = revisionsResult.Value;
            var resources = new List<WorkItemRevisionResource>();

            if (revisionsDict != null)
            {
                foreach (var kvp in revisionsDict)
                {
                    var revisionId = kvp.Key;
                    var revision = kvp.Value;

                    var resource = new WorkItemRevisionResource
                    {
                        Id = $"{projectId}/{workitemId}/{revisionId}",
                        Attributes = new WorkItemRevisionAttributes
                        {
                            Name = revisionId,
                            Created = revision.updatedSpecified ? revision.updated : null,
                            Author = revision.author?.id,
                            Title = revision.title,
                            Status = revision.status?.id,
                            Description = revision.description?.content
                        },
                        Links = new JsonApiLinks
                        {
                            Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{workitemId}/revisions/{revisionId}"
                        }
                    };
                    resources.Add(resource);
                }
            }

            var response = new JsonApiDocument<List<WorkItemRevisionResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{workitemId}/revisions"
                },
                Meta = new JsonApiMeta
                {
                    Count = resources.Count
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListWorkItemRevisionResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting revisions for work item {WorkitemId}", workitemId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    private static IResult CreateNotFoundResponse(string projectId, IEnumerable<string> availableProjects)
    {
        var availableList = string.Join(", ", availableProjects);
        var detail = string.IsNullOrEmpty(availableList)
            ? $"Project '{projectId}' not found. No projects are configured."
            : $"Project '{projectId}' not found. Available projects: {availableList}";

        return CreateErrorResponse("404", "Not Found", detail);
    }

    private static IResult CreateErrorResponse(string status, string title, string detail)
    {
        var errorResponse = new JsonApiDocument<object>
        {
            Errors = new List<JsonApiError>
            {
                new JsonApiError
                {
                    Status = status,
                    Title = title,
                    Detail = detail
                }
            }
        };

        var statusCode = int.Parse(status);
        return Results.Json(errorResponse, PolarionRestApiJsonContext.Default.JsonApiDocumentObject, statusCode: statusCode);
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> SearchWorkItems(
        string projectId,
        RestApiProjectResolver projectResolver,
        [FromQuery] string? query = null,
        [FromQuery] string? types = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = "created",
        [FromQuery(Name = "page[size]")] int pageSize = 50)
    {
        Log.Debug("REST API: SearchWorkItems called for project={ProjectId}, query={Query}, types={Types}, status={Status}",
            projectId, query, types, status);

        // Validate query parameter
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateErrorResponse("400", "Bad Request", "query parameter is required.");
        }

        // Clamp pageSize
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 500) pageSize = 500;

        // Validate sort field and direction
        var sortField = sort ?? "created";
        var sortDescending = sortField.StartsWith("-");
        if (sortDescending) sortField = sortField[1..];

        var validSortFields = new[] { "created", "updated", "id", "title" };
        if (!validSortFields.Contains(sortField.ToLower()))
        {
            return CreateErrorResponse("400", "Bad Request",
                $"Invalid sort field '{sort}'. Must be one of: {string.Join(", ", validSortFields)} (prefix with '-' for descending)");
        }

        // Get project config
        var projectConfig = projectResolver.GetProjectConfig(projectId);
        if (projectConfig == null)
        {
            return CreateNotFoundResponse(projectId, projectResolver.GetConfiguredProjectIds());
        }

        // Create client
        var clientResult = await projectResolver.CreateClientAsync(projectId);
        if (clientResult.IsFailed)
        {
            var errorMsg = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            Log.Error("REST API: Failed to create Polarion client: {Error}", errorMsg);
            return CreateErrorResponse("500", "Internal Server Error", errorMsg);
        }

        var polarionClient = clientResult.Value;

        try
        {
            // Build Lucene query (reuse logic from MCP tool)
            var luceneQuery = BuildLuceneQuery(query, types, status);

            // Default field list
            var fieldList = GetSearchFieldList();

            // Call Polarion API
            var searchResult = await polarionClient.SearchWorkitemAsync(
                luceneQuery,
                sortField.ToLower(),
                fieldList);

            if (searchResult.IsFailed)
            {
                var errorMsg = searchResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Search failed: {Error}", errorMsg);

                if (errorMsg.Contains("parse", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateErrorResponse("400", "Bad Request",
                        $"Invalid Lucene query syntax: {errorMsg}");
                }

                return CreateErrorResponse("500", "Internal Server Error", errorMsg);
            }

            var workItems = searchResult.Value ?? Array.Empty<Polarion.Generated.Tracker.WorkItem>();

            // Convert to JSON:API format
            var resources = workItems
                .Take(pageSize)
                .Select(wi => new WorkItemResource
                {
                    Id = $"{projectId}/{wi.id}",
                    Attributes = new WorkItemAttributes
                    {
                        Title = wi.title,
                        Type = wi.type?.id,
                        Status = wi.status?.id,
                        OutlineNumber = wi.outlineNumber,
                        Created = wi.createdSpecified ? wi.created : null,
                        Updated = wi.updatedSpecified ? wi.updated : null,
                        Author = wi.author?.id,
                        Assignee = wi.assignee != null && wi.assignee.Length > 0
                            ? wi.assignee[0]?.id
                            : null,
                        Description = wi.description?.content
                    },
                    Links = new JsonApiLinks
                    {
                        Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{wi.id}"
                    }
                })
                .ToList();

            var queryString = $"query={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrWhiteSpace(types))
                queryString += $"&types={Uri.EscapeDataString(types)}";
            if (!string.IsNullOrWhiteSpace(status))
                queryString += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrWhiteSpace(sort))
                queryString += $"&sort={Uri.EscapeDataString(sort)}";
            queryString += $"&page[size]={pageSize}";

            var response = new JsonApiDocument<List<WorkItemResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/workitems?{queryString}"
                },
                Meta = new WorkItemSearchMeta
                {
                    Count = resources.Count,
                    Query = query,
                    LuceneQuery = luceneQuery,
                    TypeFilter = types,
                    StatusFilter = status
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListWorkItemResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception during work item search");
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    /// <summary>
    /// Builds a Lucene query from user inputs.
    /// Same logic as the search_workitems MCP tool.
    /// </summary>
    private static string BuildLuceneQuery(string searchQuery, string? itemTypes, string? statusFilter)
    {
        var queryParts = new List<string>();

        // Text search (searches ALL indexed fields in Polarion)
        var textQuery = BuildTextSearchQuery(searchQuery);
        if (!string.IsNullOrWhiteSpace(textQuery))
        {
            queryParts.Add($"({textQuery})");
        }

        // Type filter: (type:requirement OR type:testCase)
        if (!string.IsNullOrWhiteSpace(itemTypes))
        {
            var types = itemTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => $"type:{t}");

            var typeQuery = types.Count() == 1
                ? types.First()
                : $"({string.Join(" OR ", types)})";
            queryParts.Add(typeQuery);
        }

        // Status filter: (status:open OR status:in-progress)
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var statuses = statusFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => $"status:{s}");

            var statusQuery = statuses.Count() == 1
                ? statuses.First()
                : $"({string.Join(" OR ", statuses)})";
            queryParts.Add(statusQuery);
        }

        // Combine with AND
        return string.Join(" AND ", queryParts);
    }

    /// <summary>
    /// Builds the text search portion of the Lucene query.
    /// Supports exact phrases, AND logic, and OR logic (default).
    /// </summary>
    private static string BuildTextSearchQuery(string searchQuery)
    {
        var trimmed = searchQuery.Trim();

        // Exact phrase: "rigging timeout"
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length > 2)
        {
            return trimmed;
        }

        // AND logic: HVBIT AND timeout
        if (trimmed.Contains(" AND ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // OR logic (default): HVBIT timeout → (HVBIT OR timeout)
        var terms = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 1)
        {
            return terms[0];
        }

        return $"({string.Join(" OR ", terms)})";
    }

    /// <summary>
    /// Returns the default list of fields to retrieve from Polarion for search results.
    /// </summary>
    private static List<string> GetSearchFieldList()
    {
        return new List<string>
        {
            "id", "title", "type", "status", "description",
            "updated", "created", "outlineNumber", "author", "assignee"
        };
    }
}
