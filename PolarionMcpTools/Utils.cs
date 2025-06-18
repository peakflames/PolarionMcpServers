

namespace PolarionMcpTools;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]

public sealed class Utils
{
    
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
        else if (value is User[] userValueArray)
        {
            // For arrays, return a comma-separated list of enum IDs. Be sure to remove the last comma.
            var sb = new StringBuilder();
            foreach (var entry in userValueArray)
            {
                // extract the useer id from the `uri` property which return a string in the format of: "subterra:data-service:objects:/default/${User}ybureau"
                var userId = entry.uri.Split("${User}")[1];
                sb.Append($"{userId}, ");
            }

            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 2, 2);
            }

            return sb.ToString();
        }
        else if (value is Project projectValue)
        {
            return projectValue.id;
        }

        return value.ToString() ?? "null";
    }
    
}
