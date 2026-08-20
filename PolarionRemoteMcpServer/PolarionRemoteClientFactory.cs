
using System.Diagnostics.CodeAnalysis;
using FluentResults;
using Polarion;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Credentials;

namespace PolarionRemoteMcpServer
{
    public class PolarionRemoteClientFactory : IPolarionClientFactory
    {
        private readonly List<PolarionProjectConfig> _projectConfigs; // Changed from single configuration
        private readonly ILogger<PolarionRemoteClientFactory> _logger;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IUpstreamCredentialResolver _credentialResolver;

        // Constructor updated to inject the list of project configurations
        public PolarionRemoteClientFactory(
            List<PolarionProjectConfig> projectConfigs, // Changed parameter type
            ILogger<PolarionRemoteClientFactory> logger,
            IHttpContextAccessor? httpContextAccessor,
            IUpstreamCredentialResolver credentialResolver)
        {
            _projectConfigs = projectConfigs; // Assign the injected list
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _credentialResolver = credentialResolver;
        }

        // Public property to get the projectId from route data
        public string? ProjectId => _httpContextAccessor?.HttpContext?.GetRouteValue("projectId")?.ToString();

        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        public async Task<Result<IPolarionClient>> CreateClientAsync()
        {
            string? routeProjectId = ProjectId; // Get project ID alias from route
            _logger.LogDebug("Attempting to create Polarion client for requested Project Alias: {RouteProjectId}", routeProjectId ?? "[Not Provided]");

            // Match the route alias exactly (case-insensitive) — no fallback to the default
            // project. Every MCP request carries a mandatory {projectId} route segment, so an
            // unmapped or misspelled alias must fail closed rather than silently serving the
            // default project's data (see RestApiProjectResolver.GetProjectConfig for the same
            // no-fallback contract on the REST side).
            if (string.IsNullOrEmpty(routeProjectId))
            {
                var errorMessage = "Configuration error: No project alias was provided in the request route.";
                _logger.LogError(errorMessage);
                return Result.Fail(errorMessage);
            }

            var selectedConfig = _projectConfigs.FirstOrDefault(p =>
                p.ProjectUrlAlias.Equals(routeProjectId, StringComparison.OrdinalIgnoreCase));

            if (selectedConfig == null)
            {
                var errorMessage = $"Configuration error: No Polarion project configuration found for requested alias '{routeProjectId}'. Check appsettings.json.";
                _logger.LogError(errorMessage);
                return Result.Fail(errorMessage);
            }

            _logger.LogDebug("Found matching configuration for Project Alias: {Alias}", selectedConfig.ProjectUrlAlias);

            if (selectedConfig.SessionConfig == null)
            {
                var errorMessage = "Internal error (539) the selected polarion client configuration variable is null.";
                _logger.LogError(errorMessage);
                return Result.Fail(errorMessage);
            }

            // Clone before applying a resolved credential — SessionConfig is a reference into the
            // singleton `List<PolarionProjectConfig>` (Program.cs), shared across every concurrent
            // request for this alias. Writing a per-caller credential into it in place would race
            // with every other in-flight request for the same project.
            var clientConfig = new PolarionClientConfiguration(
                selectedConfig.SessionConfig.ServerUrl,
                selectedConfig.SessionConfig.Username,
                selectedConfig.SessionConfig.Password,
                selectedConfig.SessionConfig.ProjectId,
                selectedConfig.SessionConfig.TimeoutSeconds);

            // Shared mode (default) resolves to the same service-account credential clientConfig
            // was just cloned from — a no-op for today's behavior. Only HttpBroker mode (off unless
            // Credentials:Mode is set explicitly) substitutes a per-caller credential here.
            var credentialResult = await _credentialResolver.ResolveAsync(
                selectedConfig, _httpContextAccessor?.HttpContext?.User);
            if (credentialResult.IsFailed)
            {
                var errorMessage = credentialResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                _logger.LogError("Failed to resolve upstream Polarion credential for alias '{Alias}': {ErrorMessage}",
                    selectedConfig.ProjectUrlAlias, errorMessage);
                return Result.Fail($"Failed to resolve upstream Polarion credential for alias '{selectedConfig.ProjectUrlAlias}': {errorMessage}");
            }

            var credential = credentialResult.Value;
            if (credential.Kind != PolarionCredentialKind.Password)
            {
                var errorMessage = $"Resolved credential kind '{credential.Kind}' is not yet supported by the upstream Polarion client (password only).";
                _logger.LogError(errorMessage);
                return Result.Fail(errorMessage);
            }

            clientConfig.Username = credential.Username;
            clientConfig.Password = credential.Secret;

            _logger.LogDebug("Creating Polarion client using Server: {ServerUrl}, User: {Username}, Project: {RealProjectId}",
                clientConfig.ServerUrl, clientConfig.Username, clientConfig.ProjectId);

            // Create the client using the selected configuration
            var clientResult = await PolarionClient.CreateAsync(clientConfig); 
            if (clientResult.IsFailed)
            {
                var errorMessage = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                _logger.LogError("Failed to create Polarion client via factory for server: {ServerUrl} (Alias: {Alias}). Error: {ErrorMessage}",
                    clientConfig.ServerUrl, selectedConfig.ProjectUrlAlias, errorMessage);
                return Result.Fail($"Failed to create Polarion client via factory for alias '{selectedConfig.ProjectUrlAlias}': {errorMessage}");
            }

            _logger.LogDebug("Successfully created new Polarion client for server: {ServerUrl} (Alias: {Alias})", 
                clientConfig.ServerUrl, selectedConfig.ProjectUrlAlias);
            return clientResult.Value;
        }
    }
}
