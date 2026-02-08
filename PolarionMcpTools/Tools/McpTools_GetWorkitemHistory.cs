namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitem_history"),
     Description("Gets the revision history for a WorkItem including content at each revision. Returns detailed information including title, status, and description for each revision.")]
    public async Task<string> GetWorkitemHistory(
        [Description("The WorkItem ID (e.g., 'WI-12345').")] string workitemId,
        [Description("Maximum number of revisions to return. Use -1 for all revisions.")] int limit = 5)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(workitemId))
        {
            return "ERROR: workitemId parameter cannot be empty.";
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.FirstOrDefault()?.Message ?? "ERROR: Unknown error when creating Polarion client.";
            }

            var polarionClient = clientResult.Value;

            try
            {
                var revisionsResult = await polarionClient.GetWorkItemRevisionsByIdAsync(workitemId, limit);

                if (revisionsResult.IsFailed)
                {
                    return $"ERROR: Failed to retrieve revisions for '{workitemId}': {revisionsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}";
                }

                var revisionsDict = revisionsResult.Value;

                if (revisionsDict == null || revisionsDict.Count == 0)
                {
                    return $"## Revision History for WorkItem '{workitemId}'\n\nNo revisions found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## Revision History for WorkItem '{workitemId}'");
                sb.AppendLine();

                var limitDescription = limit == -1 ? "all" : $"latest {limit}";
                sb.AppendLine($"Showing {limitDescription} revision{(revisionsDict.Count != 1 ? "s" : "")} (newest to oldest)");
                sb.AppendLine();

                var markdownConverter = new ReverseMarkdown.Converter();

                var i = 0;
                foreach (var kvp in revisionsDict)
                {
                    var revisionId = kvp.Key;
                    var revision = kvp.Value;
                    var isLatest = (i == 0);

                    sb.AppendLine("---");
                    sb.AppendLine();

                    var revisionHeader = $"### Revision {i + 1} (ID: {revisionId})";
                    if (isLatest)
                    {
                        revisionHeader += " (Latest)";
                    }
                    sb.AppendLine(revisionHeader);
                    sb.AppendLine();

                    sb.AppendLine($"- **Updated**: {revision.updated:yyyy-MM-dd HH:mm:ss}");

                    if (revision.author != null)
                    {
                        var authorString = Utils.PolarionValueToString(revision.author, markdownConverter);
                        sb.AppendLine($"- **Author**: {authorString}");
                    }

                    if (!string.IsNullOrEmpty(revision.title))
                    {
                        sb.AppendLine($"- **Title**: {revision.title}");
                    }

                    if (revision.status != null)
                    {
                        var statusString = Utils.PolarionValueToString(revision.status, markdownConverter);
                        sb.AppendLine($"- **Status**: {statusString}");
                    }

                    if (revision.type != null)
                    {
                        var typeString = Utils.PolarionValueToString(revision.type, markdownConverter);
                        sb.AppendLine($"- **Type**: {typeString}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("**Description:**");

                    if (revision.description != null)
                    {
                        var descriptionMarkdown = Utils.PolarionValueToString(revision.description, markdownConverter);
                        sb.AppendLine(descriptionMarkdown);
                    }
                    else
                    {
                        sb.AppendLine("(No description)");
                    }

                    sb.AppendLine();
                    i++;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                var errorMsg = $"ERROR: Failed to retrieve revision history for '{workitemId}' due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    errorMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return errorMsg;
            }
        }
    }
}
