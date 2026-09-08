using PolarionMcpTools.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Records how many times <see cref="CheckProjectAsync"/> was invoked, and with what arguments —
/// used to prove a malformed/absent identity or alias denies without ever calling into the gate.
/// Always allows when called, so it can be composed with assertions on <see cref="CallCount"/> alone.
/// </summary>
public sealed class CountingGate : IProjectVisibilityGate
{
    public int CallCount { get; private set; }

    /// <summary>The exact <c>identity</c> string passed to the most recent
    /// <see cref="CheckProjectAsync"/> call.</summary>
    public string? LastIdentity { get; private set; }

    /// <summary>The exact <c>projectAlias</c> string passed to the most recent
    /// <see cref="CheckProjectAsync"/> call — pins that the gate receives the byte-identical,
    /// never-normalized argument value.</summary>
    public string? LastProjectAlias { get; private set; }

    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastIdentity = identity;
        LastProjectAlias = projectAlias;
        return ValueTask.FromResult(GateDecision.Allow());
    }
}
