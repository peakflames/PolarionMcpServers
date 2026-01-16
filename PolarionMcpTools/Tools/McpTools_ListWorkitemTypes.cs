namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [McpServerTool(Name = "list_workitem_types"),
     Description("Lists all configured WorkItem types for the current project.")]
    public Task<string> ListWorkitemTypes()
    {
        var currentProjectConfig = GetCurrentProjectConfig();
        if (currentProjectConfig == null)
        {
            return Task.FromResult("ERROR: Could not determine the current project configuration.");
        }

        var workItemTypeConfigs = currentProjectConfig.PolarionWorkItemTypes;

        if (workItemTypeConfigs == null || !workItemTypeConfigs.Any())
        {
            return Task.FromResult($"No WorkItem types are configured in PolarionWorkItemTypes for project '{currentProjectConfig.ProjectUrlAlias}'.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Configured WorkItem Types (Project: {currentProjectConfig.ProjectUrlAlias})");
        sb.AppendLine();
        foreach (var config in workItemTypeConfigs)
        {
            sb.AppendLine($"- {config.Id}");
        }
        return Task.FromResult(sb.ToString());
    }
}
