
namespace PolarionMcpTools
{
    public interface IPolarionClientFactory
    {
        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        Task<Result<IPolarionClient>> CreateClientAsync();
        string? ProjectId { get; }
    }

    public class PolarionClientFactory : IPolarionClientFactory
    {
        private readonly List<PolarionProjectConfig> _projectConfigs; // Changed from single configuration
        private readonly ILogger<PolarionClientFactory> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Constructor updated to inject the list of project configurations
        public PolarionClientFactory(
            List<PolarionProjectConfig> projectConfigs, // Changed parameter type
            ILogger<PolarionClientFactory> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _projectConfigs = projectConfigs; // Assign the injected list
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        // Public property to get the projectId from route data
        public string? ProjectId => _httpContextAccessor.HttpContext?.GetRouteValue("projectId")?.ToString();

        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        public async Task<Result<IPolarionClient>> CreateClientAsync()
        {
            string? routeProjectId = ProjectId; // Get project ID alias from route
            _logger.LogDebug("Attempting to create Polarion client for requested Project Alias: {RouteProjectId}", routeProjectId ?? "[Not Provided]");

            PolarionProjectConfig? selectedConfig = null;

            // Try to find a configuration matching the route alias (case-insensitive)
            if (!string.IsNullOrEmpty(routeProjectId))
            {
                selectedConfig = _projectConfigs.FirstOrDefault(p => 
                    p.ProjectUrlAlias.Equals(routeProjectId, StringComparison.OrdinalIgnoreCase));
                
                if (selectedConfig != null) 
                {
                     _logger.LogDebug("Found matching configuration for Project Alias: {Alias}", selectedConfig.ProjectUrlAlias);
                }
            }

            // If no specific match found, try to find the default configuration
            if (selectedConfig == null)
            {
                selectedConfig = _projectConfigs.FirstOrDefault(p => p.Default);
                if (selectedConfig != null)
                {
                    _logger.LogDebug("Using default configuration for Project Alias: {Alias}", selectedConfig.ProjectUrlAlias);
                }
                else
                {
                    // If still no config (neither specific nor default), throw an error
                    var errorMessage = $"Configuration error: No specific or default Polarion project configuration found for requested alias '{routeProjectId ?? "[Not Provided]"}'. Check appsettings.json.";
                    _logger.LogError(errorMessage);
                    return Result.Fail(errorMessage);
                }
            }

            // Use the SessionConfig from the selected project configuration
            var clientConfig = selectedConfig.SessionConfig;

            if (clientConfig == null)
            {
                var errorMessage = "Internal error (539) the selected polarion client configuration variable is null.";
                _logger.LogError(errorMessage);
                return Result.Fail(errorMessage);
            }

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
