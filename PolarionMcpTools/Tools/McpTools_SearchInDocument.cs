using System.Text.RegularExpressions;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "search_in_document"),
     Description("Searches a Polarion Document for work items matching search terms. " +
                 "Returns matching Requirements, Test Cases, and Test Procedures as Markdown. " +
                 "The search is performed across title and description fields.")]
    public async Task<string> SearchInDocument(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space (e.g., 'FCC_L4_Requirements').")]
        string documentId,

        [Description("Search terms. " +
                     "Examples: 'timeout' (single term), 'rigging timeout' (either term - OR logic), " +
                     "'rigging AND timeout' (both terms required), '\"rigging timeout\"' (exact phrase).")]
        string searchQuery,

        [Description("Document revision number. Use '-1' for latest revision.")]
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

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return "ERROR: (102) No search query was provided.";
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

                // Parse search query and filter work items in memory
                var searchMatcher = ParseSearchQuery(searchQuery);
                var matchingWorkItems = allWorkItems
                    .Where(wi => wi != null && MatchesSearch(wi, searchMatcher))
                    .ToList();

                if (matchingWorkItems.Count == 0)
                {
                    return $"No work items matching '{searchQuery}' found in document '{space}/{documentId}'. Total work items in document: {allWorkItems.Length}.";
                }

                var result = new StringBuilder();
                var documentRevisionNumber = revision == "-1" ? "Latest" : revision;
                result.AppendLine($"# Search Results for Polarion Work Items");
                result.AppendLine();
                result.AppendLine($"- **Space**: {space}");
                result.AppendLine($"- **Document ID**: {documentId}");
                result.AppendLine($"- **Search Query**: {searchQuery}");
                result.AppendLine($"- **Revision**: {documentRevisionNumber}");
                result.AppendLine($"- **Matching Work Items**: {matchingWorkItems.Count}");
                result.AppendLine($"- **Total Work Items in Document**: {allWorkItems.Length}");
                result.AppendLine();

                foreach (var workItem in matchingWorkItems)
                {
                    if (workItem?.id is null)
                    {
                        continue;
                    }

                    var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

                    result.AppendLine($"## WorkItem (id={workItem.id}, type={workItem.type?.id ?? "N/A"}, lastUpdated={lastUpdated})");
                    result.AppendLine();
                    result.AppendLine($"- **Outline Number**: {workItem.outlineNumber ?? "N/A"}");
                    result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                    result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");
                    result.AppendLine();

                    if (!string.IsNullOrWhiteSpace(workItem.description?.content))
                    {
                        var markdown = polarionClient.ConvertWorkItemToMarkdown(workItem.id, workItem);
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

    /// <summary>
    /// Parses a search query into a matcher function that supports:
    /// - Single terms: "timeout" matches if term is found
    /// - Multiple terms (OR): "rigging timeout" matches if ANY term is found
    /// - AND operator: "rigging AND timeout" matches if ALL terms are found
    /// - Exact phrases: "\"rigging timeout\"" matches the exact phrase
    /// </summary>
    private static SearchMatcher ParseSearchQuery(string query)
    {
        var trimmedQuery = query.Trim();

        // Check for exact phrase (wrapped in quotes)
        if (trimmedQuery.StartsWith('"') && trimmedQuery.EndsWith('"') && trimmedQuery.Length > 2)
        {
            var phrase = trimmedQuery[1..^1]; // Remove surrounding quotes
            return new SearchMatcher
            {
                MatchType = SearchMatchType.ExactPhrase,
                Terms = [phrase]
            };
        }

        // Check for AND operator
        if (trimmedQuery.Contains(" AND ", StringComparison.OrdinalIgnoreCase))
        {
            var terms = trimmedQuery
                .Split(new[] { " AND " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            return new SearchMatcher
            {
                MatchType = SearchMatchType.AllTerms,
                Terms = terms
            };
        }

        // Default: OR logic (any term matches)
        var orTerms = trimmedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return new SearchMatcher
        {
            MatchType = SearchMatchType.AnyTerm,
            Terms = orTerms
        };
    }

    /// <summary>
    /// Checks if a work item matches the search criteria.
    /// Searches in title and description fields.
    /// </summary>
    private static bool MatchesSearch(WorkItem workItem, SearchMatcher matcher)
    {
        // Combine searchable text from title and description
        var title = workItem.title ?? "";
        var description = workItem.description?.content ?? "";
        var searchableText = $"{title} {description}";

        return matcher.MatchType switch
        {
            SearchMatchType.ExactPhrase => searchableText.Contains(matcher.Terms[0], StringComparison.OrdinalIgnoreCase),
            SearchMatchType.AllTerms => matcher.Terms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase)),
            SearchMatchType.AnyTerm => matcher.Terms.Any(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private enum SearchMatchType
    {
        AnyTerm,      // OR logic: match if any term is found
        AllTerms,     // AND logic: match if all terms are found
        ExactPhrase   // Exact phrase match
    }

    private class SearchMatcher
    {
        public SearchMatchType MatchType { get; set; }
        public List<string> Terms { get; set; } = [];
    }
}
