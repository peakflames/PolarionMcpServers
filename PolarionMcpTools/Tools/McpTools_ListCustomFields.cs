namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [McpServerTool(Name = "list_custom_fields"),
     Description("Lists available custom fields for a specific WorkItem type.")]
    public Task<string> ListCustomFields(
        [Description("The WorkItem type ID (e.g., 'requirement', 'testCase', 'failureCondition').")] string workitemType)
    {
        var currentProjectConfig = GetCurrentProjectConfig();
        if (currentProjectConfig == null)
        {
            return Task.FromResult("ERROR: Could not determine the current project configuration.");
        }

        var workItemTypeConfigs = currentProjectConfig.PolarionWorkItemTypes;

        if (workItemTypeConfigs == null || !workItemTypeConfigs.Any())
        {
            return Task.FromResult($"ERROR: No WorkItem type configurations (PolarionWorkItemTypes) found for project '{currentProjectConfig.ProjectUrlAlias}'.");
        }

        var config = workItemTypeConfigs.FirstOrDefault(c => c.Id.Equals(workitemType, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            return Task.FromResult($"ERROR: WorkItem type '{workitemType}' not found in project '{currentProjectConfig.ProjectUrlAlias}' configuration.");
        }

        if (config.Fields == null || !config.Fields.Any())
        {
            return Task.FromResult($"No custom fields are configured for WorkItem type '{workitemType}' in project '{currentProjectConfig.ProjectUrlAlias}'.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Custom Fields for WorkItem Type: {workitemType} (Project: {currentProjectConfig.ProjectUrlAlias})");
        sb.AppendLine();
        foreach (var field in config.Fields)
        {
            sb.AppendLine($"- {field}");
        }
        return Task.FromResult(sb.ToString());
    }
}
