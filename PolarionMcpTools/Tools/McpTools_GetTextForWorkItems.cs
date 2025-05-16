namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool
            (Name = "get_text_for_workitems_by_id"),
            Description(
                 "Gets the latest text for Requirements, Test Cases, and Test Procedures by WorkItem Id (e.g., MD-12345) from" +
                 "within the Polarion Application Lifecycle Management (ALM) system. " +
                 "The tool automatically extracts the raw text and returns the raw content as a string.  " +
                 "If the WorkItem is not found or encounters errors obtaining the WorkItem it will return a descriptive error message."
     )]
    public async Task<string> GetTextForWorkItemsById(
        [Description("A comma-separated list of WorkItem Ids")] string workItemIds)
    {
        string? returnMsg;
        
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            var workItemIdList = workItemIds.Split(',');
            if (workItemIdList.Length == 0)
            {
                returnMsg = $"ERROR: (100) No woritems were provided.";
                return returnMsg;
            }
            

            try
            {
                var combinedWorkItems = new StringBuilder();
                combinedWorkItems.AppendLine("# Polarion Work Items");
                combinedWorkItems.AppendLine("");

                foreach (var workItemId in workItemIdList)
                {
                    var targetWorkItemId = workItemId.Trim();
                    if (string.IsNullOrEmpty(targetWorkItemId))
                    {
                        continue;
                    }

                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(targetWorkItemId);
                    var workItemMarkdownString = "";
                    if (workItemResult.IsFailed)
                    {
                        workItemMarkdownString = Utils.ConvertWorkItemToMarkdown(
                            workItemId,
                            null,
                            $"ERROR: (101) Failed to fetch Polarion work item '{targetWorkItemId}'. Error: {workItemResult.Errors.First()}");

                        combinedWorkItems.Append(workItemMarkdownString);
                        combinedWorkItems.AppendLine("");
                        continue;
                    }

                    var workItem = workItemResult.Value;
                    if (workItem is null || workItem.id is null)
                    {
                        workItemMarkdownString = Utils.ConvertWorkItemToMarkdown(
                            workItemId,
                            null,
                            $"ERROR: (102) Failed to fetch Polarion work item '{targetWorkItemId}'. It does not exist.");
                        
                        combinedWorkItems.Append(workItemMarkdownString);
                        combinedWorkItems.AppendLine("");
                        continue;
                    }

                    workItemMarkdownString = Utils.ConvertWorkItemToMarkdown(workItemId, workItem, null);
                    combinedWorkItems.Append(workItemMarkdownString);
                    combinedWorkItems.AppendLine("");
                }
                return combinedWorkItems.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion WorkItems due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
