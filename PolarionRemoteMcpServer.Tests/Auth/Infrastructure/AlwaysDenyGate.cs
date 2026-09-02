using PolarionMcpTools.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>A gate that unconditionally denies every check.</summary>
public sealed class AlwaysDenyGate : IProjectVisibilityGate
{
    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GateDecision.Deny("always_deny_gate"));
}
