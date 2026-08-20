using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using PolarionRemoteMcpServer.Authentication;

namespace PolarionRemoteMcpServer.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Reads McpAuth:Enabled eagerly, but only to pick the branch — every scheme is configured
    /// through IConfigureOptions&lt;T&gt; classes resolving IOptions&lt;McpAuthOptions&gt; from
    /// DI, never from a value captured here. The validator and its ValidateOnStart hook are
    /// registered only inside this enabled branch: registering ValidateOnStart unconditionally
    /// would let a malformed McpAuth section break servers that have the feature off, exactly the
    /// regression this opt-in exists to prevent.
    /// </summary>
    public static bool AddMcpAuth(this WebApplicationBuilder builder, IMcpServerBuilder mcpBuilder)
    {
        var enabled = builder.Configuration.GetValue($"{McpAuthOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<McpAuthOptions>()
            .Bind(builder.Configuration.GetSection(McpAuthOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<McpAuthOptions>, McpAuthOptionsValidator>();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddSingleton<IConfigureOptions<McpAuthenticationOptions>, ConfigureMcpAuthenticationOptions>();

        // This second AddAuthentication(...) call layers on top of AddApiKeyAuthentication's
        // earlier one — the options system applies both configure delegates in registration
        // order, so this one (registered later) wins for DefaultAuthenticateScheme/
        // DefaultChallengeScheme. REST keeps working because its scope policies are pinned to the
        // ApiKey scheme explicitly (see AuthenticationExtensions.AddApiKeyAuthentication) rather
        // than relying on the default.
        services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddMcp(_ => { });

        // Schemes deliberately unpinned here (no AddAuthenticationSchemes call) — pinning
        // "Bearer" would route a 401 to JwtBearer instead of the MCP scheme, and the client would
        // never receive resource_metadata, silently breaking MCP discovery. This policy is
        // distinct from the REST "polarion:read" policy (which pins ApiKey) even though both
        // check the same ApiScopes.PolarionRead scope value.
        services.AddAuthorizationBuilder()
            .AddPolicy(ApiScopes.McpReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => ScopeClaimHelper.HasScope(context.User, ApiScopes.PolarionRead)));

        return true;
    }
}
