namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitem"),
     Description("Gets the text content of a WorkItem. Optionally retrieves a specific revision.")]
    public async Task<string> GetWorkitem(
        [Description("The WorkItem ID (e.g., 'WI-12345').")] string workitemId,
        [Description("Optional revision ID. Use '-1' or omit for latest revision.")] string? revision = null)
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
                // Determine if we need a specific revision or latest
                var useLatest = string.IsNullOrWhiteSpace(revision) || revision == "-1";

                if (useLatest)
                {
                    // Get latest version
                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(workitemId);
                    if (workItemResult.IsFailed)
                    {
                        return $"ERROR: Failed to retrieve WorkItem '{workitemId}': {workItemResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}";
                    }

                    var workItem = workItemResult.Value;
                    if (workItem == null)
                    {
                        return $"ERROR: WorkItem '{workitemId}' not found.";
                    }

                    var markdown = polarionClient.ConvertWorkItemToMarkdown(workitemId, workItem);

                    var sb = new StringBuilder();
                    sb.AppendLine($"## WorkItem (id='{workitemId}', type={workItem.type?.id ?? "N/A"}, revision=LATEST)");
                    sb.AppendLine();
                    sb.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    sb.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
                    if (workItem.updatedSpecified)
                    {
                        sb.AppendLine($"- **Last Updated**: {workItem.updated:yyyy-MM-dd HH:mm:ss}");
                    }
                    sb.AppendLine();
                    sb.AppendLine("### Content");
                    sb.AppendLine();
                    sb.AppendLine(markdown);

                    return sb.ToString();
                }
                else
                {
                    // Get specific revision
                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(workitemId, revision);
                    if (workItemResult.IsFailed)
                    {
                        return $"ERROR: Failed to retrieve WorkItem '{workitemId}' at revision '{revision}': {workItemResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}";
                    }

                    var workItem = workItemResult.Value;
                    if (workItem == null)
                    {
                        return $"ERROR: WorkItem '{workitemId}' not found at revision '{revision}'.";
                    }

                    var markdown = polarionClient.ConvertWorkItemToMarkdown(workitemId, workItem);

                    var sb = new StringBuilder();
                    sb.AppendLine($"## WorkItem (id='{workitemId}', type={workItem.type?.id ?? "N/A"}, revision={revision})");
                    sb.AppendLine();
                    sb.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    sb.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
                    if (workItem.updatedSpecified)
                    {
                        sb.AppendLine($"- **Last Updated**: {workItem.updated:yyyy-MM-dd HH:mm:ss}");
                    }
                    sb.AppendLine();
                    sb.AppendLine("### Content");
                    sb.AppendLine();
                    sb.AppendLine(markdown);

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed to retrieve WorkItem '{workitemId}' due to exception: {ex.Message}";
            }
        }
    }
}
