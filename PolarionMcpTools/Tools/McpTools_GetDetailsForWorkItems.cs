using System.Reflection;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_details_for_workitems"), Description("Gets additional details for a list of WorkItem IDs such as standard fields, custom fields, and linked work items.")]
    public async Task<string> GetDetailsForWorkItems(
        [Description("A comma-separated list of WorkItem IDs (e.g., 'PROJECT-123,PROJECT-456').")] string workItemIds,
        [Description("A comma-separated list of custom field names to retrieve (e.g., 'priority,severity,myCustomField'). Set to 'all' to retrieve all fields. Set to 'none' to not retrieve any custom fields.")] string customFieldWhitelist)
    {
        if (string.IsNullOrWhiteSpace(workItemIds))
        {
            return "ERROR: workItemIds parameter cannot be empty.";
        }

        
        var ids = workItemIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // List<string> targetCustomFieldNameWhitelist;

        if (ids.Length == 0)
        {
            return "ERROR: No valid WorkItem IDs provided after parsing.";
        }
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

            var targetCustomFieldNameWhitelist = customFieldWhitelist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            
            var markdownConverter = new ReverseMarkdown.Converter();
            
            var getAllCustomFields = customFieldWhitelist.ToLower() == "all";
            var getNoCustomFields = customFieldWhitelist.ToLower() == "none";

            foreach (var id in ids)
            {
                try
                {
                    var workItemResult = await polarionClient.GetWorkItemByIdAsync(id);
                    if (workItemResult.IsFailed)
                    {
                        sb.AppendLine($"## WorkItem: (id='{id}')");
                        sb.AppendLine();
                        sb.AppendLine($"- ERROR retrieving WorkItem '{id}': {workItemResult.Errors.FirstOrDefault()?.Message ?? "Unknown error"}");
                        sb.AppendLine();
                        continue;
                    }

                    var workItem = workItemResult.Value;
                    if (workItem == null) // Should not happen if IsFailed is false, but as a safeguard
                    {
                        sb.AppendLine($"## WorkItem: (id='{id}')");
                        sb.AppendLine();
                        sb.AppendLine($"- ERROR: WorkItem '{id}' not found (null value despite success).");
                        sb.AppendLine();
                        continue;
                    }

                    sb.AppendLine($"## WorkItem (id='{id}')");
                    sb.AppendLine();

                    var workItemProperties = workItem.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    sb.AppendLine($"### Standard Fields");
                    sb.AppendLine();

                    if (projectConfig?.PolarionWorkItemDefaultFields is null)
                    {
                        sb.AppendLine($"- No standard fields have been configured to be provided.");
                    }
                    else
                    {
                        foreach (var fieldName in projectConfig?.PolarionWorkItemDefaultFields ?? [])
                        {
                            bool fieldProcessed = false;
                            // Attempt to get standard property via reflection
                            var standardProperty = workItemProperties.FirstOrDefault(p =>
                                string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase)
                            );

                            if (standardProperty != null)
                            {
                                try
                                {
                                    var value = standardProperty.GetValue(workItem);
                                    string valueString = Utils.PolarionValueToString(value, markdownConverter);

                                    sb.AppendLine($"- **{fieldName}**: {valueString}");
                                    fieldProcessed = true;
                                }
                                catch (Exception ex)
                                {
                                    sb.AppendLine($"- **{fieldName}**: ERROR retrieving standard property: {ex.Message}");
                                    fieldProcessed = true; // Mark as processed to avoid looking in custom fields
                                }
                            }

                            if (!fieldProcessed) // Only report as not found if it was explicitly requested
                            {
                                sb.AppendLine($"- **{fieldName}**: Not found as a standard property.");
                            }
                        }
                    }

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
                                if (customField.key is null)
                                {
                                    continue;
                                }

                                if (!getAllCustomFields && !targetCustomFieldNameWhitelist.Contains(customField.key))
                                {
                                    continue;
                                }

                                var valueString = Utils.PolarionValueToString(customField.value, markdownConverter);

                                sb.AppendLine($"- **{customField.key}**: {valueString ?? "null"}");
                            }
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine($"### Incoming Linked WorkItems");
                    sb.AppendLine();

                    if (workItem.linkedWorkItemsDerived is null || workItem.linkedWorkItemsDerived.Length == 0)
                    {
                        sb.AppendLine($"- None.");
                    }
                    else
                    {
                        // generate markdown table with columsn for id, role, and suspect

                        sb.AppendLine($"| ID | Role | Suspect |");
                        sb.AppendLine($"| --- | --- | --- |");


                        foreach (var linkedWorkItemDerived in workItem.linkedWorkItemsDerived)
                        {
                            var linkRole = Utils.PolarionValueToString(linkedWorkItemDerived.role, markdownConverter);
                            if (linkRole is null || linkRole == "subsection_of")
                            {
                                continue;
                            }
                            // extract the linked work item from the workItemURI which return a string in there format of: subterra:data-service:objects:/default/Midnight${WorkItem}MD-53146
                            var linkedWorkItemId = linkedWorkItemDerived.workItemURI.Split("${WorkItem}")[1];
                            sb.AppendLine($"| {linkedWorkItemId} | {linkRole} | {linkedWorkItemDerived.suspect} |");
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine($"### Outgoing Linked WorkItems");
                    sb.AppendLine();

                    if (workItem.linkedWorkItems is null || workItem.linkedWorkItems.Length == 0)
                    {
                        sb.AppendLine($"- None.");
                    }
                    else
                    {
                        // generate markdown table with columsn for id, role, and suspect

                        sb.AppendLine($"| ID | Role | Suspect |");
                        sb.AppendLine($"| --- | --- | --- |");


                        foreach (var linkedWorkItem in workItem.linkedWorkItems)
                        {
                            var linkRole = Utils.PolarionValueToString(linkedWorkItem.role, markdownConverter);
                            if (linkRole is null || linkRole == "subsection_of")
                            {
                                continue;
                            }
                            // extract the linked work item from the workItemURI which return a string in there format of: subterra:data-service:objects:/default/Midnight${WorkItem}MD-53146
                            var linkedWorkItemId = linkedWorkItem.workItemURI.Split("${WorkItem}")[1];
                            sb.AppendLine($"| {linkedWorkItemId} | {linkRole} | {linkedWorkItem.suspect} |");
                        }
                    }
                }
                catch (Exception ex) // Catch issues with the GetWorkItemByIdAsync call or other unexpected errors
                {
                    sb.AppendLine($"- ERROR: Failed to retrieve WorkItem '{id}' due to exception: {ex.Message}");
                }
                sb.AppendLine(); // Add a blank line after each WorkItem's details
            }
        }
        return sb.ToString();
    }
}