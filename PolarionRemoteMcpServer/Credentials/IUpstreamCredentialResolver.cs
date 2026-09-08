using System.Security.Claims;
using FluentResults;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Credentials;

/// <summary>
/// Resolves which Polarion identity a request's upstream <c>PolarionClient.CreateAsync</c> call
/// should authenticate as. <paramref name="user"/> is the caller's validated
/// <see cref="ClaimsPrincipal"/> when <c>McpAuth:Enabled</c> is true, and null for anonymous
/// requests — <see cref="SharedCredentialResolver"/>, the default, ignores it either way.
/// </summary>
public interface IUpstreamCredentialResolver
{
    Task<Result<PolarionSessionCredential>> ResolveAsync(
        PolarionProjectConfig projectConfig, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
