using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Gives <see cref="McpAuthOptions.ScopesSupported"/> replace-instead-of-append semantics.
///
/// <c>ConfigurationBinder</c> appends to a collection that already holds items, so a bound
/// <c>McpAuth:ScopesSupported</c> can only ever *grow* past its C# default — measured, not assumed:
/// with a default of <c>[polarion:read]</c>, binding <c>McpAuth:ScopesSupported:0=openid</c> yields
/// <c>[polarion:read, openid]</c>. Switching the property to <c>string[]</c> does not help; arrays
/// append too, measured the same way.
///
/// That append is a live failure against an Okta *org* authorization server. The scopes a resource
/// server advertises in its RFC 9728 metadata are the scopes a conforming client then requests, so
/// a stray <c>polarion:read</c> makes the authorization request fail wholesale with
/// <c>invalid_scope</c> — the org authorization server has no such scope and cannot be given one.
/// Deployments there must advertise exactly the OIDC scopes the tenant can actually grant.
///
/// Runs as a post-configure so it lands after the section bind. When the key is absent from
/// configuration the C# default is left untouched, which keeps the spec-conforming path byte-identical.
/// </summary>
public sealed class ReplaceConfiguredScopesSupported : IPostConfigureOptions<McpAuthOptions>
{
    private const string ScopesKey = $"{McpAuthOptions.SectionName}:{nameof(McpAuthOptions.ScopesSupported)}";

    private readonly IConfiguration _configuration;

    public ReplaceConfiguredScopesSupported(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void PostConfigure(string? name, McpAuthOptions options)
    {
        var section = _configuration.GetSection(ScopesKey);
        if (!section.Exists())
            return;

        // Blank entries are dropped rather than advertised: RFC 6749's scope-token is 1*NQCHAR, so
        // a blank is never a scope a client could legitimately request. Configuring a single blank
        // entry is also the only way to express "advertise nothing" through an environment
        // variable, which cannot represent an empty array — though McpAuth:AdvertiseScopes=false
        // says the same thing far more legibly.
        options.ScopesSupported = section.GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }
}
