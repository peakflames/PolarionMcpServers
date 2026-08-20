using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// ResourceMetadataUri is deliberately left untouched (default null) so the SDK auto-serves
/// /.well-known/oauth-protected-resource/{alias}/mcp itself rather than us hand-rolling that
/// endpoint. jwks_uri is intentionally omitted from ResourceMetadata — RFC 9728's jwks_uri means
/// resource-response signing, not token signing, and it MUST be https, which would break
/// localhost dev.
///
/// Implements IConfigureNamedOptions, not plain IConfigureOptions — see ConfigureJwtBearerOptions
/// for why: a plain IConfigureOptions&lt;T&gt; is only invoked for the default-named ("") options
/// instance, and the Mcp scheme's options are requested under its own scheme name.
/// </summary>
public sealed class ConfigureMcpAuthenticationOptions : IConfigureNamedOptions<McpAuthenticationOptions>
{
    private readonly IOptions<McpAuthOptions> _authOptions;

    public ConfigureMcpAuthenticationOptions(IOptions<McpAuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    public void Configure(string? name, McpAuthenticationOptions options)
    {
        if (!string.Equals(name, McpAuthenticationDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        Configure(options);
    }

    public void Configure(McpAuthenticationOptions options)
    {
        var auth = _authOptions.Value;

        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = auth.ResourceUri,
            AuthorizationServers = new List<string> { auth.Issuer },
            BearerMethodsSupported = new List<string> { "header" },
            ScopesSupported = new List<string>(auth.ScopesSupported),
        };
    }
}
