namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_documents"), 
        Description(
            "Gets the listing of all Documents in the Polarion Project with an otional filter by Title. " +
            "Results is a Markdwon table containing the documents with the following columns: Title, Space, Type, Status."
         )]
    public async Task<string> GetDocuments(
        
        [Description("Optional filter by title. If set to empty string, all documents are returned.")]
        string titleContains
        )
    {
        string? returnMsg;
        
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.First().ToString() ?? "Internal Error (35864) unknown error when creating Polarion client";
            }

            var polarionClient = clientResult.Value;

            try
            {
                // Get the current project configuration to check for blacklist pattern
                var projectConfig = GetCurrentProjectConfig();
                string? blacklistPattern = projectConfig?.BlacklistSpaceContainingMatch;

                var result = await polarionClient.GetModulesThinAsync(blacklistPattern, titleContains);
                if (result.IsFailed)
                {
                    return $"ERROR: (06653) Failed to fetch Polarion document. Error: {result.Errors.First()}";
                }

                var modules = result.Value;

                if (modules.Length == 0)
                {
                    return $"No Polarion document found";
                }

                // geerate a markdown list of document locations
                var combinedWorkItems = new StringBuilder();

                if (string.IsNullOrEmpty(titleContains))
                {
                    combinedWorkItems.AppendLine("# Polarion Documents");
                }
                else
                {
                    combinedWorkItems.AppendLine($"# Polarion Documents with Title containing '{titleContains}'");
                }

                combinedWorkItems.AppendLine($"| Title | Space | Type | Status |");
                combinedWorkItems.AppendLine($"| ---   | ---   | ---  | ------ |");
                foreach (var module in modules)
                {
                    combinedWorkItems.AppendLine($"| {module.Title} | {module.Space} | {module.Type} | {module.Status} |");
                }
                
                return combinedWorkItems.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion Document due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
