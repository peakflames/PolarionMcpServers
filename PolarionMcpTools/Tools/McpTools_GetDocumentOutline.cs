namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_outline"),
     Description("Gets all section headings (table of contents) within a Polarion Document. " +
                 "Returns a Markdown document of only headings, ordered by outline number.")]
    public async Task<string> GetDocumentOutline(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space (e.g., 'FCC_L4_Requirements').")]
        string documentId,

        [Description("Document revision. Use '-1' for latest revision.")]
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
                return clientResult.Errors.First().ToString() ?? "ERROR: Unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                // Get all work items from the module using SQL relationship query
                var workItemsResult = await polarionClient.QueryWorkItemsInModuleAsync(
                    space,
                    documentId,
                    null); // Get all types

                if (workItemsResult.IsFailed)
                {
                    return $"ERROR: (1044) Failed to fetch work items from module '{space}/{documentId}'. Error: {workItemsResult.Errors.First().Message}";
                }

                var allWorkItems = workItemsResult.Value;
                if (allWorkItems is null || allWorkItems.Length == 0)
                {
                    return $"No work items found in module '{space}/{documentId}'.";
                }

                // Filter for headings only
                var headings = allWorkItems
                    .Where(wi => wi?.type?.id != null &&
                                 wi.type.id.Equals("heading", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(wi => wi.outlineNumber ?? "")
                    .ToList();

                if (headings.Count == 0)
                {
                    return $"No headings found in document '{space}/{documentId}'. Total work items in document: {allWorkItems.Length}.";
                }

                var result = new StringBuilder();
                var documentRevisionNumber = revision == "-1" ? "Latest" : revision;
                result.AppendLine($"# Document Outline: {documentId}");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
                result.AppendLine($"- **Document ID**: {documentId}");
                result.AppendLine($"- **Revision**: {documentRevisionNumber}");
                result.AppendLine($"- **Total Headings**: {headings.Count}");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();

                foreach (var heading in headings)
                {
                    if (heading?.id is null)
                    {
                        continue;
                    }

                    // Calculate heading level from outline number (e.g., "1" = level 1, "1.2" = level 2, "1.2.3" = level 3)
                    var outlineNumber = heading.outlineNumber ?? "";
                    var headingLevel = string.IsNullOrEmpty(outlineNumber) ? 1 : outlineNumber.Count(c => c == '.') + 1;

                    // Clamp heading level to valid markdown range (1-6)
                    headingLevel = Math.Min(Math.Max(headingLevel, 1), 6);

                    var markdownHeading = new string('#', headingLevel);
                    var title = heading.title ?? "(No Title)";

                    result.AppendLine($"{markdownHeading} {outlineNumber} {title}");
                    result.AppendLine();
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                var returnMsg = $"ERROR: Failed to get headings for document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }
}
