namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_outline"),
     Description("Gets all section headings (table of contents) within a Polarion Document. Returns a Markdown document of only headings.")]
    public async Task<string> GetDocumentOutline(
        [Description("The title of the Polarion document.")] string documentTitle,
        [Description("Document revision. Use '-1' for latest revision.")] string revision = "-1")
    {
        string? returnMsg;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "ERROR: Unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                // Get the current project configuration to check for blacklist pattern
                var projectConfig = GetCurrentProjectConfig();

                var workItemPrefix = projectConfig?.WorkItemPrefix;
                if (string.IsNullOrWhiteSpace(workItemPrefix))
                {
                    returnMsg = $"ERROR: No workItemPrefix was provided in the configuration";
                    return returnMsg;
                }

                var polarionFilter = PolarionFilter.Create("type:heading", true, false, [], false);
                var targetDocumentRevision = revision == "-1" ? null : revision;

                var results = await polarionClient.ExportModuleToMarkdownAsync(
                    workItemPrefix, documentTitle, polarionFilter, [], true, targetDocumentRevision);
                if (results.IsFailed)
                {
                    return $"ERROR: Failed to get headings for document. Error: {results.Errors.First()}";
                }

                return results.Value.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get headings for document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }
}
