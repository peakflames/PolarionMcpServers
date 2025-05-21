namespace PolarionMcpTools;

    public sealed partial class McpTools
    {
        [McpServerTool(Name = "list_configured_workitem_types"), Description("Lists all WorkItem type IDs that have configurations in the current project's PolarionWorkItemTypes settings.")]
        public Task<string> ListConfiguredWorkItemTypes()
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
