namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_outline"),
     Description("Gets all section headings (table of contents) within a Polarion Document. " +
                 "Returns a Markdown document of only headings, in document order.")]
    public async Task<string> GetDocumentOutline(
        [Description("The Polarion space name (e.g., 'MySpace').")]
        string space,

        [Description("The document ID within the space (e.g., 'MyDocument').")]
        string documentId,

        [Description("Document revision (number), '-1' for latest, or a baseline name or ID.")]
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

        // The SDK string-interpolates space and documentId directly into SQL; block injection chars.
        if (!IsSafeForPolarionPathParam(space))
        {
            return "ERROR: (103) space contains characters that are not permitted (single-quote, semicolon, or comment tokens).";
        }

        if (!IsSafeForPolarionPathParam(documentId))
        {
            return "ERROR: (104) documentId contains characters that are not permitted (single-quote, semicolon, or comment tokens).";
        }

        // A number-like revision must be "-1" (latest) or a positive integer. Baseline names and IDs are resolved after the client exists.
        if (!IsBaselineReference(revision) && !IsValidRevision(revision))
        {
            return "ERROR: (105) Revision must be '-1' for the latest revision or a positive integer revision ID.";
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
        {
            return clientResult.Errors.First().ToString() ?? "ERROR: Unknown error when creating Polarion client";
        }

        var polarionClient = clientResult.Value;

        try
        {
            var revisionResult = await ResolveRevisionAsync(polarionClient, revision, clientFactory.PolarionProjectId, space, documentId);
            if (revisionResult.IsFailed)
            {
                return revisionResult.Errors.First().Message;
            }

            var revisionNumber = revisionResult.Value;

            // Get all work items from the module, using revision-aware API when needed
            WorkItem[] allWorkItems;

            if (revisionNumber == "-1")
            {
                // Latest revision - use standard query
                var workItemsResult = await polarionClient.QueryWorkItemsInModuleAsync(
                    space,
                    documentId,
                    null); // Get all types

                if (workItemsResult.IsFailed)
                {
                    return $"ERROR: (1044) Failed to fetch work items from module '{space}/{documentId}'. Error: {workItemsResult.Errors.First().Message}";
                }

                allWorkItems = workItemsResult.Value;
            }
            else
            {
                // Specific revision - use baseline revision API
                var workItemsResult = await polarionClient.GetWorkItemsByModuleRevisionAsync(
                    space,
                    documentId,
                    revisionNumber);

                if (workItemsResult.IsFailed)
                {
                    var errorMessage = workItemsResult.Errors.First().Message;

                    if (errorMessage.Contains("UnresolvableObjectException", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1044) Document '{space}/{documentId}' not found at revision '{revision}'. " +
                               "Use get_document_revision_history to find valid revision numbers.";
                    }

                    return $"ERROR: (1044) Failed to fetch work items from module '{space}/{documentId}' at revision '{revision}'. Error: {errorMessage}";
                }

                allWorkItems = workItemsResult.Value
                    .Where(wi => wi?.WorkItem != null)
                    .Select(wi => wi.WorkItem)
                    .ToArray();
            }
            if (allWorkItems is null || allWorkItems.Length == 0)
            {
                return $"No work items found in module '{space}/{documentId}'.";
            }

            // Filter for headings only
            var headings = allWorkItems
                .Where(wi => wi?.type?.id != null &&
                             wi.type.id.Equals("heading", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (headings.Count == 0)
            {
                return $"No headings found in document '{space}/{documentId}'. Total work items in document: {allWorkItems.Length}.";
            }

            var result = new StringBuilder();
            var documentRevisionNumber = revisionNumber == "-1" ? "Latest" : revisionNumber;
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
