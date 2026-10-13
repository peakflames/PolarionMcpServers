namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_module"),
     Description(
         "Query work items from a Polarion module/document. " +
         "Pinned references show their pinned revision. Unresolvable references are reported with their URIs. " +
         "Optionally filter by work item types. " +
         "Supports querying historical document revisions using the revision parameter."
     )]
    public async Task<string> GetWorkItemsInModule(
        [Description("The Polarion space name (e.g., 'MySpace').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("Optional comma-separated list of work item types to filter (e.g., 'requirement,testCase'). Leave empty for all types.")]
        string? itemTypes = null,

        [Description("Document revision (number), '-1' for latest, or a baseline name or ID.")]
        string revision = "-1")
    {
        if (string.IsNullOrWhiteSpace(space))
        {
            return "ERROR: (100) Space cannot be empty.";
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "ERROR: (101) Document ID cannot be empty.";
        }

        // A number-like revision must be "-1" or a positive integer. Anything else is a baseline
        // name or ID and is resolved after the client exists.
        if (!IsBaselineReference(revision) && !IsValidRevision(revision))
        {
            return "ERROR: (102) Revision must be '-1' for the latest revision or a positive integer baseline revision ID.";
        }

        // space and documentId are embedded in Polarion URIs and queries; block injection chars.
        if (!IsSafeForPolarionPathParam(space))
        {
            return "ERROR: (103) space contains characters that are not permitted (single-quote, semicolon, or comment tokens).";
        }

        if (!IsSafeForPolarionPathParam(documentId))
        {
            return "ERROR: (104) documentId contains characters that are not permitted (single-quote, semicolon, or comment tokens).";
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync();
        if (clientResult.IsFailed)
        {
            return clientResult.Errors.First().ToString() ?? "Internal Error (3584) unknown error when creating Polarion client";
        }

        var polarionClient = clientResult.Value;

        try
        {
            // Parse item types if provided
            List<string>? typeList = null;
            if (!string.IsNullOrWhiteSpace(itemTypes))
            {
                // Containment: types flow into a Polarion query filter; only
                // identifier characters are permitted so a token cannot alter query shape.
                if (!AreCsvTokensSafeIdentifiers(itemTypes))
                {
                    return "ERROR: (107) itemTypes may only contain identifier characters (letters, digits, '_', '.', '-').";
                }

                typeList = itemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            var revisionResult = await ResolveRevisionAsync(polarionClient, revision, clientFactory.PolarionProjectId, space, documentId);
            if (revisionResult.IsFailed)
            {
                return revisionResult.Errors.First().Message;
            }

            var revisionNumber = revisionResult.Value;

            var projectId = clientFactory.PolarionProjectId;
            if (string.IsNullOrEmpty(projectId))
            {
                return "ERROR: (114) Polarion project ID is not available for this configuration.";
            }

            var uri = revisionNumber == "-1"
                ? ModuleUri.BuildHeadModuleUri(projectId, space, documentId)
                : PolarionUriParser.BuildModuleUriWithRevision(projectId, space, documentId, revisionNumber);

            var rowsResult = await polarionClient.GetModuleWorkItemsAsync(uri, null, true, null);
            if (rowsResult.IsFailed)
            {
                var errorMessage = rowsResult.Errors.First().Message;

                if (errorMessage.Contains("UnresolvableObjectException", StringComparison.OrdinalIgnoreCase))
                {
                    return $"ERROR: (1044) The document '{space}/{documentId}' could not be found at revision '{revisionNumber}'. " +
                           $"This typically means the revision number is invalid for this document. " +
                           $"Common causes:\n" +
                           $"  1. The revision number is a work item revision, not a document baseline revision\n" +
                           $"  2. The document did not exist at the specified revision\n\n" +
                           $"To find valid document revisions, use get_document_revision_history.";
                }

                return $"ERROR: (1044) Failed to fetch work items. Error: {errorMessage}";
            }

            var rows = rowsResult.Value ?? Array.Empty<ModuleWorkItem>();

            var unresolved = rows.Where(r => r.IsUnresolvable).ToList();

            // Type filtering applies to resolved rows only; unresolvable rows carry no type.
            var resolved = rows
                .Where(r => !r.IsUnresolvable && r.WorkItem is not null)
                .Where(r => typeList is null || typeList.Count == 0 || typeList.Contains(r.WorkItem.type?.id ?? string.Empty))
                .ToList();

            if (resolved.Count == 0 && unresolved.Count == 0)
            {
                return $"No work items found in module '{space}/{documentId}'.";
            }

            var result = new StringBuilder();
            result.AppendLine($"# Work Items in Module");
            result.AppendLine();
            result.AppendLine($"- **Space**: {space}");
            result.AppendLine($"- **Document ID**: {documentId}");

            if (revisionNumber != "-1")
            {
                result.AppendLine($"- **Revision**: {revisionNumber}");
            }

            if (typeList != null && typeList.Count > 0)
            {
                result.AppendLine($"- **Filtered Types**: {string.Join(", ", typeList)}");
            }

            result.AppendLine($"- **Total Work Items**: {resolved.Count}");
            result.AppendLine($"- **Unresolvable References**: {unresolved.Count}");
            result.AppendLine();

            foreach (var row in resolved)
            {
                var workItem = row.WorkItem;
                var lastUpdated = workItem.updatedSpecified ? workItem.updated.ToString("yyyy-MM-dd HH:mm:ss") : "N/A";

                result.AppendLine($"## WorkItem (id={workItem.id ?? "N/A"}, type={workItem.type?.id ?? "N/A"}, lastUpdated={lastUpdated})");
                result.AppendLine();
                result.AppendLine($"- **Outline Number**: {workItem.outlineNumber ?? "N/A"}");
                result.AppendLine($"- **Title**: {workItem.title ?? "N/A"}");
                result.AppendLine($"- **Status**: {workItem.status?.id ?? "N/A"}");

                if (row.IsPinned)
                {
                    result.AppendLine($"- **Pinned Revision**: {row.Revision}");
                }

                result.AppendLine();

                if (!string.IsNullOrWhiteSpace(workItem.description?.content))
                {
                    var markdown = polarionClient.ConvertWorkItemToMarkdown(workItem.id ?? "unknown", workItem);
                    result.AppendLine("### Description");
                    result.AppendLine();
                    result.AppendLine(markdown);
                    result.AppendLine();
                }
            }

            if (unresolved.Count > 0)
            {
                result.AppendLine("## Unresolvable References");
                result.AppendLine();

                foreach (var row in unresolved)
                {
                    result.AppendLine($"- {row.Uri}");
                }

                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed due to exception '{ex.Message}'";

        }
    }
}
