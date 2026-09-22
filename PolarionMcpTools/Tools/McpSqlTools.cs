namespace PolarionMcpTools;

/// <summary>
/// Opt-in SQL query tool. Registered only when the server operator sets
/// <c>SqlQueryTool:Enabled=true</c> (see <see cref="SqlQueryToolRegistration"/>).
///
/// Polarion supports a <c>SQL:(...)</c> Lucene filter that runs SQL against its database via
/// the endpoint's configured Polarion credential. This tool exposes that for join-heavy read
/// patterns plain Lucene cannot express (parent-state joins, cross-document filters, substring
/// LIKE matches) while enforcing a read-only, single-SELECT shape through <see cref="SqlQueryGuard"/>.
///
/// Results are always limited to this endpoint's project: the tool never passes
/// <c>includeAllProjects</c>, so Polarion intersects the query with the route project's id, and
/// the same per-caller project access checks that cover every other tool apply here too.
/// </summary>
public sealed class McpSqlTools
{
    private readonly IServiceProvider _serviceProvider;

    public McpSqlTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "search_workitems_sql"),
     Description("Runs a read-only Polarion SQL query and returns matching work items as Markdown. " +
                 "For advanced join-heavy filters that plain Lucene cannot express: parent-state joins, " +
                 "cross-document filters, and substring (LIKE '%...%') matches. " +
                 "The SQL must be a single read-only SELECT that reads from the WORKITEM table and projects " +
                 "the work-item primary key (C_PK) - Polarion resolves the SQL: filter on C_PK. " +
                 "Example: \"SELECT item.C_PK FROM WORKITEM item INNER JOIN PROJECT proj ON proj.C_URI = item.FK_URI_PROJECT " +
                 "INNER JOIN CF_WORKITEM cf ON cf.FK_WORKITEM = item.C_PK WHERE proj.C_ID = 'Starlight_Main' " +
                 "AND item.C_TYPE = 'requirement' AND cf.C_NAME = 'customFieldA' AND cf.C_STRING_VALUE LIKE '%STR-1234%'\". " +
                 "Writes/DDL, statement stacking (';'), SQL comments, and parentheses inside string literals are rejected. " +
                 "Results are always limited to this endpoint's project: the server intersects every query with the " +
                 "project id. This tool runs under the same Polarion credential and the same project access checks as " +
                 "every other tool, and is only available when the server operator has enabled it.")]
    public async Task<string> SearchWorkitemsSql(
        [Description("A single read-only SQL SELECT statement over the Polarion WORKITEM table that projects C_PK. " +
                     "No writes, no ';', no comments, no parentheses inside string literals. See the tool description for a worked example.")]
        string sqlQuery,

        [Description("Optional additional Lucene filter to AND with the SQL result " +
                     "(e.g. 'linkedWorkItems:subsection_of=PROJ*' to exclude recycle-bin items). " +
                     "Must not itself contain a SQL:(...) filter or unbalanced grouping.")]
        string? luceneFilter = null,

        [Description("Sort order field. Default is 'created'. Other options: 'updated', 'id', 'title'.")]
        string? sortBy = "created",

        [Description("Maximum number of results to return. Default is 50, max is 500.")]
        int? maxResults = 50)
    {
        // Validate the SQL against the read-only guard BEFORE any transport contact.
        var validation = SqlQueryGuard.Validate(sqlQuery);
        if (!validation.IsValid)
        {
            return $"ERROR: (1050) Rejected SQL query. {validation.Error}";
        }

        // A supplementary Lucene filter must not smuggle in a second SQL: filter...
        if (!string.IsNullOrWhiteSpace(luceneFilter) && McpTools.ContainsSqlFilter(luceneFilter))
        {
            return "ERROR: (1051) The luceneFilter argument must not contain a SQL:(...) filter.";
        }

        // ...nor use unbalanced grouping that could re-associate the SQL:(...) filter and the
        // project.id scope Polarion applies.
        if (!string.IsNullOrWhiteSpace(luceneFilter) && !McpTools.HasBalancedLuceneGrouping(luceneFilter))
        {
            return "ERROR: (1055) The luceneFilter argument has unbalanced parentheses or quotes.";
        }

        if (maxResults < 1)
        {
            maxResults = 1;
        }

        if (maxResults > 500)
        {
            maxResults = 500;
        }

        var validSortFields = new[] { "created", "updated", "id", "title" };
        var sortField = (sortBy ?? "created").ToLowerInvariant();
        if (!validSortFields.Contains(sortField))
        {
            return $"ERROR: (1052) Invalid sortBy value '{sortBy}'. Must be one of: {string.Join(", ", validSortFields)}.";
        }

        var luceneQuery = BuildSqlLuceneQuery(sqlQuery, luceneFilter);

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
                var fieldList = McpTools.GetDefaultFieldList();

                // The 3-argument overload leaves includeAllProjects at its default (false), so
                // Polarion keeps the project.id filter and results stay within the route project.
                //
                // SDK limitation: SearchWorkitemAsync has no page/limit parameter, so the full
                // matching result set is materialized in memory before maxResults is applied below.
                // This is a pre-existing constraint that cannot be resolved without API changes.
                var searchResult = await polarionClient.SearchWorkitemAsync(
                    luceneQuery,
                    sortField,
                    fieldList);

                if (searchResult.IsFailed)
                {
                    var errorMsg = searchResult.Errors.FirstOrDefault()?.ToString() ?? "Unknown error";

                    if (errorMsg.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("syntax", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"ERROR: (1053) Invalid SQL/Lucene query syntax. Query: '{luceneQuery}'. Error: {errorMsg}.";
                    }

                    return $"ERROR: (1054) Failed to search work items. Error: {errorMsg}";
                }

                var workItems = searchResult.Value;
                if (workItems == null || workItems.Length == 0)
                {
                    return $"No work items matching the SQL query found in project. Lucene query used: {luceneQuery}";
                }

                return McpTools.FormatResults(workItems, sqlQuery, luceneQuery, itemTypes: null, statusFilter: null, sortField, maxResults ?? 50);
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed due to exception '{ex.Message}'";
            }
        }
    }

    /// <summary>
    /// Wraps a validated SQL statement as a single Polarion <c>SQL:(...)</c> Lucene filter inside
    /// one outer group, optionally AND-ed with a supplementary Lucene filter. The single outer
    /// group is never closed before the end of the expression, so the project.id filter Polarion
    /// appends cannot be re-associated. Callers MUST have run <see cref="SqlQueryGuard.Validate"/>
    /// on <paramref name="sqlQuery"/> and checked <paramref name="luceneFilter"/> grouping first.
    /// </summary>
    internal static string BuildSqlLuceneQuery(string sqlQuery, string? luceneFilter)
    {
        var sqlFilter = $"SQL:({sqlQuery.Trim()})";

        return string.IsNullOrWhiteSpace(luceneFilter)
            ? $"({sqlFilter})"
            : $"({sqlFilter} AND ({luceneFilter.Trim()}))";
    }
}
