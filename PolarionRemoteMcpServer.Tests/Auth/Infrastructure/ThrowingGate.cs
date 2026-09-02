using PolarionMcpTools.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>Every check throws — proves a code path that must never reach the gate really never
/// does, rather than merely happening to deny today.</summary>
public sealed class ThrowingGate : IProjectVisibilityGate
{
    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ThrowingGate.CheckProjectAsync should never be called.");
}
