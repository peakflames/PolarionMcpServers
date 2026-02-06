namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_section"),
     Description("Gets content for a specific section heading and its sub-headings in a Polarion Document. " +
                 "Returns all work items within the specified section number prefix.")]
    public async Task<string> GetDocumentSection(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space (e.g., 'FCC_L4_Requirements').")]
        string documentId,

        [Description("Section number (e.g., '1' or '3.4.5'). Returns the entire section including sub-sections like 3.4.5.1, 3.4.5.2, etc.")]
        string sectionNumber,

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

        if (string.IsNullOrWhiteSpace(sectionNumber))
        {
            return "ERROR: (102) Section number cannot be empty.";
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
                // Get all work items from the module, using revision-aware API when needed
                WorkItem[] allWorkItems;

                if (revision == "-1")
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
                        revision);

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

                // Normalize section number (remove leading/trailing dots)
                var normalizedSection = sectionNumber.Trim('.');

                // Filter work items by section number prefix
                // Match items where outlineNumber equals the section or starts with "section."
                var sectionWorkItems = allWorkItems
                    .Where(wi => wi?.outlineNumber != null && MatchesSection(wi.outlineNumber, normalizedSection))
                    .OrderBy(wi => wi.outlineNumber ?? "")
                    .ToList();

                if (sectionWorkItems.Count == 0)
                {
                    // List available top-level sections to help the user
                    var topSections = allWorkItems
                        .Where(wi => wi?.outlineNumber != null && !wi.outlineNumber.Contains('.'))
                        .Select(wi => wi.outlineNumber)
                        .Distinct()
                        .OrderBy(s => s)
                        .Take(10)
                        .ToList();

                    var availableSections = topSections.Count > 0
                        ? $" Available top-level sections: {string.Join(", ", topSections)}"
                        : "";

                    return $"No work items found in section '{sectionNumber}' of document '{space}/{documentId}'.{availableSections}";
                }

                var result = new StringBuilder();
                var documentRevisionNumber = revision == "-1" ? "Latest" : revision;
                result.AppendLine($"# Section {normalizedSection} Content");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
                result.AppendLine($"- **Document ID**: {documentId}");
                result.AppendLine($"- **Section**: {normalizedSection}");
                result.AppendLine($"- **Revision**: {documentRevisionNumber}");
                result.AppendLine($"- **Work Items in Section**: {sectionWorkItems.Count}");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();

                foreach (var workItem in sectionWorkItems)
                {
                    if (workItem?.id is null)
                    {
                        continue;
                    }

                    var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";
                    var typeId = workItem.type?.id ?? "N/A";
                    var outlineNumber = workItem.outlineNumber ?? "";

                    // Calculate heading level from outline number for proper markdown formatting
                    var headingLevel = string.IsNullOrEmpty(outlineNumber) ? 2 : outlineNumber.Count(c => c == '.') + 2;
                    headingLevel = Math.Min(Math.Max(headingLevel, 2), 6);

                    if (typeId.Equals("heading", StringComparison.OrdinalIgnoreCase))
                    {
                        // Render headings as markdown headings
                        var markdownHeading = new string('#', headingLevel);
                        var title = workItem.title ?? "(No Title)";
                        result.AppendLine($"{markdownHeading} {outlineNumber} {title}");
                        result.AppendLine();
                    }
                    else
                    {
                        // Render other work items with metadata
                        result.AppendLine($"### {outlineNumber} WorkItem (id={workItem.id}, type={typeId})");
                        result.AppendLine();
                        result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                        result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
                        result.AppendLine($"- **Last Updated**: {lastUpdated}");
                        result.AppendLine();

                        if (!string.IsNullOrWhiteSpace(workItem.description?.content))
                        {
                            var markdown = polarionClient.ConvertWorkItemToMarkdown(workItem.id, workItem);
                            result.AppendLine("**Description:**");
                            result.AppendLine();
                            result.AppendLine(markdown);
                            result.AppendLine();
                        }
                    }
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                var returnMsg = $"ERROR: Failed to get section content for document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }

    /// <summary>
    /// Checks if an outline number matches a section prefix.
    /// For example, section "6.1" matches "6.1", "6.1.1", "6.1.2.3" but not "6.10" or "6.2".
    /// </summary>
    private static bool MatchesSection(string outlineNumber, string sectionPrefix)
    {
        if (string.IsNullOrEmpty(outlineNumber))
        {
            return false;
        }

        // Exact match
        if (outlineNumber.Equals(sectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Prefix match - must be followed by a dot or dash to avoid matching "6.10" when looking for "6.1"
        // Dot separates sub-sections (e.g., 6.1.2), dash separates work items within a section (e.g., 6.1.2-1)
        return outlineNumber.StartsWith(sectionPrefix + ".", StringComparison.OrdinalIgnoreCase)
            || outlineNumber.StartsWith(sectionPrefix + "-", StringComparison.OrdinalIgnoreCase);
    }
}
