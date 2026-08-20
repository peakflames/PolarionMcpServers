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

        services.AddSingleton<PolarionProjectUsersGate>();
        services.Replace(ServiceDescriptor.Singleton<IProjectVisibilityGate>(
            sp => sp.GetRequiredService<PolarionProjectUsersGate>()));
        services.AddSingleton<IEvictableCache>(sp => sp.GetRequiredService<PolarionProjectUsersGate>().Cache);

        services.AddHostedService<RbacCacheJanitor>();

        services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(RbacIdentityFilter.Create));

        return true;
    }
}
