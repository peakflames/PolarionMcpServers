namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_module"),
     Description(
         "Query work items from a Polarion module/document using SQL against the REL_MODULE_WORKITEM relationship. " +
         "This retrieves work items that belong to the specified document. " +
         "Optionally filter by work item types. " +
         "Supports querying historical document revisions using the revision parameter."
     )]
    public async Task<string> GetWorkItemsInModule(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("Optional comma-separated list of work item types to filter (e.g., 'requirement,testCase'). Leave empty for all types. NOTE: Type filtering only works for current revision (revision='-1').")]
        string? itemTypes = null,

        [Description("Document revision. Use '-1' for latest revision. For historical revisions, use a document baseline revision number from document history.")]
        string revision = "-1")
    {
        if (string.IsNullOrWhiteSpace(space))
        {
            return "ERROR: (100) Space cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "ERROR: (101) Document ID cannot be empty.";
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
                // Parse item types if provided
                List<string>? typeList = null;
                if (!string.IsNullOrWhiteSpace(itemTypes))
                {
                    typeList = itemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                }

                var isHistoricalQuery = revision != "-1";
                WorkItem[] workItems;
                Dictionary<string, (string Revision, string HeadRevision, bool IsHistorical)>? revisionMetadata = null;

                if (isHistoricalQuery)
                {
                    // Historical revision - use baseline revision API
                    // Note: Type filtering is not supported for historical queries (documented in parameter description)

                    var workItemsResult = await polarionClient.GetWorkItemsByModuleRevisionAsync(
                        space,
                        documentId,
                        revision);

                    if (workItemsResult.IsFailed)
                    {
                        var errorMessage = workItemsResult.Errors.First().Message;

                        if (errorMessage.Contains("UnresolvableObjectException", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"ERROR: (1044) The document '{space}/{documentId}' could not be found at revision '{revision}'. " +
                                   $"This typically means the revision number is invalid for this document. " +
                                   $"Common causes:\n" +
                                   $"  1. The revision number is a work item revision, not a document baseline revision\n" +
                                   $"  2. The document did not exist at the specified revision\n" +
                                   $"  3. The document has not been modified since before the specified revision\n\n" +
                                   $"To find valid document revisions, use get_document_revision_history.";
                        }

                        return $"ERROR: (1044) Failed to fetch work items. Error: {errorMessage}";
                    }

                    var wiInfoArray = workItemsResult.Value;
                    workItems = wiInfoArray.Select(wi => wi.WorkItem).ToArray();

                    // Store revision metadata for output formatting
                    revisionMetadata = new Dictionary<string, (string, string, bool)>();
                    foreach (var wiInfo in wiInfoArray)
                    {
                        if (wiInfo?.WorkItem?.id != null)
                        {
                            revisionMetadata[wiInfo.WorkItem.id] = (wiInfo.Revision, wiInfo.HeadRevision, wiInfo.IsHistorical);
                        }
                    }
                }
                else
                {
                    // Current revision - use standard query with type filtering support
                    var workItemsResult = await polarionClient.QueryWorkItemsInModuleAsync(
                        space,
                        documentId,
                        typeList);

                    if (workItemsResult.IsFailed)
                    {
                        return $"ERROR: (1044) Failed to fetch work items. Error: {workItemsResult.Errors.First().Message}";
                    }

                    workItems = workItemsResult.Value;
                }

                if (workItems is null || workItems.Length == 0)
                {
                    return $"No work items found in module '{space}/{documentId}'.";
                }

                var result = new StringBuilder();
                result.AppendLine($"# Work Items in Module");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
                result.AppendLine($"- **Document ID**: {documentId}");

                if (isHistoricalQuery)
                {
                    result.AppendLine($"- **Revision**: {revision}");
                }

                if (typeList != null && typeList.Count > 0 && !isHistoricalQuery)
                {
                    result.AppendLine($"- **Filtered Types**: {string.Join(", ", typeList)}");
                }

                result.AppendLine($"- **Total Work Items**: {workItems.Length}");

                if (isHistoricalQuery && revisionMetadata != null)
                {
                    var historicalCount = revisionMetadata.Values.Count(m => m.IsHistorical);
                    var currentCount = workItems.Length - historicalCount;
                    result.AppendLine($"- **Historical Items** (different from HEAD): {historicalCount}");
                    result.AppendLine($"- **Current Items** (same as HEAD): {currentCount}");
                }

                result.AppendLine();

                foreach (var workItem in workItems)
                {
                    if (workItem is null)
                    {
                        continue;
                    }

                    var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

                    if (isHistoricalQuery && revisionMetadata != null && workItem.id != null && revisionMetadata.TryGetValue(workItem.id, out var metadata))
                    {
                        // Historical query - show revision status
                        var revisionStatus = metadata.IsHistorical ? "HISTORICAL" : "CURRENT";
                        result.AppendLine($"## WorkItem (id={workItem.id ?? "N/A"}, type={workItem.type?.id ?? "N/A"}, status={revisionStatus})");
                        result.AppendLine();
                        result.AppendLine($"- **Revision**: {metadata.Revision} (HEAD: {metadata.HeadRevision})");
                    }
                    else
                    {
                        // Current query - use original format
                        result.AppendLine($"## WorkItem (id={workItem.id ?? "N/A"}, type={workItem.type?.id ?? "N/A"}, lastUpdated={lastUpdated})");
                        result.AppendLine();
                    }

                    result.AppendLine($"- **Outline Number**: {workItem.outlineNumber ?? "N/A"}");
                    result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");

                    if (!isHistoricalQuery)
                    {
                        // Only show lastUpdated for current queries (already shown above for current queries)
                    }
                    else
                    {
                        result.AppendLine($"- **Last Updated**: {lastUpdated}");
                    }

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
