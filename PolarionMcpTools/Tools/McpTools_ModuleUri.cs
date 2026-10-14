namespace PolarionMcpTools;

/// <summary>
/// Builds module URIs for SDK calls that take a URI rather than a space/document pair.
/// </summary>
internal static class ModuleUri
{
    /// <summary>
    /// Builds the HEAD module URI. Mirrors the SDK's internal <c>PolarionUriParser.BuildModuleUri</c>
    /// (Polarion 0.4.1) because that helper is not public. Revision reads use the public
    /// <c>PolarionUriParser.BuildModuleUriWithRevision</c> instead.
    /// </summary>
    public static string BuildHeadModuleUri(string projectId, string space, string documentId)
    {
        return $"subterra:data-service:objects:/default/{projectId}${{Module}}{{moduleFolder}}{space}#{documentId}";
    }
}
