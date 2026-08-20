using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PolarionMcpTools;
using PolarionMcpTools.Rbac;
using PolarionRemoteMcpServer.Rbac.Audit;
using System.Diagnostics;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// The single central interception point for every <c>tools/call</c> — registered via
/// <c>services.Configure&lt;McpServerOptions&gt;(o =&gt; o.Filters.Request.CallToolFilters.Add(...))</c>.
/// Resolves the caller's identity once, defers the actual allow/deny decision to
/// <see cref="DecideAsync"/>, emits exactly one <see cref="AccessAuditRecord"/> before ever calling
/// <c>next</c>, and is the single fail-closed short-circuit site.
///
/// Unlike a cross-project tool surface, the resource being checked is never taken from the call's
/// arguments — every Polarion MCP tool is already scoped to one project by the route
/// (<c>/{alias}/mcp</c>), so the alias comes from <see cref="IPolarionClientFactory.ProjectId"/>
/// (route data), the same source every tool body's own client creation already uses.
/// </summary>
public static class RbacIdentityFilter
{
    public const string DeniedMessage = "ERROR: The requested resource was not found.";

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Create(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var services = request.Services
                ?? throw new InvalidOperationException("RbacIdentityFilter requires RequestContext.Services.");

            var options = services.GetRequiredService<IOptions<RbacOptions>>().Value;
            var gate = services.GetRequiredService<IProjectVisibilityGate>();
            var identityResolver = services.GetRequiredService<IIdentityResolver>();
            var accessor = services.GetRequiredService<IRbacCallContextAccessor>();
            var auditSink = services.GetRequiredService<IMcpAccessAuditSink>();
            var clientFactory = services.GetRequiredService<IPolarionClientFactory>();

            var toolName = request.Params?.Name ?? string.Empty;
            var projectAlias = clientFactory.ProjectId;

            var identityClaimValue = request.User?.FindFirst(options.IdentityClaim)?.Value;
            var polarionUsername = identityClaimValue is not null
                ? await identityResolver.ResolveAsync(identityClaimValue, cancellationToken)
                : null;

            accessor.Current = new RbacCallContext
            {
                ToolName = toolName,
                ProjectAlias = projectAlias,
                IdentityClaimValue = identityClaimValue,
                PolarionUsername = polarionUsername,
            };

            try
            {
                var decision = await DecideAsync(gate, projectAlias, polarionUsername, cancellationToken);

                // Elapsed here is gate latency only — not the downstream tool body's own latency.
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var blocked = !decision.Allowed && !options.AuditOnly;

                var result = blocked ? DeniedResult() : await next(request, cancellationToken);

                auditSink.Record(new AccessAuditRecord(
                    DateTimeOffset.UtcNow,
                    request.User?.FindFirst("sub")?.Value,
                    request.User?.FindFirst("client_id")?.Value,
                    request.User?.FindFirst("jti")?.Value,
                    identityClaimValue,
                    polarionUsername,
                    toolName,
                    projectAlias,
                    decision.Allowed ? AccessDecision.Allow : AccessDecision.Deny,
                    decision.Reason,
                    blocked,
                    elapsedMs));

                return result;
            }
            finally
            {
                accessor.Current = null;
            }
        };
    }

    /// <summary>
    /// The ordered contract. Read top to bottom — there is no catch-all allow branch. When
    /// <c>Rbac:Enabled</c> is false, <see cref="NoOpProjectVisibilityGate"/> is wired and this
    /// always resolves to <c>gate.CheckProjectAsync</c> returning Allow; when true,
    /// <see cref="PolarionProjectUsersGate"/> performs the real per-project membership check.
    /// </summary>
    private static async ValueTask<GateDecision> DecideAsync(
        IProjectVisibilityGate gate, string? projectAlias, string? identity, CancellationToken cancellationToken)
    {
        if (!gate.Enabled)
            return GateDecision.Allow("rbac_gate_not_configured");

        if (identity is null)
            return GateDecision.Deny("identity_unresolved");

        if (string.IsNullOrEmpty(projectAlias))
            return GateDecision.Deny("project_alias_missing");

        return await gate.CheckProjectAsync(identity, projectAlias, cancellationToken);
    }

    private static CallToolResult DeniedResult() => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = DeniedMessage }],
    };
}
