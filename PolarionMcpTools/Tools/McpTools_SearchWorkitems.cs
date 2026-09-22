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
                     "Simple terms: 'HVBIT' (single term), 'HVBIT timeout' (either term - OR logic), " +
                     "'HVBIT AND timeout' (both terms required), '\"HVBIT timeout\"' (exact phrase). " +
                     "Raw Lucene is also accepted and passed through verbatim when it contains a " +
                     "field-scoped filter (e.g. 'category.KEY:MyCategory'), a boolean " +
                     "operator (AND/OR/NOT), or parenthesized grouping - e.g. " +
                     "'category.KEY:MyCategory AND (timeout)' or " +
                     "'type:requirement AND NOT HAS_VALUE:rationale'.")]
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

        // Containment: the server scopes every search to the route project by
        // AND-ing project.id onto the caller's Lucene. A SQL:(...) filter, an unbalanced
        // parenthesis/quote, or a non-identifier type/status value can each re-associate or
        // escape that project.id suffix and read another project's data. Reject all three
        // before the query is built or sent.
        if (ContainsSqlFilter(searchQuery))
        {
            return "ERROR: (105) SQL filters (SQL:(...)) are not permitted through search_workitems. " +
                   "Use search_workitems_sql, which validates SQL before sending it and is available " +
                   "only when the server operator has enabled it.";
        }

        if (!HasBalancedLuceneGrouping(searchQuery))
        {
            return "ERROR: (106) Unbalanced parentheses or quotes in search query.";
        }

        if (!AreCsvTokensSafeIdentifiers(itemTypes) || !AreCsvTokensSafeIdentifiers(statusFilter))
        {
            return "ERROR: (107) itemTypes and statusFilter may only contain identifier characters " +
                   "(letters, digits, '_', '.', '-').";
        }

        // Cap maxResults to valid range
        if (maxResults < 1) maxResults = 1;
        if (maxResults > 500) maxResults = 500;

        // Validate sortBy field
        var validSortFields = new[] { "created", "updated", "id", "title" };
        var sortField = (sortBy ?? "created").ToLowerInvariant();
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
                // Build Lucene query, then prepend an explicit project.id scope so results are
                // always constrained to this endpoint's project regardless of the SOAP session's
                // active project. Without this, a session whose active project differs from the
                // configured ProjectId returns items from the wrong scope (or exceeds the WCF
                // message-size limit when the session is globally scoped).
                var luceneQuery = BuildLuceneQuery(searchQuery, itemTypes, statusFilter);
                var projectConfig = GetCurrentProjectConfig();
                var projectId = projectConfig?.SessionConfig?.ProjectId;
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    luceneQuery = $"project.id:{projectId} AND ({luceneQuery})";
                }

                // Get field list
                var fieldList = GetDefaultFieldList();

                // Call Polarion API
                var searchResult = await polarionClient.SearchWorkitemAsync(
                    luceneQuery,
                    sortField,
                    fieldList);

                if (searchResult.IsFailed)
                {
                    var errorMsg = searchResult.Errors.FirstOrDefault()?.ToString() ?? "Unknown error";

                    if (errorMsg.Contains("MaxReceivedMessageSize", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("message size quota", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1047) Search returned too many results for the SOAP transport limit. " +
                               $"Narrow the query (add type:, status:, or date filters) or use search_workitems_sql " +
                               $"with a WHERE clause to reduce the result set. Query: '{luceneQuery}'";
                    }

                    if (errorMsg.Contains("maximum allowed limit", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("100,000", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1048) Query matches more than Polarion's 100,000 object limit. " +
                               $"Add more filters (type:, status:, document.id:, etc.) to narrow the result set. " +
                               $"Query: '{luceneQuery}'";
                    }

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
                return FormatResults(workItems, searchQuery, luceneQuery, itemTypes, statusFilter, sortField, maxResults ?? 50);
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
    internal static string BuildLuceneQuery(string searchQuery, string? itemTypes, string? statusFilter)
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
    /// Supports exact phrases, raw Lucene passthrough,
    /// AND logic, and OR logic (default).
    /// </summary>
    internal static string BuildTextSearchQuery(string searchQuery)
    {
        var trimmed = searchQuery.Trim();

        // Exact phrase: "voltage regulator"
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length > 2)
        {
            return trimmed;
        }

        // Raw Lucene passthrough: when the caller supplies real Lucene the
        // transport already accepts - a field-scoped filter, a boolean operator (AND/OR/NOT),
        // or parenthesized grouping - pass it through verbatim instead of re-tokenizing plain
        // words into (a OR b). This also covers the legacy "... AND ..." case.
        if (LooksLikeRawLucene(trimmed))
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

    // A "SQL:" filter token at any non-identifier boundary.
    // Polarion resolves SQL:(...) as a Lucene filter that executes raw SQL, so it must
    // never travel the plain-search path — it is owned exclusively by the opt-in
    // search_workitems_sql tool.
    //
    // Negative lookbehind (?<![A-Za-z0-9_]) catches ALL boundary forms — start-of-string,
    // whitespace, open-paren, AND operator-prefix chars like '-', '+', '!', ']', ':'.
    // The earlier pattern "(^|\s|\()" missed those adjacents, allowing "-SQL:(...)".
    private static readonly Regex SqlFilterRegex =
        new(@"(?<![A-Za-z0-9_])SQL\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // An identifier safe to interpolate into a type:/status: field filter: letters,
    // digits, and the '_', '.', '-' characters real Polarion enum ids use. Anything
    // else (spaces, parentheses, quotes, ':') could re-open Lucene grouping.
    private static readonly Regex SafeIdentifierRegex =
        new(@"^[A-Za-z0-9_.\-]+$", RegexOptions.Compiled);

    // Whole-word, case-sensitive Lucene boolean operators (Lucene operators are uppercase;
    // lowercase "and"/"or"/"not" are treated as search terms).
    private static readonly Regex BooleanOperatorRegex =
        new(@"(^|\s)(AND|OR|NOT)(\s|$)", RegexOptions.Compiled);

    // Field-scoped filter token: at a start/whitespace boundary, a word (optionally dotted,
    // e.g. category.KEY) immediately followed by ':' and a non-space, non-'/' value. The
    // leading boundary and non-'/' value class keep out URLs (http://...); the [A-Za-z_]
    // first char excludes numeric time/ratio tokens like 12:30 or 3:1.
    //
    // Intentionally broad: this is a passthrough signal for LooksLikeRawLucene, not an
    // allowlist. Narrowing to known Polarion field names would break valid custom fields
    // (category.KEY, linkedWorkItems.role, etc.) that vary per-project.
    private static readonly Regex FieldScopedRegex =
        new(@"(^|\s)[A-Za-z_][A-Za-z0-9_.]*:[^\s/]", RegexOptions.Compiled);

    /// <summary>
    /// Detects whether a search string embeds a Polarion <c>SQL:(...)</c> filter, which
    /// executes SQL against the endpoint's credential and must only run through the opt-in
    /// <c>search_workitems_sql</c> tool.
    /// </summary>
    internal static bool ContainsSqlFilter(string query)
        => !string.IsNullOrWhiteSpace(query) && SqlFilterRegex.IsMatch(query);

    /// <summary>
    /// Detects whether a search string is already raw Lucene that should be passed through to
    /// the transport verbatim rather than re-tokenized into an OR of terms. Signals: a boolean
    /// operator (AND/OR/NOT), parenthesized grouping, or a field-scoped filter (field:value).
    ///
    /// A <c>SQL:</c> filter is deliberately NOT a passthrough signal - it is blocked upstream
    /// by callers (see <see cref="ContainsSqlFilter"/>) so SQL can only ever run through the
    /// opt-in tool.
    /// </summary>
    internal static bool LooksLikeRawLucene(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        // Boolean operators: "a AND b", "a OR b", "NOT x".
        if (BooleanOperatorRegex.IsMatch(query))
        {
            return true;
        }

        // Parenthesized grouping / composition: "(timeout)".
        if (query.Contains('(') && query.Contains(')'))
        {
            return true;
        }

        // Field-scoped filter: "category.KEY:MyCategory". URLs are excluded (the
        // value class rejects '/'), so "http://x" does not trip passthrough.
        if (!query.Contains("//") && FieldScopedRegex.IsMatch(query))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when a caller-supplied Lucene fragment cannot re-associate the project.id
    /// scope the server appends: parenthesis depth never goes negative and ends at zero,
    /// and quotes are balanced. Parentheses inside a phrase (between <c>"</c>) are literal
    /// text and are ignored.
    ///
    /// Lucene backslash-escapes (<c>\"</c>) are explicitly handled: a <c>\</c> immediately
    /// before a <c>"</c> makes that quote a literal character, NOT a phrase delimiter. Without
    /// this, an attacker could send <c>\")\""</c> to make the checker treat the <c>)</c> as
    /// "inside a phrase" while Lucene sees it as a real (unmatched) close-paren that escapes
    /// the project-scope group the server appends.
    /// </summary>
    internal static bool HasBalancedLuceneGrouping(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        var depth = 0;
        var inPhrase = false;
        var i = 0;

        while (i < query.Length)
        {
            var c = query[i];

            // Lucene backslash-escaped quote: \" is a literal " char, NOT a phrase delimiter.
            // Consume both characters so the " does not flip inPhrase.
            if (c == '\\' && i + 1 < query.Length && query[i + 1] == '"')
            {
                i += 2;
                continue;
            }

            if (c == '"')
            {
                inPhrase = !inPhrase;
                i++;
                continue;
            }

            if (inPhrase)
            {
                i++;
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }

            i++;
        }

        return depth == 0 && !inPhrase;
    }

    /// <summary>
    /// True when <paramref name="value"/> contains only identifier characters and is safe
    /// to interpolate into a <c>type:</c>/<c>status:</c> Lucene filter.
    /// </summary>
    internal static bool IsSafeIdentifier(string? value)
        => !string.IsNullOrEmpty(value) && SafeIdentifierRegex.IsMatch(value);

    /// <summary>
    /// True when <paramref name="value"/> is safe to pass as a Polarion space name or document
    /// ID. The Polarion SDK string-interpolates these directly into SQL, so SQL injection
    /// characters are blocked. Spaces and dashes are allowed (real space names use them, e.g.
    /// "My Space - Section").
    /// </summary>
    internal static bool IsSafeForPolarionPathParam(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return !value.Contains('\'') &&
               !value.Contains(';') &&
               !value.Contains("--") &&
               !value.Contains("/*") &&
               !value.Contains("*/");
    }

    /// <summary>
    /// True when every comma-separated token in <paramref name="csv"/> is a safe identifier.
    /// An empty/absent value contributes no filter and is treated as safe.
    /// </summary>
    internal static bool AreCsvTokensSafeIdentifiers(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return true;
        }

        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IsSafeIdentifier(token))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the default list of fields to retrieve from Polarion.
    /// </summary>
    internal static List<string> GetDefaultFieldList()
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
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    internal static string FormatResults(
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
