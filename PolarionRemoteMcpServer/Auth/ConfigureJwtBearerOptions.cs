using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Configures the resource-server gate through DI (resolving IOptions&lt;McpAuthOptions&gt;)
/// rather than an eager copy captured in an AddJwtBearer lambda — capturing an eager copy at
/// registration time would make McpAuthOptionsValidator's ValidateOnStart purely decorative,
/// since the lambda would run before the validator has had a chance to reject it.
/// Authority is set to the external authorization server — JwtBearer fetches its discovery
/// document and JWKS from there (with automatic key-rotation refresh) rather than us holding or
/// resolving signing keys ourselves.
///
/// Implements IConfigureNamedOptions, not plain IConfigureOptions: the options system only
/// invokes a plain IConfigureOptions&lt;T&gt; registration for the *default-named* ("") options
/// instance. JwtBearer's options are requested under the scheme name ("Bearer"), so a plain
/// IConfigureOptions&lt;JwtBearerOptions&gt; here would be silently skipped.
/// </summary>
public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<McpAuthOptions> _authOptions;
    private readonly List<PolarionProjectConfig> _projects;

    public ConfigureJwtBearerOptions(IOptions<McpAuthOptions> authOptions, List<PolarionProjectConfig> projects)
    {
        _authOptions = authOptions;
        _projects = projects;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var auth = _authOptions.Value;

        options.Authority = auth.Issuer;
        if (!string.IsNullOrWhiteSpace(auth.MetadataAddress))
        {
            options.MetadataAddress = auth.MetadataAddress;
        }

        // Derived from the configured Issuer's scheme, not IHostEnvironment — McpAuthOptionsValidator
        // already only allows a plaintext http Issuer in Development, so this mirrors that decision
        // instead of duplicating it via a second environment check.
        options.RequireHttpsMetadata = auth.Issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        // Keep "sub" as "sub" instead of ASP.NET's default ClaimTypes.NameIdentifier mapping —
        // a later per-caller RBAC gate will key on the raw "sub" claim.
        options.MapInboundClaims = false;

        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;

        // The only relaxation this class permits, and only when explicitly configured: an Okta
        // *org* authorization server stamps `aud` with its own issuer and offers no way to set a
        // per-resource audience. ClientIdRequirement then carries the binding instead, and
        // McpAuthOptionsValidator refuses to boot if that substitute is missing.
        tvp.ValidateAudience = auth.ValidateAudience;
        tvp.ValidateLifetime = true;
        tvp.ValidateIssuerSigningKey = true;
        tvp.ValidIssuer = auth.Issuer;

        // One MCP server serves several project aliases, each advertising its own RFC 9728
        // `resource` (see McpAuthOptions.ResourceFor / ConfigureMcpAuthenticationOptions). The
        // spec-conforming audience check therefore accepts any of them, not a single fixed value.
        // Moot while ValidateAudience is false (the org-AS shape), but keeps the audience-validating
        // path correct for a future authorization server that can mint one.
        tvp.ValidAudiences = _projects.Select(p => auth.ResourceFor(p.ProjectUrlAlias));
        tvp.ClockSkew = TimeSpan.FromSeconds(auth.ClockSkewSeconds);

        // Pinned — blocks alg-confusion attacks and "none".
        tvp.ValidAlgorithms = ["RS256"];

        CaptureRawTokenForUserInfo(options);
    }

    /// <summary>
    /// Stashes the validated raw token on the HttpContext so an OIDC <c>/userinfo</c> call can be
    /// made on the caller's behalf — the only way to reach an `email` for an authorization server
    /// that puts none on its access tokens. Captured here rather than re-read from the
    /// Authorization header downstream so only a token that already *passed* validation is ever
    /// reachable.
    ///
    /// The existing delegate is chained, not replaced: JwtBearerEvents initializes its delegates to
    /// no-ops, so overwriting looks harmless today and would silently drop someone else's handler
    /// the moment one is added.
    /// </summary>
    private static void CaptureRawTokenForUserInfo(JwtBearerOptions options)
    {
        options.Events ??= new JwtBearerEvents();
        var previous = options.Events.OnTokenValidated;

        options.Events.OnTokenValidated = async context =>
        {
            await previous(context);

            if (context.SecurityToken is JsonWebToken jwt)
            {
                context.HttpContext.Items[McpAuthHttpContextItems.RawAccessToken] = jwt.EncodedToken;
            }
        };
    }
}
