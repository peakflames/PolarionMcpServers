using System.ComponentModel;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool
            (Name = "get_text_for_workitem_at_revision"),
            Description(
                 "Gets the text for a single Requirement, Test Case, or Test Procedure at a specific revision by WorkItem Id (e.g., MD-12345) from " +
                 "within the Polarion Application Lifecycle Management (ALM) system. " +
                 "The tool automatically extracts the raw text and returns the raw content as a string. " +
                 "If the WorkItem is not found or encounters errors obtaining the WorkItem it will return a descriptive error message."
     )]
    public async Task<string> GetTextForWorkItemAtRevision(
        [Description("The WorkItem ID (e.g., 'MD-12345')")] string workItemId,
        [Description("The revision ID to retrieve")] string revision)
    {
        string? returnMsg;
        
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            IPolarionClientFactory? clientFactory;

            try
            {
                clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion Client Factory due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }

            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            var targetWorkItemId = workItemId.Trim();
            if (string.IsNullOrEmpty(targetWorkItemId))
            {
                returnMsg = $"ERROR: (100) No workitem ID was provided.";
                return returnMsg;
            }

            if (string.IsNullOrEmpty(revision))
            {
                returnMsg = $"ERROR: (103) No revision ID was provided.";
                return returnMsg;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Polarion Work Item at Revision");
                sb.AppendLine("");

                var workItemResult = await polarionClient.GetWorkItemByIdAsync(targetWorkItemId, revision);
                var workItemMarkdownString = "";
                if (workItemResult.IsFailed)
                {
                    workItemMarkdownString = polarionClient.ConvertWorkItemToMarkdown(
                        workItemId,
                        null,
                        $"ERROR: (101) Failed to fetch Polarion work item '{targetWorkItemId}' at revision '{revision}'. Error: {workItemResult.Errors.First()}");

                    sb.Append(workItemMarkdownString);
                    sb.AppendLine("");
                    return sb.ToString();
                }

                var workItem = workItemResult.Value;
                if (workItem is null || workItem.id is null)
                {
                    workItemMarkdownString = polarionClient.ConvertWorkItemToMarkdown(
                        workItemId,
                        null,
                        $"ERROR: (102) Failed to fetch Polarion work item '{targetWorkItemId}' at revision '{revision}'. It does not exist.");
                    
                    sb.Append(workItemMarkdownString);
                    sb.AppendLine("");
                    return sb.ToString();
                }

                workItemMarkdownString = polarionClient.ConvertWorkItemToMarkdown(workItemId, workItem, null, true);
                sb.Append(workItemMarkdownString);
                sb.AppendLine("");
                sb.AppendLine($"*Retrieved at revision: {revision}*");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion WorkItem '{targetWorkItemId}' at revision '{revision}' due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
