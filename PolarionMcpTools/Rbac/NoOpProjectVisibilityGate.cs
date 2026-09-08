namespace PolarionMcpTools.Rbac;

/// <summary>
/// The default gate for both hosts. Permanent for the stdio host (no HTTP identity ever reaches it);
/// the remote host's RBAC branch <c>Replace()</c>s this registration once the real gate mechanism
/// ships. Always allows — this is what makes <c>Rbac:Enabled=false</c> byte-identical to today's
/// behavior.
/// </summary>
public sealed class NoOpProjectVisibilityGate : IProjectVisibilityGate
{
    public bool Enabled => false;

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Allow());
}
