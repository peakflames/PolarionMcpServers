namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_section"),
     Description("Gets content for a specific section heading and its sub-headings in a Polarion Document. Returns a partial Markdown document.")]
    public async Task<string> GetDocumentSection(
        [Description("The title of the Polarion document.")] string documentTitle,
        [Description("Section number (e.g., '1' or '3.4.5'). Returns the entire section including sub-sections like 3.4.5.1, 3.4.5.2, etc.")] string sectionNumber,
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

                var polarionFilter = PolarionFilter.Create(null, false, false, [], false);
                var targetDocumentRevision = revision == "-1" ? null : revision;

                var headinglevel = sectionNumber.Split('.').Length;

                var results = await polarionClient.ExportModuleToMarkdownGroupedByHeadingAsync(
                    headinglevel, workItemPrefix, documentTitle, polarionFilter, [], true, targetDocumentRevision);
                if (results.IsFailed)
                {
                    return $"ERROR: Failed to get section content for document. Error: {results.Errors.First()}";
                }

                var contentGroupedByHeading = results.Value;
                var targetContent = contentGroupedByHeading.Where(x => x.Key.Contains($"{sectionNumber}_")).FirstOrDefault();
                if (targetContent.Key is null)
                {
                    return $"ERROR: Failed to find section {sectionNumber} in document {documentTitle}";
                }
                return targetContent.Value.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get section content for document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        }
    }
}
