namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "list_documents"),
     Description("Lists all Documents in the Polarion Project. Optionally filter by space name and/or title. Results are returned as a Markdown table with columns: Id, Title, Space, Type, Status.")]
    public async Task<string> ListDocuments(
        [Description("Optional space name to filter documents by. If not provided, returns documents from all spaces.")] string? space = null,
        [Description("Optional title filter. Returns documents whose title contains this string (case-insensitive).")] string? titleFilter = null)
    {
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
                var projectConfig = GetCurrentProjectConfig();
                string? blacklistPattern = projectConfig?.BlacklistSpaceContainingMatch;

                List<ModuleThin> modules;

                if (!string.IsNullOrWhiteSpace(space))
                {
                    // Filter by specific space
                    var result = await polarionClient.GetModulesInSpaceThinAsync(space);
                    if (result.IsFailed)
                    {
                        return $"ERROR: Failed to fetch documents from space '{space}'. Error: {result.Errors.First()}";
                    }
                    modules = result.Value.ToList();
                }
                else
                {
                    // Get all documents (with optional title filter passed to API)
                    var result = await polarionClient.GetModulesThinAsync(blacklistPattern, titleFilter);
                    if (result.IsFailed)
                    {
                        return $"ERROR: Failed to fetch Polarion documents. Error: {result.Errors.First()}";
                    }
                    modules = result.Value.ToList();
                }

                // Apply title filter if space was specified (API doesn't filter when getting by space)
                if (!string.IsNullOrWhiteSpace(space) && !string.IsNullOrWhiteSpace(titleFilter))
                {
                    modules = modules.Where(m =>
                        m.Title != null && m.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (modules.Count == 0)
                {
                    var filterDesc = "";
                    if (!string.IsNullOrWhiteSpace(space)) filterDesc += $" in space '{space}'";
                    if (!string.IsNullOrWhiteSpace(titleFilter)) filterDesc += $" matching '{titleFilter}'";
                    return $"No documents found{filterDesc}.";
                }

                var sb = new StringBuilder();

                // Build header
                if (!string.IsNullOrWhiteSpace(space) || !string.IsNullOrWhiteSpace(titleFilter))
                {
                    var filterParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(space)) filterParts.Add($"Space: '{space}'");
                    if (!string.IsNullOrWhiteSpace(titleFilter)) filterParts.Add($"Title contains: '{titleFilter}'");
                    sb.AppendLine($"# Polarion Documents ({string.Join(", ", filterParts)})");
                }
                else
                {
                    sb.AppendLine("# Polarion Documents");
                }

                sb.AppendLine();
                sb.AppendLine($"Found {modules.Count} document(s).");
                sb.AppendLine();
                sb.AppendLine("| Id | Title | Space | Type | Status |");
                sb.AppendLine("| --- | --- | --- | --- | --- |");

                foreach (var module in modules.OrderBy(m => m.Space).ThenBy(m => m.Title))
                {
                    sb.AppendLine($"| {module.Id} | {module.Title} | {module.Space} | {module.Type} | {module.Status} |");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                var errorMsg = $"ERROR: Failed to list documents due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    errorMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return errorMsg;
            }
        }
    }
}
