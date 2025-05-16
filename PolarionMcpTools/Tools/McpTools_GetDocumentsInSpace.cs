namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_documents_by_space_names"), 
        Description(
            "Gets the listing of all Documents in the Polarion Project with the specified Spaces. " +
            "Results is a Markdwon table containing the documents with the following columns: Title, Space, Type, Status."
         )]
    public async Task<string> GetDocumentBySpaceNames(
        
        [Description("Comma-sepereated list of Space names")]
        string spaceNames
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
                var modules = new List<ModuleThin>();

                var spaceNamesList = spaceNames.Split(',');

                // trim the spaces
                spaceNamesList = spaceNamesList.Select(x => x.Trim()).ToArray();

                if (spaceNamesList.Length == 0)
                {
                    return $"ERROR: (06653) No spaces were provided.";
                }

                if (spaceNamesList.Length == 1)
                {
                    var result = await polarionClient.GetModulesInSpaceThinAsync(spaceNamesList[0]);
                    if (result.IsFailed)
                    {
                        return $"ERROR: (06657) Failed to fetch Polarion document. Error: {result.Errors.First()}";
                    }

                    modules.AddRange(result.Value);
                }
                else
                {
                    
                    // Get the current project configuration to check for blacklist pattern
                    var projectConfig = GetCurrentProjectConfig();
                    string? blacklistPattern = projectConfig?.BlacklistSpaceContainingMatch;

                    var result = await polarionClient.GetModulesThinAsync(blacklistPattern);
                    if (result.IsFailed)
                    {
                        return $"ERROR: (06653) Failed to fetch Polarion document. Error: {result.Errors.First()}";
                    }

                    // Filter the results by the space names
                    modules.AddRange(result.Value.Where(x => spaceNamesList.Contains(x.Space)));
                }

                if (modules.Count == 0)
                {
                    return $"No Polarion document found";
                }

                // geerate a markdown list of document locations
                var combinedWorkItems = new StringBuilder();               

                var modulesBySpace = modules.GroupBy(x => x.Space);

                combinedWorkItems.AppendLine($"# Documents by Space");
                foreach (var space in modulesBySpace)
                {
                    combinedWorkItems.AppendLine($"## Documents in Space '{space.Key}'");
                    combinedWorkItems.AppendLine("");
                    combinedWorkItems.AppendLine($"| Title | Type | Status |");
                    combinedWorkItems.AppendLine($"| ---   | ---  | ------ |");
                    foreach (var module in space)
                    {
                        combinedWorkItems.AppendLine($"| {module.Title} | {module.Type} | {module.Status} |");
                    }
                    combinedWorkItems.AppendLine("");
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
