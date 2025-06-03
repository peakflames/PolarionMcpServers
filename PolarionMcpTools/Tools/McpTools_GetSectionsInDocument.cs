namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_sections_in_document"), Description("Get all section headings within a Polarion Document. Results in a Markdwon document of only headings.")]
    public async Task<string> GetSectionsInDocument(
        [Description("Name of Polarion document")]
        string documentName,

        [Description("To use latest, set to -1")]
        string documentRevision
    )
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

            try
            {
                // Get the current project configuration to check for blacklist pattern
                var projectConfig = GetCurrentProjectConfig();
                
                var workItemPrefix = projectConfig?.WorkItemPrefix;
                if (string.IsNullOrWhiteSpace(workItemPrefix))
                {
                    returnMsg = $"ERROR: (100) No workItemPrefix was provided in the configuration";
                    return returnMsg;
                }

                var polarionFilter = PolarionFilter.Create(null, true, false, [], false);
                var targetDocumentRevision = documentRevision == "-1" ? null : documentRevision;

                var results = await polarionClient.ExportModuleToMarkdownAsync(
                    workItemPrefix, documentName, polarionFilter, [], targetDocumentRevision);
                if (results.IsFailed)
                {
                    return $"ERROR: (3859) Failed to get headings for document. Error: {results.Errors.First()}";
                }

                var stringBuilder = results.Value;
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
        } // Close the scope
    }
}
