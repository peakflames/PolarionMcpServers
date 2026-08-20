using System.Diagnostics.CodeAnalysis;
using Polarion;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Resolves an OAuth email claim to a Polarion user id via <c>ProjectWebService.getUsers()</c>,
/// joining on the Polarion user's own <c>email</c> field — never the <c>id == local-part</c>
/// convention, which holds for most accounts but breaks for aliased domains.
///
/// <c>getUsers()</c> is a server-wide operation with no project scope, so this authenticates using
/// the default project's shared service account rather than the alias the caller happened to be
/// hitting. This assumes every configured project alias's users live on the same Polarion server as
/// the default alias — true for the single-server deployments this targets, but not for the
/// multi-server example in appsettings.json's placeholder fixtures. A future per-alias identity
/// resolver would be needed if that ever becomes real.
/// </summary>
internal sealed class PolarionIdentityLookup : IIdentityLookup
{
    private readonly List<PolarionProjectConfig> _projectConfigs;
    private readonly ILogger<PolarionIdentityLookup> _logger;

    public PolarionIdentityLookup(List<PolarionProjectConfig> projectConfigs, ILogger<PolarionIdentityLookup> logger)
    {
        _projectConfigs = projectConfigs;
        _logger = logger;
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public async Task<IdentityResult> LookupAsync(string claimValue, CancellationToken cancellationToken = default)
    {
        var defaultConfig = _projectConfigs.FirstOrDefault(p => p.Default);
        if (defaultConfig?.SessionConfig is not { } sessionConfig)
        {
            _logger.LogError("RBAC identity lookup found no default Polarion project configuration.");
            throw new IdentityLookupException("no_default_project_configured");
        }

        var clientResult = await PolarionClient.CreateAsync(sessionConfig);
        if (clientResult.IsFailed)
        {
            _logger.LogWarning(
                "RBAC identity lookup could not log in to Polarion: {Error}",
                clientResult.Errors.FirstOrDefault()?.Message);
            throw new IdentityLookupException("polarion_login_failed");
        }

        var usersResult = await clientResult.Value.GetUsersAsync();
        if (usersResult.IsFailed)
        {
            _logger.LogWarning(
                "RBAC identity lookup's getUsers() call failed: {Error}",
                usersResult.Errors.FirstOrDefault()?.Message);
            throw new IdentityLookupException("polarion_get_users_failed");
        }

        var matches = usersResult.Value
            .Where(u => string.Equals(u.email, claimValue, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => new IdentityResult(IdentityOutcome.NotFound, null),
            1 => new IdentityResult(IdentityOutcome.Resolved, matches[0].id),
            _ => new IdentityResult(IdentityOutcome.Ambiguous, null),
        };
    }
}
