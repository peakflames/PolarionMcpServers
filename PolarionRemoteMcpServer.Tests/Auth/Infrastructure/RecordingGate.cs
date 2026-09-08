using PolarionMcpTools.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Substituted for the real gate via <see cref="PolarionMcpServerFactory.WithPostAuthServices"/>
/// (which runs after <c>AddRbac</c>'s own <c>Replace()</c> call, the only seam that can win against
/// it). Always allows, and records every call so a test can assert the exact identity/alias pair
/// the filter passed through.
/// </summary>
public sealed class RecordingGate : IProjectVisibilityGate
{
    public List<RecordedCall> Calls { get; } = [];

    public bool Enabled => true;

    public ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default)
    {
        Calls.Add(new RecordedCall(identity, projectAlias));
        return ValueTask.FromResult(GateDecision.Allow());
    }

    public sealed record RecordedCall(string Identity, string ProjectAlias);
}
