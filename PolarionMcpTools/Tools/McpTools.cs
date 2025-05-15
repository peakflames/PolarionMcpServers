using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider and CreateAsyncScope
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System; // Added for IServiceProvider
using System.Collections.Generic; // Added for List<>
using System.Linq; // Added for LINQ operations

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    private readonly IServiceProvider _serviceProvider;

    public McpTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the current project configuration based on the project ID from the client factory.
    /// </summary>
    /// <returns>The current project configuration, or null if not found.</returns>
    private PolarionProjectConfig? GetCurrentProjectConfig()
    {
        // Get the current project ID from the client factory
        var clientFactory = _serviceProvider.GetRequiredService<IPolarionClientFactory>();
        string? projectId = clientFactory.ProjectId;
        
        // Get all project configurations
        var projectConfigs = _serviceProvider.GetRequiredService<List<PolarionProjectConfig>>();
        
        // Find the matching configuration
        return projectConfigs.FirstOrDefault(p => 
            p.ProjectUrlAlias.Equals(projectId, StringComparison.OrdinalIgnoreCase)) 
            ?? projectConfigs.FirstOrDefault(p => p.Default);
    }

}
