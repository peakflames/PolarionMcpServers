using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

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

    public ConfigureJwtBearerOptions(IOptions<McpAuthOptions> authOptions)
    {
        _authOptions = authOptions;
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
        tvp.ValidateAudience = true;
        tvp.ValidateLifetime = true;
        tvp.ValidateIssuerSigningKey = true;
        tvp.ValidIssuer = auth.Issuer;
        tvp.ValidAudience = auth.ResourceUri;
        tvp.ClockSkew = TimeSpan.FromSeconds(auth.ClockSkewSeconds);

        // Pinned — blocks alg-confusion attacks and "none".
        tvp.ValidAlgorithms = ["RS256"];
    }
}
