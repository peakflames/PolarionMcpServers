namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool
            (Name = "search_workitems_in_document"),
            Description(
                 "Search a Polarion Document for Requirements, Test Cases, and Test Procedures whose text contains key words and phrases. " +
                 "Tool returns a Markdown document of the matching WorkItems."
     )]
    public async Task<string> SearchWorkitemsInDocument(
        
            [Description("Name of Polarion document")]
            string documentName,

            [Description("Quoted Keyphrases using Lucene boolean syntax to search. e.g. (\"proximity\" OR \"Protective Earth\") AND \"Charge\")")]
            string textSearchTerms,

            [Description("Search only on the specified document revision. To use latest, set to -1")]
            string documentRevision
        )
    {
        string? returnMsg;
        
        
        if (string.IsNullOrWhiteSpace(documentName))
        {
            returnMsg = $"ERROR: (100) No document name was provided.";
            return returnMsg;
        }

        if (string.IsNullOrWhiteSpace(textSearchTerms))
        {
            returnMsg = $"ERROR: (101) No textSearchTerms were provided.";
            return returnMsg;
        }

        var searchTerms = textSearchTerms.Trim();

        // Check if the string is already wrapped in parentheses and remove them.
        if (searchTerms.StartsWith("(") && searchTerms.EndsWith(")"))
        {
            searchTerms = searchTerms[1..^1];
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

            var moduleTitle = documentName;
            var descriptionQuery = $"description:({searchTerms.Trim()})";
            var moduleFilter = $"document.title:\"{moduleTitle}\" AND {descriptionQuery}";
            var workItemFields = new List<string>()
            {
                "id",
                "outlineNumber",
                "type",
                "description",
                "status",
                "title",
            };

            var query = $"{moduleFilter}";

            // if documentRevision is -1, call SearchWorkitem otherwise SearchWorkitemInBaseline
            //
            var workItemResult = documentRevision == "-1" || string.IsNullOrEmpty(documentRevision)
                                    ? await polarionClient.SearchWorkitemAsync(query, "outlineNumber", workItemFields)
                                    : await polarionClient.SearchWorkitemInBaselineAsync(documentRevision, query, "outlineNumber", workItemFields);


            if (workItemResult.IsFailed)
            {
                return $"ERROR: (1044) Failed to fetch Polarion work items. Error: {workItemResult.Errors.First()}";
            }

            var workItems = workItemResult.Value;
            if (workItems is null || workItems.Length == 0)
            {
                return $"ERROR: (1045) No Polarion work items were found for Document '{moduleTitle}'.";
            }


            var combinedWorkItems = new StringBuilder();

            var documentRevisionNumber = documentRevision == "-1" ? "Latest" : documentRevision;
            combinedWorkItems.AppendLine($"# Search Results for Polarion Work Items (Document=\"{documentName}\", searchTerms=\"{searchTerms}\", documentRevision=\"{documentRevisionNumber}\")");
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
                combinedWorkItems.Append(workItemMarkdownString);
                combinedWorkItems.AppendLine("");
            }

            return combinedWorkItems.ToString();

        }
    }
}
