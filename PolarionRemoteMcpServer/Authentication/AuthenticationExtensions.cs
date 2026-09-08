using Microsoft.AspNetCore.Authorization;
using Serilog;

namespace PolarionRemoteMcpServer.Authentication;

/// <summary>
/// Extension methods for configuring API key authentication.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// The authentication scheme name for API key authentication.
    /// </summary>
    public const string ApiKeyScheme = "ApiKey";

    /// <summary>
    /// Adds API key authentication services to the service collection.
    /// </summary>
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load consumer configuration
        var consumersConfig = configuration.GetSection("ApiConsumers").Get<ApiConsumersConfig>()
            ?? new ApiConsumersConfig();

        Log.Information("API Key authentication: Loaded {Count} consumer(s)", consumersConfig.Consumers.Count);
        foreach (var consumer in consumersConfig.Consumers)
        {
            Log.Debug("  - Consumer '{Id}': {Name} (Active: {Active}, Scopes: [{Scopes}])",
                consumer.Key, consumer.Value.Name, consumer.Value.Active,
                string.Join(", ", consumer.Value.AllowedScopes));
        }

        services.AddSingleton(consumersConfig);

        // Add authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = ApiKeyScheme;
            options.DefaultChallengeScheme = ApiKeyScheme;
        })
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, options => { });

        // Add authorization with scope-based policies. Pinned to the ApiKey scheme explicitly —
        // once McpAuth is enabled it changes the *default* authenticate scheme to JwtBearer so MCP
        // discovery works (see Auth/AuthenticationServiceCollectionExtensions.cs), and without this
        // pin these REST policies would silently start authenticating against Bearer tokens instead
        // of the X-API-Key header.
        services.AddAuthorization(options =>
        {
            foreach (var scope in ApiScopes.All)
            {
                options.AddPolicy(scope, policy =>
                {
                    policy.AddAuthenticationSchemes(ApiKeyScheme);
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new ScopeRequirement(scope));
                });
            }
        });

        // Register the scope authorization handler
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// Adds the authentication and authorization middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
