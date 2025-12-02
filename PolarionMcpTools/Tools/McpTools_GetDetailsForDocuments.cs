using System.Reflection;

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_details_for_document"), Description("Gets additional details for for a Polarion Document such as standard fields and custom fields")]
    public async Task<string> GetDetailsForDocuments(
        [Description("The Polarion Space.")] string space,
        [Description("The Polarion Document/Module Id (NOT the Document Title).")] string documentId,
        [Description("A comma-separated list of custom field names to retrieve (e.g., 'priority,severity,myCustomField'). Set to 'all' to retrieve all fields. Set to 'none' to not retrieve any custom fields.")] string customFieldWhitelist
        )
    {
        if (string.IsNullOrWhiteSpace(space))
        {
            return "ERROR: 'spaceName' parameter cannot be empty or whitespace.";
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return "ERROR: 'documentName' parameter cannot be empty or whitespace.";
        }

        var documentLocation = $"{space}/{documentId}";
        
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

            var targetCustomFieldNameWhitelist = customFieldWhitelist.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            
            var markdownConverter = new ReverseMarkdown.Converter();
            
            var getAllCustomFields = customFieldWhitelist.ToLower() == "all";
            var getNoCustomFields = customFieldWhitelist.ToLower() == "none";
                        
            try
            {
                var getModuleResult = await polarionClient.GetModuleByLocationAsync(documentLocation);
                if (getModuleResult.IsFailed)
                {
                    return $"ERROR: Failed to retrieve the document by the location '{documentLocation}'. Error: {getModuleResult.Errors.First()}";
                }

                var module = getModuleResult.Value;

                sb.AppendLine($"# Document (space='{space}', id='{documentId}')");
                sb.AppendLine();

                var moduleProperties = module.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                sb.AppendLine($"## Standard Fields");
                sb.AppendLine();

                if (projectConfig?.PolarionDocumentDefaultFields is null)
                {
                    sb.AppendLine($"- No standard document fields have been configured to be provided.");
                }
                else
                {
                    foreach (var fieldName in projectConfig?.PolarionDocumentDefaultFields ?? [])
                    {
                        bool fieldProcessed = false;
                        // Attempt to get standard property via reflection
                        var standardProperty = moduleProperties.FirstOrDefault(p =>
                            string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase)
                        );

                        if (standardProperty != null)
                        {
                            try
                            {
                                var value = standardProperty.GetValue(module);
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
                    sb.AppendLine($"## Custom Fields");
                    sb.AppendLine();

                    if (module.customFields is null || module.customFields.Length == 0)
                    {
                        sb.AppendLine($"- No custom fields found.");
                    }
                    else
                    {
                        foreach (var customField in module.customFields)
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

                
            }
            catch (Exception ex)
            {
                sb.AppendLine($"- ERROR: Failed to retrieve document details due to exception: {ex.Message}");
            }
            sb.AppendLine();
            
        }
        return sb.ToString();
    }
}

