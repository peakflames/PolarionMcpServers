using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Polarion;
using PolarionMcpTools;
using PolarionMcpTools.Rbac;
using PolarionRemoteMcpServer.Rbac.Caching;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Asks Polarion itself, via <c>ProjectWebService.getProjectUsers</c>, whether the resolved
/// identity is an explicit member of the project mapped to the call's route alias. Verified
/// against a live Polarion server: membership differs per project rather than returning a
/// blanket allow, confirming this as a viable primary gate.
///
/// Membership lists are cached per real Polarion project id (never per alias, in case two aliases
/// ever map to the same underlying project) with <c>Rbac:MembershipCacheTtlSeconds</c>.
///
/// Fails closed: an unmapped alias, a project with no configured <c>SessionConfig</c>, or any
/// upstream failure all deny rather than allow.
/// </summary>
internal sealed class PolarionProjectUsersGate : IProjectVisibilityGate
{
    private readonly List<PolarionProjectConfig> _projectConfigs;
    private readonly TtlCache<string, HashSet<string>> _membershipCache;
    private readonly TimeSpan _membershipTtl;
    private readonly ILogger<PolarionProjectUsersGate> _logger;

    public bool Enabled => true;

    public PolarionProjectUsersGate(
        List<PolarionProjectConfig> projectConfigs,
        IOptions<RbacOptions> options,
        TimeProvider timeProvider,
        ILogger<PolarionProjectUsersGate> logger)
    {
        _projectConfigs = projectConfigs;
        _membershipTtl = TimeSpan.FromSeconds(options.Value.MembershipCacheTtlSeconds);
        _logger = logger;
        _membershipCache = new TtlCache<string, HashSet<string>>(
            options.Value.MaxCacheEntries, timeProvider, logger, "Rbac:MembershipCache", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Exposed so <c>AddRbac</c> can also register this instance under
    /// <see cref="IEvictableCache"/> for <see cref="RbacCacheJanitor"/>, without a second lookup.</summary>
    internal IEvictableCache Cache => _membershipCache;

    public async ValueTask<GateDecision> CheckProjectAsync(
        string identity, string projectAlias, CancellationToken cancellationToken = default)
    {
        var config = _projectConfigs.FirstOrDefault(p =>
            p.ProjectUrlAlias.Equals(projectAlias, StringComparison.OrdinalIgnoreCase));

        if (config?.SessionConfig is not { } sessionConfig)
            return GateDecision.Deny("unmapped_project_alias");

        var realProjectId = sessionConfig.ProjectId;
        if (string.IsNullOrWhiteSpace(realProjectId))
            return GateDecision.Deny("project_id_missing");

        try
        {
            var members = await _membershipCache.GetOrAddAsync(
                realProjectId,
                _membershipTtl,
                ct => FetchMembersAsync(sessionConfig, realProjectId, ct),
                cancellationToken);

            return members.Contains(identity)
                ? GateDecision.Allow()
                : GateDecision.Deny("not_a_project_member");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "RBAC membership check failed for project '{ProjectId}' (alias '{Alias}').", realProjectId, projectAlias);
            return GateDecision.Deny("membership_check_failed");
        }
    }

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    private static async Task<HashSet<string>> FetchMembersAsync(
        PolarionClientConfiguration sessionConfig, string realProjectId, CancellationToken cancellationToken)
    {
        var clientResult = await PolarionClient.CreateAsync(sessionConfig);
        if (clientResult.IsFailed)
            throw new InvalidOperationException($"Polarion login failed: {clientResult.Errors.FirstOrDefault()?.Message}");

        var usersResult = await clientResult.Value.GetProjectUsersAsync(realProjectId);
        if (usersResult.IsFailed)
            throw new InvalidOperationException($"getProjectUsers failed: {usersResult.Errors.FirstOrDefault()?.Message}");

        return new HashSet<string>(usersResult.Value.Select(u => u.id), StringComparer.OrdinalIgnoreCase);
    }
}
