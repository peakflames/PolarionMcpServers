
using System.Diagnostics.CodeAnalysis;
using FluentResults;
using Microsoft.Extensions.Logging;
using Polarion;
using PolarionMcpTools;

namespace PolarionMcpServer
{
    public class PolarionStdioClientFactory : IPolarionClientFactory
    {
        private readonly List<PolarionProjectConfig> _projectConfigs; // Changed from single configuration
        private readonly ILogger<PolarionStdioClientFactory> _logger;
        private readonly string? _commandLineProjectAlias; // Project alias from command line arguments


        // Constructor updated to inject the list of project configurations and optional command line project alias
        public PolarionStdioClientFactory(
            List<PolarionProjectConfig> projectConfigs, // Changed parameter type
            ILogger<PolarionStdioClientFactory> logger,
            string? commandLineProjectAlias = null)
        {
            _projectConfigs = projectConfigs; // Assign the injected list
            _logger = logger;
            _commandLineProjectAlias = commandLineProjectAlias;
        }

        // Public property to get the projectId from route data
        public string? ProjectId => null;

        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        public async Task<Result<IPolarionClient>> CreateClientAsync()
        {
            // First priority: Command line project alias
            string? effectiveProjectAlias = _commandLineProjectAlias;
            string projectAliasSource = "command line";
                        
            _logger.LogDebug("Attempting to create Polarion client for requested Project Alias: {ProjectAlias} (from {Source})", 
                effectiveProjectAlias ?? "[Not Provided]", projectAliasSource);

            PolarionProjectConfig? selectedConfig = null;

            // Try to find a configuration matching the effective project alias (case-insensitive)
            if (!string.IsNullOrEmpty(effectiveProjectAlias))
            {
                selectedConfig = _projectConfigs.FirstOrDefault(p => 
                    p.ProjectUrlAlias.Equals(effectiveProjectAlias, StringComparison.OrdinalIgnoreCase));
                
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
                    var errorMessage = $"Configuration error: No specific or default Polarion project configuration found for requested alias '{effectiveProjectAlias ?? "[Not Provided]"}'. Check appsettings.json.";
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
