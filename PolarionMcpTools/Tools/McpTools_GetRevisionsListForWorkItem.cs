namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_revisions_list_for_workitem"),
     Description("Gets the list of revision IDs for a specific work item, ordered from newest to oldest.")]
    public async Task<string> GetRevisionsListForWorkItem(
        [Description("The WorkItem ID (e.g., 'MD-12345')")] string workItemId)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(workItemId))
        {
            return "ERROR: workItemId parameter cannot be empty.";
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();

            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.FirstOrDefault()?.Message ??
                       "ERROR: Unknown error when creating Polarion client.";
            }
            var polarionClient = clientResult.Value;

            try
            {
                var revisionsResult = await polarionClient.GetRevisionsIdsByWorkItemIdAsync(workItemId);

                if (revisionsResult.IsFailed)
                {
                    return $"ERROR: Failed to retrieve revisions for '{workItemId}': {revisionsResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}";
                }

                var revisionIds = revisionsResult.Value;

                if (revisionIds == null || revisionIds.Length == 0)
                {
                    return $"## Revisions for WorkItem '{workItemId}'\n\nNo revisions found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## Revisions for WorkItem '{workItemId}'");
                sb.AppendLine();
                sb.AppendLine($"Total revisions: {revisionIds.Length} (ordered newest to oldest)");
                sb.AppendLine();

                for (int i = 0; i < revisionIds.Length; i++)
                {
                    sb.AppendLine($"{i + 1}. {revisionIds[i]}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed to retrieve revisions for '{workItemId}' due to exception: {ex.Message}";
            }
        }
    }
}
