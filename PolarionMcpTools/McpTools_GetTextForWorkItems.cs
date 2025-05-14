using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider and CreateAsyncScope
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System; // Added for IServiceProvider

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool
            (Name = "get_text_for_workitems"),
            Description(
                 "Gets the latest text for Requirements, Test Cases, and Test Procedures by WorkItem Id (e.g., MD-12345) from" +
                 "within the Polarion Application Lifecycle Management (ALM) system. " +
                 "The tool automatically extracts the raw text and returns the raw content as a string.  " +
                 "If the WorkItem is not found or encounters errors obtaining the WorkItem it will return a descriptive error message."
     )]
    public async Task<string> GetTextForWorkItems(
        [Description("A comma-separated list of WorkItem Ids")] string workItemIds)
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

            var workItemIdList = workItemIds.Split(',');
            if (workItemIdList.Length == 0)
            {
                returnMsg = $"ERROR: (100) No woritems were provided.";
                return returnMsg;
            }

            try
            {
                var combinedWorkItems = new StringBuilder();
                combinedWorkItems.AppendLine("# Polarion Work Items");
                combinedWorkItems.AppendLine("");
                
                foreach (var workItemId in workItemIdList)
                {
                    var targetWorkItemId = workItemId.Trim();
                    if (string.IsNullOrEmpty(targetWorkItemId))
                    {
                        continue;
                    }

                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(targetWorkItemId);
                    if (workItemResult.IsFailed)
                    {
                        return $"ERROR: (101) Failed to fetch Polarion work item '{targetWorkItemId}'. Error: {workItemResult.Errors.First()}";
                    }

                    var workItem = workItemResult.Value;
                    if (workItem is null || workItem.id is null)
                    {
                        return $"ERROR: (102) Failed to fetch Polarion work item '{targetWorkItemId}'. It does not exist.";
                    }

                    var workItemMarkdownString = Utils.ConvertWorkItemToMarkdown(workItem);
                    combinedWorkItems.Append(workItemMarkdownString);
                    combinedWorkItems.AppendLine("");
                }
                return combinedWorkItems.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion WorkItems due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
