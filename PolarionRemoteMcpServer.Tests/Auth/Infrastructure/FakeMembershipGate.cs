using PolarionMcpTools.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Stands in for <see cref="PolarionRemoteMcpServer.Rbac.PolarionProjectUsersGate"/> with an
/// explicit, test-authored membership table keyed on (alias, identity) — the deny path for a
/// specific caller/project pair, without wiring up the SOAP-backed <c>getProjectUsers</c> emulation
/// the real gate uses.
/// </summary>
public sealed class FakeMembershipGate : IProjectVisibilityGate
{
    private readonly HashSet<(string Alias, string Identity)> _members = new();

    public bool Enabled => true;

    public FakeMembershipGate AllowMember(string projectAlias, string identity)
    {
        _members.Add((projectAlias, identity));
        return this;
    }

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_members.Contains((projectAlias, identity))
            ? GateDecision.Allow("project_member")
            : GateDecision.Deny("not_a_project_member"));
    }
}
