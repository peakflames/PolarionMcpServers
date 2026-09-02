using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using PolarionMcpTools;

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
///
/// ResourceMetadata.Resource is left null here: this MCP server serves several project aliases
/// under one host (/{alias}/mcp), each of which must advertise its own resource identifier, not a
/// single shared one. ModelContextProtocol.AspNetCore.Authentication.McpAuthenticationHandler
/// derives a per-request resource from the well-known request path
/// (.../oauth-protected-resource/{alias}/mcp) and only falls back to it when Resource is null
/// (ProtectedResourceMetadata.Clone: `Resource = Resource ?? derivedResource`) — measured against
/// SDK 2.1.0's decompiled source. OnResourceMetadataRequest below still overwrites Resource
/// explicitly with McpAuthOptions.ResourceFor(alias) so the advertised value is correct by
/// construction rather than by coincidentally matching the SDK's derivation, and rejects any alias
/// that is not a configured Polarion project.
/// </summary>
public sealed class ConfigureMcpAuthenticationOptions : IConfigureNamedOptions<McpAuthenticationOptions>
{
    private const string WellKnownPrefix = "/.well-known/oauth-protected-resource";

    private readonly IOptions<McpAuthOptions> _authOptions;
    private readonly List<PolarionProjectConfig> _projects;

    public ConfigureMcpAuthenticationOptions(IOptions<McpAuthOptions> authOptions, List<PolarionProjectConfig> projects)
    {
        _authOptions = authOptions;
        _projects = projects;
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
            Resource = null,
            AuthorizationServers = new List<string> { auth.Issuer },
            BearerMethodsSupported = new List<string> { "header" },
            // AdvertiseScopes=false publishes an empty list. The configured scopes themselves get
            // replace-not-append semantics upstream in ReplaceConfiguredScopesSupported. Blank
            // entries are dropped at both layers: RFC 6749's scope-token is 1*NQCHAR, so a blank is
            // never a scope a client could legitimately request, and advertising one would invite
            // exactly that request.
            ScopesSupported = auth.AdvertiseScopes
                ? auth.ScopesSupported.Where(scope => !string.IsNullOrWhiteSpace(scope)).ToList()
                : [],
        };

        options.Events ??= new McpAuthenticationEvents();
        options.Events.OnResourceMetadataRequest = context =>
        {
            var alias = ExtractAlias(context.Request.Path.Value ?? string.Empty);

            if (alias is null || !_projects.Any(p => string.Equals(p.ProjectUrlAlias, alias, StringComparison.Ordinal)))
            {
                // Not Handled, not Failed — SkipHandler() leaves the request for the rest of the
                // pipeline, which has no route for an unknown alias and answers 404, the same
                // outcome as calling any other unmapped /{alias}/mcp endpoint.
                context.SkipHandler();
                return Task.CompletedTask;
            }

            if (context.ResourceMetadata is not null)
            {
                context.ResourceMetadata.Resource = auth.ResourceFor(alias);
            }

            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// The well-known metadata request never runs through the "{projectId}/mcp" route (it is
    /// answered by McpAuthenticationHandler directly), so Request.RouteValues has no "projectId" —
    /// the alias has to be parsed out of the literal request path instead, e.g.
    /// "/.well-known/oauth-protected-resource/starlight/mcp" -&gt; "starlight".
    /// </summary>
    private static string? ExtractAlias(string requestPath)
    {
        var remaining = requestPath.StartsWith(WellKnownPrefix, StringComparison.OrdinalIgnoreCase)
            ? requestPath[WellKnownPrefix.Length..]
            : requestPath;

        var segments = remaining.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || !string.Equals(segments[1], "mcp", StringComparison.OrdinalIgnoreCase))
            return null;

        return segments[0];
    }
}
