using System.ComponentModel;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool
            (Name = "get_revisions_content_for_workitem"),
            Description(
                 "Gets the content of a work item at different revisions. Returns detailed information including title, status, description, and other standard fields for each revision. " +
                 "Supports configurable limit of revisions to return (default: 2, use -1 for all revisions). " +
                 "If the WorkItem is not found or encounters errors obtaining revisions it will return a descriptive error message."
     )]
    public async Task<string> GetRevisionsContentForWorkItem(
        [Description("The WorkItem ID (e.g., 'MD-12345')")] string workItemId,
        [Description("Maximum number of revisions to return (default: 2). Set to -1 to return all revisions.")] int limit = 2)
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

            try
            {
                var revisionsResult = await polarionClient.GetWorkItemRevisionsByIdAsync(workItemId, limit);

                if (revisionsResult.IsFailed)
                {
                    returnMsg = $"ERROR: (201) Failed to retrieve revisions for '{workItemId}'. Error: {revisionsResult.Errors.First()}";
                    return returnMsg;
                }

                var revisions = revisionsResult.Value;

                if (revisions == null || revisions.Length == 0)
                {
                    returnMsg = $"## Revision History for WorkItem '{workItemId}'\n\nNo revisions found.";
                    return returnMsg;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## Revision History for WorkItem '{workItemId}'");
                sb.AppendLine();

                string limitDescription = limit == -1 ? "all" : $"latest {limit}";
                sb.AppendLine($"Showing {limitDescription} revision{(revisions.Length != 1 ? "s" : "")} (newest to oldest)");
                sb.AppendLine();

                var markdownConverter = new ReverseMarkdown.Converter();

                for (int i = 0; i < revisions.Length; i++)
                {
                    var revision = revisions[i];
                    bool isLatest = (i == 0);

                    sb.AppendLine("---");
                    sb.AppendLine();

                    string revisionHeader = $"### Revision {i + 1}";
                    if (isLatest)
                    {
                        revisionHeader += " (Latest)";
                    }
                    sb.AppendLine(revisionHeader);
                    sb.AppendLine();

                    sb.AppendLine($"- **Updated**: {revision.updated:yyyy-MM-dd HH:mm:ss}");

                    if (revision.author != null)
                    {
                        var authorString = Utils.PolarionValueToString(revision.author, markdownConverter);
                        sb.AppendLine($"- **Author**: {authorString}");
                    }

                    if (!string.IsNullOrEmpty(revision.title))
                    {
                        sb.AppendLine($"- **Title**: {revision.title}");
                    }

                    if (revision.status != null)
                    {
                        var statusString = Utils.PolarionValueToString(revision.status, markdownConverter);
                        sb.AppendLine($"- **Status**: {statusString}");
                    }

                    if (revision.type != null)
                    {
                        var typeString = Utils.PolarionValueToString(revision.type, markdownConverter);
                        sb.AppendLine($"- **Type**: {typeString}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("**Description:**");

                    if (revision.description != null)
                    {
                        var descriptionMarkdown = Utils.PolarionValueToString(revision.description, markdownConverter);
                        sb.AppendLine(descriptionMarkdown);
                    }
                    else
                    {
                        sb.AppendLine("(No description)");
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to retrieve revision content for '{workItemId}' due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
