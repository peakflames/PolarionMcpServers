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

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]

public sealed class Utils
{
    [RequiresUnreferencedCode("Uses ReverseMarkdown API which requires reflection")]
    public static string ConvertWorkItemToMarkdown(string workItemId, WorkItem? workItem, string? errorMsg = null, bool includeMetadata = false)
    {
        var sb = new StringBuilder();

        if (!includeMetadata)
        {
            sb.AppendLine($"## WorkItem (ID='{workItemId}')");
        }

        if (workItem is null)
            {
                sb.AppendLine(errorMsg ?? $"ERROR: WorkItem with ID '{workItemId}' does not exist.");
                return sb.ToString(); ;
            }

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

        if (includeMetadata)
        {
            sb.AppendLine($"## WorkItem (ID='{workItemId}', type='{workItem.type.id}', status='{workItem.status.id}')");
        }
        sb.AppendLine("");
        sb.AppendLine(description);
        return sb.ToString();
    }
}
