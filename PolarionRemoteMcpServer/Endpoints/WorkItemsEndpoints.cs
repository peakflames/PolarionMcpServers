using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PolarionMcpTools;
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

        group.MapGet("/workitems/{workitemId}", GetWorkItem);
        group.MapGet("/workitems/{workitemId}/revisions", GetWorkItemRevisions);
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
        int? limit = 10)
    {
        Log.Debug("REST API: GetWorkItemRevisions called for project={ProjectId}, workitemId={WorkitemId}, limit={Limit}",
            projectId, workitemId, limit);

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
            var revisionsResult = await polarionClient.GetWorkItemRevisionsByIdAsync(workitemId, limit ?? 10);
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
                    TotalCount = resources.Count
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
}
