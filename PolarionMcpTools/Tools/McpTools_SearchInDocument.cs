namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "search_in_document"),
     Description("Searches a Polarion Document for Requirements, Test Cases, and Test Procedures whose text contains keywords and phrases. Returns a Markdown document of matching WorkItems.")]
    public async Task<string> SearchInDocument(
        [Description("The title of the Polarion document.")] string documentTitle,
        [Description("Search query using Lucene boolean syntax (e.g., '(\"proximity\" OR \"Protective Earth\") AND \"Charge\"').")] string searchQuery,
        [Description("Document revision. Use '-1' for latest revision.")] string revision = "-1")
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

        // ensure that the searchQuery string has '\' escaped quotes
        //
        var escapedSearchQuery = searchQuery.Replace("\"", "\\\"");

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
            var descriptionQuery = $"description:({escapedSearchQuery.Trim()})";
            var moduleFilter = $"document.title:\"{moduleTitle}\" AND {descriptionQuery}";
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
