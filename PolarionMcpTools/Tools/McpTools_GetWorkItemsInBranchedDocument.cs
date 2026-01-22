namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_branched_document"),
     Description(
         "Retrieve work items from a branched Polarion document at a specific document baseline revision. " +
         "IMPORTANT: The revision must be a document baseline revision (from document history), NOT a work item revision. " +
         "Use get_document_info to find valid document revisions. " +
         "Returns work items with revision metadata indicating whether each item is historical or current."
     )]
    public async Task<string> GetWorkItemsInBranchedDocument(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("The document baseline revision number. Must be a valid document revision (not a work item revision). " +
                     "Use get_document_info or document history to find valid revision numbers.")]
        string revision)
    {
        if (string.IsNullOrWhiteSpace(space))
        {
            return "ERROR: (100) Space cannot be empty.";
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
                    space,
                    documentId,
                    revision);

                if (workItemsResult.IsFailed)
                {
                    var errorMessage = workItemsResult.Errors.First().Message;

                    // Check for UnresolvableObjectException which indicates invalid revision
                    if (errorMessage.Contains("UnresolvableObjectException", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1044) The document '{space}/{documentId}' could not be found at revision '{revision}'. " +
                               $"This typically means the revision number is invalid for this document. " +
                               $"Common causes:\n" +
                               $"  1. The revision number is a work item revision, not a document baseline revision\n" +
                               $"  2. The document did not exist at the specified revision\n" +
                               $"  3. The document has not been modified since before the specified revision\n\n" +
                               $"To find valid document revisions, use the document history in Polarion or check when the document was last modified.";
                    }

                    return $"ERROR: (1044) Failed to fetch work items. Error: {errorMessage}";
                }

                var workItems = workItemsResult.Value;
                if (workItems is null || workItems.Length == 0)
                {
                    return $"No work items found in document '{space}/{documentId}' at revision '{revision}'.";
                }

                var result = new StringBuilder();
                result.AppendLine($"# Work Items in Branched Document");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
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
