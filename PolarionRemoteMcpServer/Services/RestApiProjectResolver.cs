using System.Diagnostics.CodeAnalysis;
using FluentResults;
using Polarion;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Services;

/// <summary>
/// Resolves Polarion project configurations for REST API endpoints.
/// Unlike MCP endpoints which use ProjectUrlAlias, REST API endpoints match against
/// the actual Polarion ProjectId (SessionConfig.ProjectId) for compatibility with
/// the native Polarion REST API.
/// </summary>
public class RestApiProjectResolver
{
    private readonly List<PolarionProjectConfig> _projectConfigs;
    private readonly ILogger<RestApiProjectResolver> _logger;

    public RestApiProjectResolver(
        List<PolarionProjectConfig> projectConfigs,
        ILogger<RestApiProjectResolver> logger)
    {
        _projectConfigs = projectConfigs;
        _logger = logger;
    }

    /// <summary>
    /// Gets the project configuration matching the given Polarion project ID.
    /// Matches against SessionConfig.ProjectId (the actual Polarion project ID),
    /// NOT the ProjectUrlAlias used by MCP endpoints.
    /// Returns null if no matching configuration is found (no fallback to default).
    /// </summary>
    /// <param name="projectId">The Polarion project ID from the REST API route.</param>
    /// <returns>The matching project configuration, or null if not found.</returns>
    public PolarionProjectConfig? GetProjectConfig(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            _logger.LogWarning("REST API: Empty projectId provided");
            return null;
        }

        var config = _projectConfigs.FirstOrDefault(p =>
            p.SessionConfig?.ProjectId != null &&
            p.SessionConfig.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            _logger.LogWarning("REST API: No configuration found for Polarion project ID '{ProjectId}'. " +
                "Available projects: [{AvailableProjects}]",
                projectId,
                string.Join(", ", _projectConfigs
                    .Where(p => p.SessionConfig?.ProjectId != null)
                    .Select(p => p.SessionConfig!.ProjectId)));
        }
        else
        {
            _logger.LogDebug("REST API: Found configuration for Polarion project ID '{ProjectId}'", projectId);
        }

        return config;
    }

    /// <summary>
    /// Creates a Polarion client for the given project ID.
    /// Returns a failure result if no matching configuration is found.
    /// </summary>
    /// <param name="projectId">The Polarion project ID from the REST API route.</param>
    /// <returns>A Result containing the Polarion client or an error.</returns>
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public async Task<Result<IPolarionClient>> CreateClientAsync(string projectId)
    {
        var config = GetProjectConfig(projectId);
        if (config == null)
        {
            return Result.Fail($"Project '{projectId}' not found. Ensure the project is configured in appsettings.json.");
        }

        if (config.SessionConfig == null)
        {
            return Result.Fail($"Project '{projectId}' has no SessionConfig defined.");
        }

        _logger.LogDebug("REST API: Creating Polarion client for project '{ProjectId}' on server '{ServerUrl}'",
            projectId, config.SessionConfig.ServerUrl);

        var clientResult = await PolarionClient.CreateAsync(config.SessionConfig);
        if (clientResult.IsFailed)
        {
            var errorMessage = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogError("REST API: Failed to create Polarion client for project '{ProjectId}': {Error}",
                projectId, errorMessage);
            return Result.Fail($"Failed to connect to Polarion for project '{projectId}': {errorMessage}");
        }

        // Convert PolarionClient to IPolarionClient
        return Result.Ok<IPolarionClient>(clientResult.Value);
    }

    /// <summary>
    /// Gets all configured Polarion project IDs.
    /// </summary>
    /// <returns>List of configured Polarion project IDs.</returns>
    public IEnumerable<string> GetConfiguredProjectIds()
    {
        return _projectConfigs
            .Where(p => p.SessionConfig?.ProjectId != null)
            .Select(p => p.SessionConfig!.ProjectId);
    }
}
