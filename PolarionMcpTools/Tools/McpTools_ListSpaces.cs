namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "list_spaces"),
     Description("Lists all Space names in the Polarion project. Space names are filtered by an internal blacklist.")]
    public async Task<string> ListSpaces()
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
                string? blacklistPattern = projectConfig?.BlacklistSpaceContainingMatch;

                var spacesResult = await polarionClient.GetSpacesAsync(blacklistPattern);
                if (spacesResult.IsFailed)
                {
                    return $"ERROR: Failed to fetch Polarion spaces. Error: {spacesResult.Errors.First()}";
                }

                var spaces = spacesResult.Value;

                // return a comma-separated list of space names
                var combinedWorkItems = new StringBuilder();
                combinedWorkItems.AppendLine("# Polarion Space Names");
                combinedWorkItems.AppendLine($"- {string.Join("\n- ", spaces)}"); // markdown bullet list
                return combinedWorkItems.ToString();
            }
            catch (Exception ex)
            {
                returnMsg = $"ERROR: Failed to get Polarion Document Space Names due to exception '{ex.Message}'";
                if (ex.InnerException != null)
                {
                    returnMsg += $"\nInner Exception: {ex.InnerException.Message}";
                }
                return returnMsg;
            }
        } // Close the scope
    }
}
