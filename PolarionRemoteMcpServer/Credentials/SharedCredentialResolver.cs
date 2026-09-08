using System.Security.Claims;
using FluentResults;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Credentials;

/// <summary>
/// Default resolver, active whenever <c>Credentials:Mode</c> is unset or <c>"Shared"</c>: every
/// caller authenticates upstream as the project's configured service account, exactly today's
/// behavior. Ignores <paramref name="user"/> entirely — there is no per-caller identity to apply.
/// </summary>
public sealed class SharedCredentialResolver : IUpstreamCredentialResolver
{
    public Task<Result<PolarionSessionCredential>> ResolveAsync(
        PolarionProjectConfig projectConfig, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        var sessionConfig = projectConfig.SessionConfig;
        if (sessionConfig is null)
        {
            return Task.FromResult(Result.Fail<PolarionSessionCredential>(
                $"Project '{projectConfig.ProjectUrlAlias}' has no SessionConfig to resolve a shared credential from."));
        }

        return Task.FromResult(Result.Ok(new PolarionSessionCredential
        {
            Kind = PolarionCredentialKind.Password,
            Username = sessionConfig.Username,
            Secret = sessionConfig.Password,
        }));
    }
}
