namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_section_content_for_document"), Description("Get content for a specific section heading and its sub-headings in a Polarion Document. Results in a partial Markdwon document")]
    public async Task<string> GetSectionContentInDocument(
        [Description("Name of Polarion document")]
        string documentName,

        [Description("Section Number (e.g. 1 or 3.4.5). Example, a value of 3.4.5 will return the entirety of section 3.4.5 and its sub-headings like 3.4.5.1, 3.4.5.2, etc.")]
        string documentNumber,

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
                    returnMsg = $"ERROR: (6185) No workItemPrefix was provided in the configuration";
                    return returnMsg;
                }

                var polarionFilter = PolarionFilter.Create(null, false, false, [], false);
                var targetDocumentRevision = documentRevision == "-1" ? null : documentRevision;

                var headinglevel = documentNumber.Split('.').Length;

                var results = await polarionClient.ExportModuleToMarkdownGroupedByHeadingAsync(
                    headinglevel, workItemPrefix, documentName, polarionFilter, [], targetDocumentRevision);
                if (results.IsFailed)
                {
                    return $"ERROR: (98651) Failed to section content for document. Error: {results.Errors.First()}";
                }

                var contentGroupedByHeading = results.Value;
                var targetContent = contentGroupedByHeading.Where(x => x.Key.Contains($"{documentNumber}_")).FirstOrDefault();
                if (targetContent.Key is null)
                {
                    return $"ERROR: (16684) Failed to find section {documentNumber} in document {documentName}";
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
        } // Close the scope
    }
}
