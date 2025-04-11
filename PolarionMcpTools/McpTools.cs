using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PolarionMcpTools;


[McpServerToolType]
public sealed class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool, Description(
                 "Request to read the latest content of one or more WorkItems by Id from a single Polarion project " +
                 "within the Polarion Application Lifecycle Management (ALM) system. " +
                 "The tool automatically extracts the raw text and returns the raw content as a string.  " +
                 "If the WorkItem is not found or encounters errors obtaining the WorkItem it will return a descriptive error message."
     )]
    public static async Task<string> ReadWorkItems(
        IPolarionClient polarionClient,
        [Description("A comma-seperated list of WorkItem Ids")] string workItemIds)
    {
        string? returnMsg;
        
        var workItemIdList = workItemIds.Split(',');
        if (workItemIdList.Length == 0)
        {
            returnMsg = $"ERROR: Failed to Polarion file. The workitem '{workItemIds}' does not exist.";
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
                    return $"ERROR: Failed to fetch Polarion work item '{targetWorkItemId}'.";
                }

                var workItem = workItemResult.Value;
                if (workItem is null || workItem.id is null)
                {
                    return $"ERROR: Failed to fetch Polarion work item '{targetWorkItemId}'. It does not exist.";
                }

                var workItemMarkdownString = ConvertWorkItemToMarkdown(workItem);
                combinedWorkItems.Append(workItemMarkdownString);
                combinedWorkItems.AppendLine("");
            }
            return combinedWorkItems.ToString();
        }
        catch (Exception ex)
        {
            returnMsg = $"ERROR: Failed to get Polarion WorkItems due to exception '{ex.Message}'\n{ex.StackTrace}";
            return returnMsg;
        }
    }

    [RequiresUnreferencedCode("Uses ReverseMarkdown API which requires reflection")]
    public static string ConvertWorkItemToMarkdown(WorkItem workItem)
    {
        string description = workItem.description?.content?.ToString() ?? "Work Item description was null. Likely does not exist";
    
        try
        {
            if (workItem.description?.type == "text/html")
            {
                var converter = new ReverseMarkdown.Converter();
                var htmlContent = workItem.description.content?.ToString() ?? "";
                var markdownContent = converter.Convert(htmlContent);
                description = markdownContent;
            }
        }
        catch (Exception ex)
        {
            return $"Error extracting data from WorkItem: {ex.Message}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## WorkItem (ID={workItem.id})");
        sb.AppendLine(description);
        return sb.ToString();
    }
}
