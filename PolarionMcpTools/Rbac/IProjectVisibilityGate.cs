namespace PolarionMcpTools.Rbac;

/// <summary>
/// Answers one question per <c>tools/call</c>: may this resolved identity access the Polarion
/// project bound to the call's route alias? Every Polarion MCP tool is already scoped to one project
/// by the route (<c>/{alias}/mcp</c>), so unlike a cross-project tool surface this needs no response
/// filtering, no permission mirror, and no per-tool resource-kind map — a single boolean per request,
/// decided before any Polarion data is fetched.
/// </summary>
public interface IProjectVisibilityGate
{
    /// <summary>False for <see cref="NoOpProjectVisibilityGate"/> — callers should treat a disabled
    /// gate as having nothing meaningful to say rather than relying on it to no-op.</summary>
    bool Enabled { get; }

    ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default);
}
