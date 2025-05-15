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
