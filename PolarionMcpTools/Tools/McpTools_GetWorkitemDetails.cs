using System.Reflection;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitem_details"),
     Description("Gets detailed information for WorkItems including standard fields, custom fields, and linked work items. Supports traceability with recursive link following.")]
    public async Task<string> GetWorkitemDetails(
        [Description("Comma-separated list of WorkItem IDs (e.g., 'MD-123,MD-456').")] string workitemIds,
        [Description("Custom fields: 'all', 'none', or comma-separated list (e.g., 'priority,severity').")] string? customFields = "none",
        [Description("Link direction filter: 'incoming' (items linking TO this), 'outgoing' (items this links TO), or 'both'.")] string? linkDirection = "both",
        [Description("Filter by link role. Comma-separated list (e.g., 'verifies,validates'). Leave empty for all link types.")] string? linkTypeFilter = null,
        [Description("Recursively follow links N levels deep. 1 = direct links only. Max 5.")] int followLevels = 1)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(workitemIds))
        {
            return "ERROR: workitemIds parameter cannot be empty.";
        }

        // Validate linkDirection
        var validDirections = new[] { "incoming", "outgoing", "both" };
        var direction = (linkDirection ?? "both").ToLower();
        if (!validDirections.Contains(direction))
        {
            return $"ERROR: linkDirection must be one of: {string.Join(", ", validDirections)}. Got: '{linkDirection}'";
        }

        // Validate and cap followLevels
        if (followLevels < 1) followLevels = 1;
        if (followLevels > 5) followLevels = 5;

        var ids = workitemIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0)
        {
            return "ERROR: No valid WorkItem IDs provided after parsing.";
        }

        // Parse link type filter
        var linkTypeFilters = string.IsNullOrWhiteSpace(linkTypeFilter)
            ? new HashSet<string>()
            : linkTypeFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLower())
                .ToHashSet();

        var sb = new StringBuilder();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var clientFactory = scope.ServiceProvider.GetRequiredService<IPolarionClientFactory>();
            var clientResult = await clientFactory.CreateClientAsync();
            if (clientResult.IsFailed)
            {
                return clientResult.Errors.FirstOrDefault()?.Message ?? "ERROR: Unknown error when creating Polarion client.";
            }
            var polarionClient = clientResult.Value;
            var projectConfig = GetCurrentProjectConfig();

            var customFieldsList = (customFields ?? "none").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var getAllCustomFields = customFields?.ToLower() == "all";
            var getNoCustomFields = customFields?.ToLower() == "none";

            var markdownConverter = new ReverseMarkdown.Converter();

            foreach (var id in ids)
            {
                try
                {
                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(id);
                    if (workItemResult.IsFailed)
                    {
                        sb.AppendLine($"## WorkItem (id='{id}')");
                        sb.AppendLine();
                        sb.AppendLine($"- ERROR: {workItemResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}");
                        sb.AppendLine();
                        continue;
                    }

                    var workItem = workItemResult.Value;
                    if (workItem == null)
                    {
                        sb.AppendLine($"## WorkItem (id='{id}')");
                        sb.AppendLine();
                        sb.AppendLine($"- ERROR: WorkItem not found.");
                        sb.AppendLine();
                        continue;
                    }

                    sb.AppendLine($"## WorkItem (id='{id}')");
                    sb.AppendLine();

                    // Standard Fields
                    var workItemProperties = workItem.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    sb.AppendLine($"### Standard Fields");
                    sb.AppendLine();

                    if (projectConfig?.PolarionWorkItemDefaultFields is null)
                    {
                        sb.AppendLine($"- No standard fields configured.");
                    }
                    else
                    {
                        foreach (var fieldName in projectConfig.PolarionWorkItemDefaultFields)
                        {
                            var standardProperty = workItemProperties.FirstOrDefault(p =>
                                string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));

                            if (standardProperty != null)
                            {
                                try
                                {
                                    var value = standardProperty.GetValue(workItem);
                                    var valueString = Utils.PolarionValueToString(value, markdownConverter);
                                    sb.AppendLine($"- **{fieldName}**: {valueString}");
                                }
                                catch (Exception ex)
                                {
                                    sb.AppendLine($"- **{fieldName}**: ERROR: {ex.Message}");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"- **{fieldName}**: Not found.");
                            }
                        }
                    }

                    // Custom Fields
                    if (!getNoCustomFields)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"### Custom Fields");
                        sb.AppendLine();

                        if (workItem.customFields is null || workItem.customFields.Length == 0)
                        {
                            sb.AppendLine($"- No custom fields found.");
                        }
                        else
                        {
                            foreach (var customField in workItem.customFields)
                            {
                                if (customField.key is null) continue;
                                if (!getAllCustomFields && !customFieldsList.Contains(customField.key)) continue;

                                var valueString = Utils.PolarionValueToString(customField.value, markdownConverter);
                                sb.AppendLine($"- **{customField.key}**: {valueString ?? "null"}");
                            }
                        }
                    }

                    // Linked WorkItems - with direction and type filtering
                    var showIncoming = direction == "incoming" || direction == "both";
                    var showOutgoing = direction == "outgoing" || direction == "both";

                    if (showIncoming)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"### Incoming Linked WorkItems");
                        sb.AppendLine();

                        if (workItem.linkedWorkItemsDerived is null || workItem.linkedWorkItemsDerived.Length == 0)
                        {
                            sb.AppendLine($"- None.");
                        }
                        else
                        {
                            sb.AppendLine($"| ID | Role | Suspect |");
                            sb.AppendLine($"| --- | --- | --- |");

                            foreach (var linked in workItem.linkedWorkItemsDerived)
                            {
                                var linkRole = Utils.PolarionValueToString(linked.role, markdownConverter);
                                if (linkRole is null || linkRole == "subsection_of") continue;

                                // Apply link type filter
                                if (linkTypeFilters.Count > 0 && !linkTypeFilters.Contains(linkRole.ToLower())) continue;

                                var linkedId = linked.workItemURI.Split("${WorkItem}")[1];
                                sb.AppendLine($"| {linkedId} | {linkRole} | {linked.suspect} |");
                            }
                        }
                    }

                    if (showOutgoing)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"### Outgoing Linked WorkItems");
                        sb.AppendLine();

                        if (workItem.linkedWorkItems is null || workItem.linkedWorkItems.Length == 0)
                        {
                            sb.AppendLine($"- None.");
                        }
                        else
                        {
                            sb.AppendLine($"| ID | Role | Suspect |");
                            sb.AppendLine($"| --- | --- | --- |");

                            foreach (var linked in workItem.linkedWorkItems)
                            {
                                var linkRole = Utils.PolarionValueToString(linked.role, markdownConverter);
                                if (linkRole is null || linkRole == "subsection_of") continue;

                                // Apply link type filter
                                if (linkTypeFilters.Count > 0 && !linkTypeFilters.Contains(linkRole.ToLower())) continue;

                                var linkedId = linked.workItemURI.Split("${WorkItem}")[1];
                                sb.AppendLine($"| {linkedId} | {linkRole} | {linked.suspect} |");
                            }
                        }
                    }

                    // Recursive traceability (if followLevels > 1)
                    if (followLevels > 1)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"### Traceability Chain ({followLevels} levels)");
                        sb.AppendLine();

                        var visited = new HashSet<string> { id };
                        var traceResults = await GetTraceabilityChainAsync(
                            polarionClient, id, direction, linkTypeFilters, followLevels, 1, visited, markdownConverter);

                        if (traceResults.Count == 0)
                        {
                            sb.AppendLine($"- No additional linked items found within {followLevels} levels.");
                        }
                        else
                        {
                            sb.AppendLine($"| Level | ID | Role | Linked From |");
                            sb.AppendLine($"| --- | --- | --- | --- |");

                            foreach (var trace in traceResults)
                            {
                                sb.AppendLine($"| {trace.Level} | {trace.Id} | {trace.Role} | {trace.LinkedFrom} |");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"## WorkItem (id='{id}')");
                    sb.AppendLine();
                    sb.AppendLine($"- ERROR: {ex.Message}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private async Task<List<(int Level, string Id, string Role, string LinkedFrom)>> GetTraceabilityChainAsync(
        Polarion.IPolarionClient polarionClient,
        string startId,
        string direction,
        HashSet<string> linkTypeFilters,
        int maxLevels,
        int currentLevel,
        HashSet<string> visited,
        ReverseMarkdown.Converter markdownConverter)
    {
        var results = new List<(int Level, string Id, string Role, string LinkedFrom)>();

        if (currentLevel > maxLevels) return results;

        var workItemResult = await polarionClient.GetWorkItemByIdAsync(startId);
        if (workItemResult.IsFailed) return results;

        var workItem = workItemResult.Value;
        if (workItem == null) return results;

        var linkedIds = new List<(string Id, string Role)>();

        // Collect linked IDs based on direction
        if ((direction == "incoming" || direction == "both") && workItem.linkedWorkItemsDerived != null)
        {
            foreach (var linked in workItem.linkedWorkItemsDerived)
            {
                var linkRole = Utils.PolarionValueToString(linked.role, markdownConverter);
                if (linkRole is null || linkRole == "subsection_of") continue;
                if (linkTypeFilters.Count > 0 && !linkTypeFilters.Contains(linkRole.ToLower())) continue;

                var linkedId = linked.workItemURI.Split("${WorkItem}")[1];
                if (!visited.Contains(linkedId))
                {
                    linkedIds.Add((linkedId, linkRole));
                }
            }
        }

        if ((direction == "outgoing" || direction == "both") && workItem.linkedWorkItems != null)
        {
            foreach (var linked in workItem.linkedWorkItems)
            {
                var linkRole = Utils.PolarionValueToString(linked.role, markdownConverter);
                if (linkRole is null || linkRole == "subsection_of") continue;
                if (linkTypeFilters.Count > 0 && !linkTypeFilters.Contains(linkRole.ToLower())) continue;

                var linkedId = linked.workItemURI.Split("${WorkItem}")[1];
                if (!visited.Contains(linkedId))
                {
                    linkedIds.Add((linkedId, linkRole));
                }
            }
        }

        // Add to results and recurse
        foreach (var (linkedId, role) in linkedIds)
        {
            if (visited.Contains(linkedId)) continue;
            visited.Add(linkedId);

            results.Add((currentLevel, linkedId, role, startId));

            // Recurse to next level
            if (currentLevel < maxLevels)
            {
                var childResults = await GetTraceabilityChainAsync(
                    polarionClient, linkedId, direction, linkTypeFilters, maxLevels, currentLevel + 1, visited, markdownConverter);
                results.AddRange(childResults);
            }
        }

        return results;
    }
}
