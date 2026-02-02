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
/// REST API endpoints for Document operations, compatible with Polarion REST API format.
/// Uses SessionConfig.ProjectId for project matching (not ProjectUrlAlias).
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>
    /// Maps Document REST endpoints to the application.
    /// </summary>
    public static void MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}");

        group.MapGet("/documents", GetDocuments);
        group.MapGet("/documents/{documentId}", GetDocument);
        group.MapGet("/documents/{documentId}/workitems", GetDocumentWorkItems);
        group.MapGet("/documents/{documentId}/revisions", GetDocumentRevisions);
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetDocuments(
        string projectId,
        string spaceId,
        RestApiProjectResolver projectResolver)
    {
        Log.Debug("REST API: GetDocuments called for project={ProjectId}, space={SpaceId}", projectId, spaceId);

        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return CreateErrorResponse("400", "Bad Request", "spaceId parameter cannot be empty.");
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
            var documentsResult = await polarionClient.GetModulesInSpaceThinAsync(spaceId);
            if (documentsResult.IsFailed)
            {
                var errorMsg = documentsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get documents for space {SpaceId}: {Error}", spaceId, errorMsg);
                return CreateErrorResponse("500", "Internal Server Error", errorMsg);
            }

            var documents = documentsResult.Value;
            var resources = new List<DocumentResource>();

            foreach (var doc in documents)
            {
                var resource = new DocumentResource
                {
                    Id = $"{projectId}/{spaceId}/{doc.Id}",
                    Attributes = new DocumentAttributes
                    {
                        ModuleName = doc.Id,
                        Title = doc.Title,
                        Type = doc.Type,
                        Status = doc.Status,
                        ModuleFolder = doc.Space
                    },
                    Links = new JsonApiLinks
                    {
                        Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{doc.Id}"
                    }
                };
                resources.Add(resource);
            }

            var response = new JsonApiDocument<List<DocumentResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents"
                },
                Meta = new JsonApiMeta
                {
                    TotalCount = resources.Count
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListDocumentResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting documents for space {SpaceId}", spaceId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetDocument(
        string projectId,
        string spaceId,
        string documentId,
        RestApiProjectResolver projectResolver)
    {
        Log.Debug("REST API: GetDocument called for project={ProjectId}, space={SpaceId}, document={DocumentId}",
            projectId, spaceId, documentId);

        if (string.IsNullOrWhiteSpace(spaceId) || string.IsNullOrWhiteSpace(documentId))
        {
            return CreateErrorResponse("400", "Bad Request", "spaceId and documentId parameters cannot be empty.");
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
            var documentLocation = $"{spaceId}/{documentId}";
            var documentResult = await polarionClient.GetModuleByLocationAsync(documentLocation);
            if (documentResult.IsFailed)
            {
                var errorMsg = documentResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get document {DocumentLocation}: {Error}", documentLocation, errorMsg);
                return CreateErrorResponse("404", "Not Found", $"Document '{documentLocation}' not found: {errorMsg}");
            }

            var doc = documentResult.Value;
            if (doc == null)
            {
                return CreateErrorResponse("404", "Not Found", $"Document '{documentLocation}' not found.");
            }

            var resource = new DocumentResource
            {
                Id = $"{projectId}/{spaceId}/{documentId}",
                Attributes = new DocumentAttributes
                {
                    ModuleName = doc.moduleName,
                    Title = doc.title,
                    Type = doc.type?.id,
                    Status = doc.status?.id,
                    HomePageContent = doc.homePageContent?.content,
                    Created = doc.createdSpecified ? doc.created : null,
                    Updated = doc.updatedSpecified ? doc.updated : null,
                    UpdatedBy = doc.updatedBy?.id,
                    ModuleLocation = doc.moduleLocation,
                    ModuleFolder = doc.moduleFolder
                },
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}"
                }
            };

            var response = new JsonApiDocument<DocumentResource>
            {
                Data = resource,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}"
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentDocumentResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting document {SpaceId}/{DocumentId}", spaceId, documentId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetDocumentWorkItems(
        string projectId,
        string spaceId,
        string documentId,
        RestApiProjectResolver projectResolver,
        string? types = null)
    {
        Log.Debug("REST API: GetDocumentWorkItems called for project={ProjectId}, space={SpaceId}, document={DocumentId}, types={Types}",
            projectId, spaceId, documentId, types);

        if (string.IsNullOrWhiteSpace(spaceId) || string.IsNullOrWhiteSpace(documentId))
        {
            return CreateErrorResponse("400", "Bad Request", "spaceId and documentId parameters cannot be empty.");
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
            // Parse item types if provided
            List<string>? typeList = null;
            if (!string.IsNullOrWhiteSpace(types))
            {
                typeList = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            var workItemsResult = await polarionClient.QueryWorkItemsInModuleAsync(spaceId, documentId, typeList);
            if (workItemsResult.IsFailed)
            {
                var errorMsg = workItemsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get work items for {SpaceId}/{DocumentId}: {Error}",
                    spaceId, documentId, errorMsg);
                return CreateErrorResponse("500", "Internal Server Error", errorMsg);
            }

            var workItems = workItemsResult.Value ?? Array.Empty<Polarion.Generated.Tracker.WorkItem>();
            var resources = new List<WorkItemResource>();

            foreach (var workItem in workItems)
            {
                if (workItem == null) continue;

                var resource = new WorkItemResource
                {
                    Id = $"{projectId}/{workItem.id}",
                    Attributes = new WorkItemAttributes
                    {
                        Title = workItem.title,
                        Type = workItem.type?.id,
                        Status = workItem.status?.id,
                        OutlineNumber = workItem.outlineNumber,
                        Created = workItem.createdSpecified ? workItem.created : null,
                        Updated = workItem.updatedSpecified ? workItem.updated : null,
                        Author = workItem.author?.id,
                        Description = workItem.description?.content
                    },
                    Links = new JsonApiLinks
                    {
                        Self = $"/polarion/rest/v1/projects/{projectId}/workitems/{workItem.id}"
                    }
                };
                resources.Add(resource);
            }

            var response = new JsonApiDocument<List<WorkItemResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/workitems"
                },
                Meta = new JsonApiMeta
                {
                    TotalCount = resources.Count
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListWorkItemResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting work items for document {SpaceId}/{DocumentId}", spaceId, documentId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetDocumentRevisions(
        string projectId,
        string spaceId,
        string documentId,
        RestApiProjectResolver projectResolver,
        int limit = 10)
    {
        Log.Debug("REST API: GetDocumentRevisions called for project={ProjectId}, space={SpaceId}, document={DocumentId}, limit={Limit}",
            projectId, spaceId, documentId, limit);

        if (string.IsNullOrWhiteSpace(spaceId) || string.IsNullOrWhiteSpace(documentId))
        {
            return CreateErrorResponse("400", "Bad Request", "spaceId and documentId parameters cannot be empty.");
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
            var location = $"{spaceId}/{documentId}";
            var revisionsResult = await polarionClient.GetModuleRevisionsByLocationAsync(location, limit);
            if (revisionsResult.IsFailed)
            {
                var errorMsg = revisionsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get revisions for {Location}: {Error}", location, errorMsg);
                return CreateErrorResponse("500", "Internal Server Error", errorMsg);
            }

            var revisions = revisionsResult.Value ?? Array.Empty<Polarion.Generated.Tracker.Module>();
            var resources = new List<DocumentRevisionResource>();

            foreach (var revision in revisions)
            {
                // Extract revision ID from URI
                var revisionId = ExtractRevisionIdFromUri(revision.uri);

                var resource = new DocumentRevisionResource
                {
                    Id = $"{projectId}/{spaceId}/{documentId}/{revisionId}",
                    Attributes = new DocumentRevisionAttributes
                    {
                        Name = revisionId,
                        Created = revision.updatedSpecified ? revision.updated : null,
                        Author = revision.updatedBy?.id,
                        Title = revision.title,
                        Status = revision.status?.id
                    },
                    Links = new JsonApiLinks
                    {
                        Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/revisions/{revisionId}"
                    }
                };
                resources.Add(resource);
            }

            var response = new JsonApiDocument<List<DocumentRevisionResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceId}/documents/{documentId}/revisions"
                },
                Meta = new JsonApiMeta
                {
                    TotalCount = resources.Count
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListDocumentRevisionResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting revisions for document {SpaceId}/{DocumentId}", spaceId, documentId);
            return CreateErrorResponse("500", "Internal Server Error", ex.Message);
        }
    }

    /// <summary>
    /// Extracts the revision ID from a Polarion module URI.
    /// </summary>
    private static string ExtractRevisionIdFromUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return "N/A";
        }

        // Try percent format first (e.g., ...%611906)
        var percentIndex = uri.LastIndexOf('%');
        if (percentIndex >= 0 && percentIndex < uri.Length - 1)
        {
            return uri[(percentIndex + 1)..];
        }

        // Fall back to query format (e.g., ...?revision=611906)
        var revisionIndex = uri.IndexOf("?revision=", StringComparison.OrdinalIgnoreCase);
        if (revisionIndex >= 0)
        {
            return uri[(revisionIndex + 10)..];
        }

        return "N/A";
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
