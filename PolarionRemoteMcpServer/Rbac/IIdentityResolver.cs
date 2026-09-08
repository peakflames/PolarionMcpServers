namespace PolarionRemoteMcpServer.Rbac;

/// <summary>Resolves an OAuth claim value (e.g. an email address) to a Polarion username. Returns
/// null on zero or ambiguous matches — callers treat null as fail-closed, never as
/// "unrestricted".</summary>
public interface IIdentityResolver
{
    Task<string?> ResolveAsync(string identityClaimValue, CancellationToken cancellationToken = default);
}
