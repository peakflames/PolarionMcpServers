using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PolarionMcpTools;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
[McpServerToolType]
public sealed class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool, Description(
                 "Gets the latest text for Requirements, Test Cases, and Test Procedures by WorkItem Id (e.g., MD-12345) from" +
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

                var workItemMarkdownString = ConvertWorkItemToMarkdown(workItem);
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
