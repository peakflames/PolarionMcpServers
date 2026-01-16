namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_module"),
     Description(
         "Query work items from a Polarion module/document using SQL against the REL_MODULE_WORKITEM relationship. " +
         "This retrieves work items that belong to the specified document. " +
         "Optionally filter by work item types."
     )]
    public async Task<string> GetWorkItemsInModule(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("Optional comma-separated list of work item types to filter (e.g., 'requirement,testCase'). Leave empty for all types.")]
        string? itemTypes = null)
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

                var workItemsResult = await polarionClient.QueryWorkItemsInModuleAsync(
                    space,
                    documentId,
                    typeList);

                if (workItemsResult.IsFailed)
                {
                    return $"ERROR: (1044) Failed to fetch work items. Error: {workItemsResult.Errors.First().Message}";
                }

                var workItems = workItemsResult.Value;
                if (workItems is null || workItems.Length == 0)
                {
                    return $"No work items found in module '{space}/{documentId}'.";
                }

                var result = new StringBuilder();
                result.AppendLine($"# Work Items in Module");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
                result.AppendLine($"- **Document ID**: {documentId}");
                if (typeList != null && typeList.Count > 0)
                {
                    result.AppendLine($"- **Filtered Types**: {string.Join(", ", typeList)}");
                }
                result.AppendLine($"- **Total Work Items**: {workItems.Length}");
                result.AppendLine();

                foreach (var workItem in workItems)
                {
                    if (workItem is null)
                    {
                        continue;
                    }

                    var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

                    result.AppendLine($"## WorkItem (id={workItem.id ?? "N/A"}, type={workItem.type?.id ?? "N/A"}, lastUpdated={lastUpdated})");
                    result.AppendLine();
                    result.AppendLine($"- **Outline Number**: {workItem.outlineNumber ?? "N/A"}");
                    result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
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
