namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_custom_fields_for_workitems"), Description("Gets specified custom fields for a list of WorkItem IDs.")]
    public async Task<string> GetCustomFieldsForWorkItems(
        [Description("A comma-separated list of WorkItem IDs (e.g., 'PROJECT-123,PROJECT-456').")] string workItemIds,
        [Description("A comma-separated list of custom field names to retrieve (e.g., 'priority,severity,myCustomField'). If set to empty string, all documents are returned.")] string customFields)
    {
        if (string.IsNullOrWhiteSpace(workItemIds))
        {
            return "ERROR: workItemIds parameter cannot be empty.";
        }

        
        var ids = workItemIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fieldNames = customFields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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
            
            var markdwonConverter = new ReverseMarkdown.Converter();

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

                    sb.AppendLine($"## WorkItem (id='{id}', type='{workItem.type?.id}', lastUpdated='{workItem.updated}')");
                    sb.AppendLine();

                    if (workItem.customFields is null)
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

                            if (fieldNames.Count > 0 && !fieldNames.Contains(customField.key))
                            {
                                continue;
                            }

                            // if the custom field value is of type string, convert it to a string
                            // else if it is of type enumOptionId, convert it to the enumOptionId.id
                            // else if it is of type Text, convert it to a string
                            string? valueString = null;

                            if (customField.value is EnumOptionId enumId)
                            {
                                valueString = enumId.id;
                            }
                            else if (customField.value is Text text)
                            {
                                try
                                {
                                    valueString = $"\n\n{markdwonConverter.Convert(text.content.ToString())}\n";
                                }
                                catch (Exception ex)
                                {
                                    valueString = $"ERROR: Failed to convert Text value to markdown due to exception: {ex.Message}";
                                }
                            }
                            else
                            {
                                valueString = customField.value.ToString();
                            }

                            sb.AppendLine($"- **{customField.key}**: {valueString ?? "null"}");
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

