using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Models.JsonApi;
using PolarionRemoteMcpServer.Services;
using Serilog;

namespace PolarionRemoteMcpServer.Endpoints;

/// <summary>
/// REST API endpoints for Space operations, compatible with Polarion REST API format.
/// Uses SessionConfig.ProjectId for project matching (not ProjectUrlAlias).
/// </summary>
public static class SpacesEndpoints
{
    /// <summary>
    /// Maps Spaces REST endpoints to the application.
    /// </summary>
    public static void MapSpacesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/polarion/rest/v1/projects/{projectId}");

        group.MapGet("/spaces", GetSpaces)
            .RequireAuthorization(ApiScopes.PolarionRead);
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<IResult> GetSpaces(
        string projectId,
        RestApiProjectResolver projectResolver)
    {
        Log.Debug("REST API: GetSpaces called for project={ProjectId}", projectId);

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
            string? blacklistPattern = projectConfig.BlacklistSpaceContainingMatch;

            var spacesResult = await polarionClient.GetSpacesAsync(blacklistPattern);
            if (spacesResult.IsFailed)
            {
                var errorMsg = spacesResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                Log.Warning("REST API: Failed to get spaces: {Error}", errorMsg);
                return CreateErrorResponse("500", "Internal Server Error", errorMsg);
            }

            var spaces = spacesResult.Value;
            var resources = new List<SpaceResource>();

            foreach (var spaceName in spaces)
            {
                var resource = new SpaceResource
                {
                    Id = $"{projectId}/{spaceName}",
                    Attributes = new SpaceAttributes
                    {
                        Name = spaceName
                    },
                    Links = new JsonApiLinks
                    {
                        Self = $"/polarion/rest/v1/projects/{projectId}/spaces/{spaceName}"
                    }
                };
                resources.Add(resource);
            }

            var response = new JsonApiDocument<List<SpaceResource>>
            {
                Data = resources,
                Links = new JsonApiLinks
                {
                    Self = $"/polarion/rest/v1/projects/{projectId}/spaces"
                },
                Meta = new JsonApiMeta
                {
                    TotalCount = resources.Count
                }
            };

            return Results.Json(response, PolarionRestApiJsonContext.Default.JsonApiDocumentListSpaceResource);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "REST API: Exception getting spaces");
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
