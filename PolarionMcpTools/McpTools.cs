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
        var returnMsg = "";
        
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
                    returnMsg = $"ERROR: Failed to fetch Polarion work item. {workItemResult.Errors.First()}";
                
                    return returnMsg;
                }

                var workItem = workItemResult.Value;
                var workItemMarkdownString = ConvertWorkItemToMarkdown(workItem);
                combinedWorkItems.Append($"## WorkItem (ID={targetWorkItemId})\n");
                combinedWorkItems.Append(workItemMarkdownString);
                combinedWorkItems.AppendLine("");
            }
            return combinedWorkItems.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching requirements: {ex.Message}");
        }

        returnMsg = $"ERROR: Failed to get Polarion WorkItems. For unknow reason.";
        return returnMsg;
    }

    [RequiresUnreferencedCode("Uses ReverseMarkdown API which requires reflection")]
    public static string ConvertWorkItemToMarkdown(WorkItem workItem)
    {
        string description = workItem.description.content?.ToString() ?? "";
        // var attributes = new Dictionary<string, string>();
    
        try
        {
            if (workItem.description.type == "text/html")
            {
                var converter = new ReverseMarkdown.Converter();
                var htmlContent = workItem.description.content?.ToString() ?? "";
                var markdownContent = converter.Convert(htmlContent);
                description = markdownContent;
            }

            // Process custom fields if available
            // if (workItem.customFields != null)
            // {
            //     foreach (var field in workItem.customFields)
            //     {
            //         requirement.Properties[field.key] = field.value?.ToString() ?? "";
            //         // if (field.value. != null && field.Value != null)
            //         // {
            //         //     requirement.Properties[field.Key] = field.Value.ToString();
            //         // }
            //     }
            // }

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
