namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "search_in_document"),
     Description("Searches a Polarion Document for work items matching search terms. " +
                 "Returns matching Requirements, Test Cases, and Test Procedures as Markdown. " +
                 "The search is performed across all indexed text fields (title, description, etc.).")]
    public async Task<string> SearchInDocument(
        [Description("The exact title of the Polarion document to search within.")]
        string documentTitle,

        [Description("Search terms using Lucene syntax. " +
                     "Examples: 'timeout' (single term), 'rigging timeout' (either term), " +
                     "'rigging AND timeout' (both terms required), '\"rigging timeout\"' (exact phrase). " +
                     "Do NOT prefix with field names like 'description:' - just provide the search terms.")]
        string searchQuery,

        [Description("Document revision number. Use '-1' for latest revision.")]
        string revision = "-1")
    {
        string? returnMsg;

        if (string.IsNullOrWhiteSpace(documentTitle))
        {
            returnMsg = $"ERROR: (100) No document title was provided.";
            return returnMsg;
        }

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            returnMsg = $"ERROR: (101) No search query was provided.";
            return returnMsg;
        }

        // Note: Do NOT escape quotes in searchQuery - they are meaningful Lucene syntax
        // for phrase searches (e.g., "rigging timeout" searches for that exact phrase)
        var escapedSearchQuery = searchQuery;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            var moduleTitle = documentTitle;
            // Pass search query directly - user provides Lucene syntax
            // Example: "rigging timeout" for phrase, rigging AND timeout for both terms
            var moduleFilter = $"document.title:\"{moduleTitle}\" AND ({escapedSearchQuery.Trim()})";
            var workItemFields = new List<string>()
            {
                "id",
                "outlineNumber",
                "type",
                "description",
                "status",
                "title",
                "updated"
            };

            var query = $"{moduleFilter}";

            // if revision is -1, call SearchWorkitem otherwise SearchWorkitemInBaseline
            //
            var targetRevision = revision == "-1" ? null : revision;
            var workItemResult = targetRevision is null
                                    ? await polarionClient.SearchWorkitemAsync(query, "outlineNumber", workItemFields)
                                    : await polarionClient.SearchWorkitemInBaselineAsync(targetRevision, query, "outlineNumber", workItemFields);

            if (workItemResult.IsFailed)
            {
                return $"ERROR: (1044) Failed to fetch Polarion work items. Error: {workItemResult.Errors.First()}. Note: The resulting transformed Lucene query was: '{query}'";
            }

            var workItems = workItemResult.Value;
            if (workItems is null || workItems.Length == 0)
            {
                return $"ERROR: (1045) No Polarion work items were found for Document '{moduleTitle}'.";
            }

            var combinedWorkItems = new StringBuilder();

            var documentRevisionNumber = targetRevision ?? "Latest";
            combinedWorkItems.AppendLine($"# Search Results for Polarion Work Items (Document=\"{documentTitle}\", searchQuery=\"{searchQuery}\", revision=\"{documentRevisionNumber}\")");
            combinedWorkItems.AppendLine("");
            combinedWorkItems.AppendLine($"Found {workItems.Length} Work Items.");
            combinedWorkItems.AppendLine("");

            foreach (var workItem in workItems)
            {
                if (workItem is null)
                {
                    continue;
                }

                if (workItem.id is null)
                {
                    continue;
                }

                var workItemMarkdownString = "";

                workItemMarkdownString = polarionClient.ConvertWorkItemToMarkdown(workItem.id, workItem, null, true);
                combinedWorkItems.AppendLine($"## Work Item: {workItem.id}");
                combinedWorkItems.Append(workItemMarkdownString);
                combinedWorkItems.AppendLine("");
            }

            return combinedWorkItems.ToString();
        }
    }
}
