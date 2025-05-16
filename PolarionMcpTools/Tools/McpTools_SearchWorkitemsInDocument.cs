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
            string textSearchTerms
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
            var descriptionQuery = $"description:({textSearchTerms.Trim()})";
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
            var workItemResult = await polarionClient.SearchWorkitemAsync(query, "outlineNumber", workItemFields);

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
            combinedWorkItems.AppendLine($"# Search Results for Polarion Work Items (Document=\"{documentName}\", searchTerms=\"{textSearchTerms}\")");
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

                workItemMarkdownString = Utils.ConvertWorkItemToMarkdown(workItem.id, workItem, null, true);
                combinedWorkItems.Append(workItemMarkdownString);
                combinedWorkItems.AppendLine("");
            }

            return combinedWorkItems.ToString();

        }
    }
}
