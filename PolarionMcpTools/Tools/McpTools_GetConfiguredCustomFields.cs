namespace PolarionMcpTools;

    public sealed partial class McpTools
    {
        [McpServerTool(Name = "get_configured_custom_fields"), Description("Gets the configured list of custom fields for a specific WorkItem type ID from the current project's settings.")]
        public Task<string> GetConfiguredCustomFields(
            [Description("The ID of the WorkItem type (e.g., 'requirement', 'failureCondition').")] string workItemTypeId)
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

            var config = workItemTypeConfigs.FirstOrDefault(c => c.Id.Equals(workItemTypeId, System.StringComparison.OrdinalIgnoreCase));

            if (config == null)
            {
                return Task.FromResult($"ERROR: WorkItem type ID '{workItemTypeId}' not found in project '{currentProjectConfig.ProjectUrlAlias}' configuration.");
            }

            if (config.Fields == null || !config.Fields.Any())
            {
                return Task.FromResult($"No custom fields are configured for WorkItem type ID '{workItemTypeId}' in project '{currentProjectConfig.ProjectUrlAlias}'.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## Custom Fields for WorkItem Type: {workItemTypeId} (Project: {currentProjectConfig.ProjectUrlAlias})");
        sb.AppendLine();
        foreach (var field in config.Fields)
        {
            sb.AppendLine($"- {field}");
        }
        return Task.FromResult(sb.ToString());
    }
}
