namespace PolarionMcpTools;

public sealed partial class McpTools
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    [McpServerTool(Name = "get_workitems_in_branched_document"),
     Description(
         "DEPRECATED: Use get_workitems_in_module with revision parameter instead. " +
         "This tool will be removed in a future version."
     )]
    public async Task<string> GetWorkItemsInBranchedDocument(
        [Description("The Polarion space name (e.g., 'FCC_L4_Air8_1').")]
        string space,

        [Description("The document ID within the space.")]
        string documentId,

        [Description("The document baseline revision number. Must be a valid document revision (not a work item revision). " +
                     "Use get_document_info or document history to find valid revision numbers.")]
        string revision)
    {
        var result = "**DEPRECATION WARNING**: This tool is deprecated. " +
                     "Please use 'get_workitems_in_module' with the 'revision' parameter instead.\n\n";
        result += await GetWorkItemsInModule(space, documentId, null, revision);
        return result;
    }
}
