using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using PolarionMcpTools.Rbac;
using PolarionRemoteMcpServer.Rbac.Audit;
using PolarionRemoteMcpServer.Rbac.Caching;

namespace PolarionRemoteMcpServer.Rbac;

public static class RbacServiceCollectionExtensions
{
    /// <summary>
    /// Reads Rbac:Enabled eagerly, but only to pick the branch — same pattern as AddMcpAuth. A
    /// server with Rbac off never constructs the real gate/resolver or their dependencies; the
    /// <see cref="NoOpProjectVisibilityGate"/> Program.cs registers unconditionally stays wired.
    /// </summary>
    public static bool AddRbac(this WebApplicationBuilder builder, IMcpServerBuilder mcpBuilder)
    {
        var enabled = builder.Configuration.GetValue($"{RbacOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<RbacOptions>()
            .Bind(builder.Configuration.GetSection(RbacOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RbacOptions>, RbacOptionsValidator>();

        services.AddSingleton<IRbacCallContextAccessor, RbacCallContextAccessor>();
        services.AddSingleton<IMcpAccessAuditSink, SerilogMcpAccessAuditSink>();

        services.AddSingleton<PolarionIdentityLookup>();
        services.AddSingleton<IIdentityLookup>(sp => sp.GetRequiredService<PolarionIdentityLookup>());
        services.AddSingleton<CachingIdentityResolver>();
        services.AddSingleton<IIdentityResolver>(sp => sp.GetRequiredService<CachingIdentityResolver>());
        services.AddSingleton<IEvictableCache>(sp => sp.GetRequiredService<CachingIdentityResolver>().Cache);

        // Where the identity *value* comes from, upstream of the Polarion lookup above. Both
        // implementations are registered concretely and the branch is a single eager read of
        // Rbac:IdentitySource — a config value that selects a service graph, not one a request can
        // change. Rbac:IdentitySource=Claim (the default) resolves ClaimIdentitySource, which is the
        // pre-existing behavior verbatim.
        var identitySource = builder.Configuration.GetValue(
            $"{RbacOptions.SectionName}:{nameof(RbacOptions.IdentitySource)}", IdentitySource.Claim);

        if (identitySource == IdentitySource.UserInfo)
        {
            // The /userinfo call needs the caller's raw bearer token, which lives on HttpContext.Items
            // because the MCP SDK dispatches a tool call through a nested service scope.
            services.AddHttpContextAccessor();
            services.AddHttpClient(OktaUserInfoEmailSource.HttpClientName, client =>
            {
                // Short and explicit: this call is on the tool-call request path, so a slow identity
                // provider must fail fast into a denial rather than hold the caller open.
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddSingleton<OktaUserInfoEmailSource>();
            services.AddSingleton<IIdentitySource>(sp => sp.GetRequiredService<OktaUserInfoEmailSource>());
            services.AddSingleton<IEvictableCache>(sp => sp.GetRequiredService<OktaUserInfoEmailSource>().Cache);
        }
        else
        {
            services.AddSingleton<IIdentitySource, ClaimIdentitySource>();
        }

        services.AddSingleton<PolarionProjectUsersGate>();
        services.Replace(ServiceDescriptor.Singleton<IProjectVisibilityGate>(
            sp => sp.GetRequiredService<PolarionProjectUsersGate>()));
        services.AddSingleton<IEvictableCache>(sp => sp.GetRequiredService<PolarionProjectUsersGate>().Cache);

        services.AddHostedService<RbacCacheJanitor>();

        services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(RbacIdentityFilter.Create));

        return true;
    }
}
