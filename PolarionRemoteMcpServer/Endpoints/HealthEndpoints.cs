using System.Reflection;

namespace PolarionRemoteMcpServer.Endpoints;

/// <summary>
/// Health and version endpoints for the REST API.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health and version endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Simple health check endpoint
        app.MapGet("api/health", () => Results.Json("Healthy", PolarionRestApiJsonContext.Default.String))
            .WithTags("Health")
            .WithName("HealthCheck")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Health check endpoint";
                operation.Description = "Returns 'Healthy' if the service is running.";
                return operation;
            });

        // Version endpoint that returns the current version of the API
        app.MapGet("api/version", () =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";
                var informationalVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";

                return Results.Json(new VersionInfo
                {
                    Version = version,
                    InformationalVersion = informationalVersion
                }, PolarionRestApiJsonContext.Default.VersionInfo);
            })
            .WithTags("Health")
            .WithName("VersionInfo")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Version information endpoint";
                operation.Description = "Returns the current version of the API.";
                return operation;
            });

        return app;
    }
}

/// <summary>
/// Version information response.
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// The assembly version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The informational version (may include git commit info).
    /// </summary>
    public string InformationalVersion { get; set; } = string.Empty;
}
