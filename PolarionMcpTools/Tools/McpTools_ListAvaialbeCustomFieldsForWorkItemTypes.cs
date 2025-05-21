namespace PolarionMcpTools;

    public sealed partial class McpTools
    {
        [McpServerTool(Name = "list_available_custom_fields_for_workitem_types"), Description("List the available custom fields for the specific WorkItem types. To obtain the list of WorkItem types, use the 'list_available_workitem_types' tool.")]
        public Task<string> ListAvaialbeCustomFieldsForWorkItemTypes(
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
