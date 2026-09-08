using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Accumulates every validation failure into one result. Returns Success immediately when
/// McpAuth is disabled so a malformed McpAuth section can never break a server with the feature off.
/// </summary>
public sealed class McpAuthOptionsValidator : IValidateOptions<McpAuthOptions>
{
    private readonly IHostEnvironment _environment;

    public McpAuthOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, McpAuthOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        var isDevelopment = _environment.IsDevelopment();

        ValidateIssuer(options, isDevelopment, failures);
        ValidateResourceUri(options, failures);
        ValidateMetadataAddress(options, isDevelopment, failures);
        ValidateClockSkew(options, failures);
        ValidateClientBinding(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuer(McpAuthOptions options, bool isDevelopment, List<string> failures)
    {
        if (!TryParseAbsoluteUri(options.Issuer, out var issuerUri))
        {
            failures.Add("Issuer must be an absolute URI.");
            return;
        }

        if (!IsAllowedScheme(issuerUri, isDevelopment))
        {
            failures.Add("Issuer must use https (http is only allowed in the Development environment).");
        }

        // Unlike an embedded issuer, an external authorization server's issuer legitimately
        // has a path (e.g. https://issuer.example.invalid/oauth2/<asid>), so a path is not
        // itself an error.
        if (!string.IsNullOrEmpty(issuerUri.Fragment))
        {
            failures.Add("Issuer must not contain a fragment.");
        }
    }

    private static void ValidateResourceUri(McpAuthOptions options, List<string> failures)
    {
        if (!TryParseAbsoluteUri(options.ResourceUri, out var resourceUri))
        {
            failures.Add("ResourceUri must be an absolute URI.");
            return;
        }

        if (!string.IsNullOrEmpty(resourceUri.Fragment))
        {
            failures.Add("ResourceUri must not contain a fragment.");
        }

        // ResourceUri is now the base URL a per-alias resource is derived from (see
        // McpAuthOptions.ResourceFor). A trailing /mcp is the old single-alias shape and would
        // double up into .../mcp/{alias}/mcp once ResourceFor appends the alias and /mcp.
        if (resourceUri.AbsolutePath.TrimEnd('/').EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("ResourceUri must be a base URL with no alias and no trailing /mcp — " +
                "the per-alias resource (…/{alias}/mcp) is derived automatically.");
        }
    }

    private static void ValidateClientBinding(McpAuthOptions options, List<string> failures)
    {
        if (!options.ValidateAudience && (options.AllowedClientIds is null || options.AllowedClientIds.Count == 0))
        {
            failures.Add("AllowedClientIds must not be empty when ValidateAudience is false — " +
                "otherwise a token issued to any client for any resource would be accepted.");
            return;
        }

        if (options.AllowedClientIds is null)
            return;

        foreach (var clientId in options.AllowedClientIds)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                failures.Add("AllowedClientIds must not contain blank entries.");
                break;
            }
        }
    }

    private static void ValidateMetadataAddress(McpAuthOptions options, bool isDevelopment, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.MetadataAddress))
            return;

        if (!TryParseAbsoluteUri(options.MetadataAddress, out var metadataUri))
        {
            failures.Add("MetadataAddress must be an absolute URI.");
            return;
        }

        if (!IsAllowedScheme(metadataUri, isDevelopment))
        {
            failures.Add("MetadataAddress must use https (http is only allowed in the Development environment).");
        }
    }

    private static void ValidateClockSkew(McpAuthOptions options, List<string> failures)
    {
        if (options.ClockSkewSeconds < 0 || options.ClockSkewSeconds > 300)
            failures.Add("ClockSkewSeconds must be between 0 and 300.");
    }

    private static bool TryParseAbsoluteUri(string value, out Uri uri)
    {
        var ok = Uri.TryCreate(value, UriKind.Absolute, out var parsed);
        uri = parsed ?? new Uri("about:blank");
        return ok;
    }

    private static bool IsAllowedScheme(Uri uri, bool isDevelopment)
    {
        if (string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
            return true;

        return isDevelopment && string.Equals(uri.Scheme, "http", StringComparison.Ordinal);
    }
}
