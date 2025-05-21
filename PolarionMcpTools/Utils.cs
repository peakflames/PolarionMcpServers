

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
            sb.AppendLine($"## WorkItem (id='{workItemId}')");
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
            sb.AppendLine($"## WorkItem (id='{workItemId}', type='{workItem.type.id}', lastUpdated='{workItem.updated}')");
        }
        sb.AppendLine("");
        sb.AppendLine(description);
        return sb.ToString();
    }


    public static string PolarionValueToString(object? value, ReverseMarkdown.Converter? markdownConverter)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is DateTime dtValue)
        {
            return dtValue.ToString("o"); // ISO 8601 for dates
        }
        else if (value is EnumOptionId enumId)
        {
            return enumId.id;
        }
        else if (value is EnumOptionId[] enumIdArray)
        {
            // For arrays, return a comma-separated list of enum IDs. Be sure to remove the last comma.
            var sb = new StringBuilder();
            foreach (var enumEntry in enumIdArray)
            {
                sb.Append($"{enumEntry.id}, ");
            }

            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 2, 2);
            }

            return sb.ToString();
        }
        else if (value is Text text)
        {
            if (markdownConverter is null)
            {
                return text.content.ToString();
            }
            else
            {
                try
                {
                    return $"\n\n{markdownConverter.Convert(text.content.ToString())}\n";
                }
                catch (Exception ex)
                {
                    return $"ERROR: Failed to convert Text value to markdown due to exception: {ex.Message}";
                }
            }
        }
        else if (value is User userValue)
        {
            // extract the useer id from the `uri` property which return a string in the format of: "subterra:data-service:objects:/default/${User}ybureau"
            var userId = userValue.uri.Split("${User}")[1];
            return userId;
        }
        else if (value is Project projectValue)
        {
            return projectValue.id;
        }

        return value.ToString() ?? "null";
    }
    
}
