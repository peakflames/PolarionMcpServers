namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_document_revision_history"),
     Description("Gets the revision history for a Polarion document/module. " +
                 "Returns revision IDs with metadata showing when the document was modified. " +
                 "Use these revision IDs with get_workitems_in_module, get_document_section, or search_in_document.")]
    public async Task<string> GetDocumentRevisionHistory(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("Maximum number of revisions to return. Use -1 for all revisions. Default is 10.")]
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(space))
        {
            return "ERROR: (100) Space cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "ERROR: (101) Document ID cannot be empty.";
        }

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
                var location = $"{space}/{documentId}";
                var revisionsResult = await polarionClient.GetModuleRevisionsByLocationAsync(location, limit);

                if (revisionsResult.IsFailed)
                {
                    return $"ERROR: (1044) Failed to retrieve revision history for '{location}': {revisionsResult.Errors.First().Message}";
                }

                var revisions = revisionsResult.Value;

                if (revisions == null || revisions.Length == 0)
                {
                    return $"## Revision History for Document '{location}'\n\nNo revisions found.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"## Revision History for Document '{location}'");
                sb.AppendLine();

                var limitDescription = limit == -1 ? "all" : $"latest {limit}";
                sb.AppendLine($"Showing {limitDescription} revision{(revisions.Length != 1 ? "s" : "")} (newest to oldest)");
                sb.AppendLine();

                for (var i = 0; i < revisions.Length; i++)
                {
                    var module = revisions[i];
                    var isLatest = (i == 0);

                    // Extract revision ID from URI (format: ...?revision=XXXXX)
                    var revisionId = ExtractRevisionIdFromUri(module.uri);

                    sb.AppendLine("---");
                    sb.AppendLine();

                    var revisionHeader = $"### Revision {i + 1} (ID: {revisionId})";
                    if (isLatest)
                    {
                        revisionHeader += " (Latest)";
                    }
                    sb.AppendLine(revisionHeader);
                    sb.AppendLine();

                    if (module.updatedSpecified)
                    {
                        sb.AppendLine($"- **Updated**: {module.updated:yyyy-MM-dd HH:mm:ss}");
                    }

                    if (module.updatedBy != null && !string.IsNullOrEmpty(module.updatedBy.id))
                    {
                        sb.AppendLine($"- **Modified By**: {module.updatedBy.id}");
                    }

                    if (!string.IsNullOrEmpty(module.title))
                    {
                        sb.AppendLine($"- **Title**: {module.title}");
                    }

                    if (module.status != null && !string.IsNullOrEmpty(module.status.id))
                    {
                        sb.AppendLine($"- **Status**: {module.status.id}");
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: Failed due to exception '{ex.Message}'";
            }
        }
    }

    /// <summary>
    /// Extracts the revision ID from a Polarion module URI.
    /// URI formats supported:
    /// - subterra:data-service:objects:/default/...%XXXXX (percent format)
    /// - subterra:data-service:objects:/default/...?revision=XXXXX (query format)
    /// </summary>
    private static string ExtractRevisionIdFromUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return "N/A";
        }

        // Try percent format first (e.g., ...%611906)
        var percentIndex = uri.LastIndexOf('%');
        if (percentIndex >= 0 && percentIndex < uri.Length - 1)
        {
            return uri[(percentIndex + 1)..];
        }

        // Fall back to query format (e.g., ...?revision=611906)
        var revisionIndex = uri.IndexOf("?revision=", StringComparison.OrdinalIgnoreCase);
        if (revisionIndex >= 0)
        {
            return uri[(revisionIndex + 10)..]; // Skip "?revision="
        }

        return "N/A";
    }
}
