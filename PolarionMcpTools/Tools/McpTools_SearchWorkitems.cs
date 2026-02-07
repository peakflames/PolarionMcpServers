namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "search_workitems"),
     Description("Searches for work items across the entire Polarion project using text content. " +
                 "Searches across all indexed text fields (title, description, custom text fields) without requiring document IDs or work item IDs. " +
                 "Returns matching work items as Markdown.")]
    public async Task<string> SearchWorkitems(
        [Description("Search terms to find in work items. " +
                     "Examples: 'HVBIT' (single term), 'HVBIT timeout' (either term - OR logic), " +
                     "'HVBIT AND timeout' (both terms required), '\"HVBIT timeout\"' (exact phrase).")]
        string searchQuery,

        [Description("Optional comma-separated list of work item types to filter (e.g., 'requirement,testCase'). Leave empty for all types.")]
        string? itemTypes = null,

        [Description("Optional comma-separated list of status values to filter (e.g., 'open,in-progress'). Leave empty for all statuses.")]
        string? statusFilter = null,

        [Description("Sort order field. Default is 'created'. Other options: 'updated', 'id', 'title'.")]
        string? sortBy = "created",

        [Description("Maximum number of results to return. Default is 50, max is 500.")]
        int? maxResults = 50)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return "ERROR: (100) Search query cannot be empty.";
        }

        // Cap maxResults to valid range
        if (maxResults < 1) maxResults = 1;
        if (maxResults > 500) maxResults = 500;

        // Validate sortBy field
        var validSortFields = new[] { "created", "updated", "id", "title" };
        var sortField = (sortBy ?? "created").ToLower();
        if (!validSortFields.Contains(sortField))
        {
            return $"ERROR: (104) Invalid sortBy value '{sortBy}'. Must be one of: {string.Join(", ", validSortFields)}.";
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error: unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                // Build Lucene query
                var luceneQuery = BuildLuceneQuery(searchQuery, itemTypes, statusFilter);

                // Get field list
                var fieldList = GetDefaultFieldList();

                // Call Polarion API
                var searchResult = await polarionClient.SearchWorkitemAsync(
                    luceneQuery,
                    sortField,
                    fieldList);

                if (searchResult.IsFailed)
                {
                    var errorMsg = searchResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";

                    if (errorMsg.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("syntax", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1046) Invalid search query syntax. Query: '{luceneQuery}'. " +
                               $"Error: {errorMsg}. Try simplifying your search.";
                    }

                    return $"ERROR: (1045) Failed to search work items. Error: {errorMsg}";
                }

                var workItems = searchResult.Value;
                if (workItems == null || workItems.Length == 0)
                {
                    return $"No work items matching '{searchQuery}' found in project. " +
                           $"Lucene query used: {luceneQuery}";
                }

                // Format and return results
                return FormatResults(workItems, searchQuery, luceneQuery, itemTypes, statusFilter, sortField, maxResults.Value);
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed due to exception '{ex.Message}'";
            }
        }
    }

    /// <summary>
    /// Builds a Lucene query from user inputs.
    /// Combines text search with optional type and status filters.
    /// </summary>
    private static string BuildLuceneQuery(string searchQuery, string? itemTypes, string? statusFilter)
    {
        var queryParts = new List<string>();

        // Text search (searches ALL indexed fields in Polarion)
        var textQuery = BuildTextSearchQuery(searchQuery);
        if (!string.IsNullOrWhiteSpace(textQuery))
        {
            queryParts.Add($"({textQuery})");
        }

        // Type filter: (type:requirement OR type:testCase)
        if (!string.IsNullOrWhiteSpace(itemTypes))
        {
            var types = itemTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => $"type:{t}");

            var typeQuery = types.Count() == 1
                ? types.First()
                : $"({string.Join(" OR ", types)})";
            queryParts.Add(typeQuery);
        }

        // Status filter: (status:open OR status:in-progress)
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var statuses = statusFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => $"status:{s}");

            var statusQuery = statuses.Count() == 1
                ? statuses.First()
                : $"({string.Join(" OR ", statuses)})";
            queryParts.Add(statusQuery);
        }

        // Combine with AND
        return string.Join(" AND ", queryParts);
    }

    /// <summary>
    /// Builds the text search portion of the Lucene query.
    /// Supports exact phrases, AND logic, and OR logic (default).
    /// </summary>
    private static string BuildTextSearchQuery(string searchQuery)
    {
        var trimmed = searchQuery.Trim();

        // Exact phrase: "voltage regulator"
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length > 2)
        {
            return trimmed;
        }

        // AND logic: HVBIT AND timeout
        if (trimmed.Contains(" AND ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // OR logic (default): HVBIT timeout → (HVBIT OR timeout)
        var terms = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 1)
        {
            return terms[0];
        }

        return $"({string.Join(" OR ", terms)})";
    }

    /// <summary>
    /// Returns the default list of fields to retrieve from Polarion.
    /// </summary>
    private static List<string> GetDefaultFieldList()
    {
        return new List<string>
        {
            "id", "title", "type", "status", "description",
            "updated", "created", "outlineNumber", "author", "assignee"
        };
    }

    /// <summary>
    /// Formats search results as markdown.
    /// </summary>
    private static string FormatResults(
        WorkItem[] workItems,
        string searchQuery,
        string luceneQuery,
        string? itemTypes,
        string? statusFilter,
        string sortField,
        int maxResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Search Results for Work Items");
        sb.AppendLine();
        sb.AppendLine($"- **Search Query**: {searchQuery}");
        sb.AppendLine($"- **Lucene Query**: {luceneQuery}");
        sb.AppendLine($"- **Type Filter**: {itemTypes ?? "All"}");
        sb.AppendLine($"- **Status Filter**: {statusFilter ?? "All"}");
        sb.AppendLine($"- **Sort By**: {sortField}");
        sb.AppendLine($"- **Matching Work Items**: {Math.Min(workItems.Length, maxResults)}");
        sb.AppendLine($"- **Max Results**: {maxResults}");
        sb.AppendLine();

        var itemsToDisplay = workItems.Take(maxResults);

        foreach (var item in itemsToDisplay)
        {
            if (item is null)
            {
                continue;
            }

            var lastUpdated = item.updatedSpecified ? item.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

            sb.AppendLine($"## WorkItem (id={item.id ?? "N/A"}, type={item.type?.id ?? "N/A"}, lastUpdated={lastUpdated})");
            sb.AppendLine();
            sb.AppendLine($"- **Outline Number**: {item.outlineNumber ?? "N/A"}");
            sb.AppendLine($"- **Title**: {item.title ?? "N/A"}");
            sb.AppendLine($"- **Status**: {item.status?.id ?? "N/A"}");

            // Format author and assignee using Utils
            if (item.author != null)
            {
                var authorString = Utils.PolarionValueToString(item.author, null);
                sb.AppendLine($"- **Author**: {authorString}");
            }
            else
            {
                sb.AppendLine("- **Author**: N/A");
            }

            if (item.assignee != null && item.assignee.Length > 0)
            {
                var assigneeString = Utils.PolarionValueToString(item.assignee, null);
                sb.AppendLine($"- **Assignee**: {assigneeString}");
            }
            else
            {
                sb.AppendLine("- **Assignee**: Unassigned");
            }

            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(item.description?.content))
            {
                sb.AppendLine("### Description");
                sb.AppendLine();
                sb.AppendLine(item.description.content);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
