namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_branched_document"),
     Description(
         "Retrieve work items from a branched Polarion document at a specific revision. " +
         "This tool uses a revision-aware algorithm that correctly fetches historical versions of work items " +
         "when they differ from the HEAD revision. Returns work items with revision metadata indicating " +
         "whether each item is historical or current."
     )]
    public async Task<string> GetWorkItemsInBranchedDocument(
        [Description("The module folder path (e.g., 'FCC_L4_Air8_1')")]
        string moduleFolder,

        [Description("The document ID within the module folder")]
        string documentId,

        [Description("The revision number to query the document at")]
        string revision)
    {
        if (string.IsNullOrWhiteSpace(moduleFolder))
        {
            return "ERROR: (100) Module folder cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "ERROR: (101) Document ID cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(revision))
        {
            return "ERROR: (102) Revision cannot be empty.";
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                var workItemsResult = await polarionClient.GetWorkItemsByModuleRevisionAsync(
                    moduleFolder,
                    documentId,
                    revision);

                if (workItemsResult.IsFailed)
                {
                    return $"ERROR: (1044) Failed to fetch work items. Error: {workItemsResult.Errors.First().Message}";
                }

                var workItems = workItemsResult.Value;
                if (workItems is null || workItems.Length == 0)
                {
                    return $"No work items found in document '{moduleFolder}/{documentId}' at revision '{revision}'.";
                }

                var result = new StringBuilder();
                result.AppendLine($"# Work Items in Branched Document");
                result.AppendLine();
                result.AppendLine($"- **Module Folder**: {moduleFolder}");
                result.AppendLine($"- **Document ID**: {documentId}");
                result.AppendLine($"- **Revision**: {revision}");
                result.AppendLine($"- **Total Work Items**: {workItems.Length}");
                result.AppendLine();

                var historicalCount = workItems.Count(wi => wi.IsHistorical);
                var currentCount = workItems.Length - historicalCount;
                result.AppendLine($"- **Historical Items** (different from HEAD): {historicalCount}");
                result.AppendLine($"- **Current Items** (same as HEAD): {currentCount}");
                result.AppendLine();

                foreach (var wiInfo in workItems)
                {
                    if (wiInfo?.WorkItem is null)
                    {
                        continue;
                    }

                    var workItem = wiInfo.WorkItem;
                    var revisionStatus = wiInfo.IsHistorical ? "HISTORICAL" : "CURRENT";
                    var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

                    result.AppendLine($"## WorkItem (id={workItem.id ?? "N/A"}, type={workItem.type?.id ?? "N/A"}, status={revisionStatus})");
                    result.AppendLine();
                    result.AppendLine($"- **Revision**: {wiInfo.Revision} (HEAD: {wiInfo.HeadRevision})");
                    result.AppendLine($"- **Outline Number**: {workItem.outlineNumber ?? "N/A"}");
                    result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
                    result.AppendLine($"- **Last Updated**: {lastUpdated}");
                    result.AppendLine();

                    if (!string.IsNullOrWhiteSpace(workItem.description?.content))
                    {
                        var markdown = polarionClient.ConvertWorkItemToMarkdown(workItem.id ?? "unknown", workItem);
                        result.AppendLine("### Description");
                        result.AppendLine();
                        result.AppendLine(markdown);
                        result.AppendLine();
                    }
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed due to exception '{ex.Message}'";
            }
        }
    }
}
