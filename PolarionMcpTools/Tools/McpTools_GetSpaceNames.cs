using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider and CreateAsyncScope
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System; // Added for IServiceProvider
using System.Linq; // Added for LINQ operations

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_space_names"), Description("Gets the names of all Space in the Polarion Application Lifecycle Management (ALM) Project. Results are filtered by the BlacklistSpaceContainingMatch configuration if present.")]
    public async Task<string> GetSpaceNames()
    {
        string? returnMsg;
        
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                // Get the current project configuration to check for blacklist pattern
                var projectConfig = GetCurrentProjectConfig();
                string? blacklistPattern = projectConfig?.BlacklistSpaceContainingMatch;

                var spacesResult = await polarionClient.GetSpacesAsync(blacklistPattern);
                if (spacesResult.IsFailed)
                {
                    return $"ERROR: (0665) Failed to fetch Polarion spaces. Error: {spacesResult.Errors.First()}";
                }

                var spaces = spacesResult.Value;

                // return a comma-separated list of space names
                var combinedWorkItems = new StringBuilder();
                combinedWorkItems.AppendLine("# Polarion Space Names");
                combinedWorkItems.AppendLine($"- {string.Join("\n- ", spaces)}"); // markdown bullet list
                return combinedWorkItems.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion Document Space Names due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
