namespace PolarionRemoteMcpServer.Credentials;

/// <summary>
/// A password is the only kind any shipped resolver can produce today — the upstream
/// <c>Polarion.PolarionClientConfiguration</c> from the PolarionApiClient package has no token
/// field to apply an <see cref="AccessToken"/> credential to. The member exists now so the later
/// PAT-passthrough phase (once the Polarion server is upgraded) is a resolver change, not another
/// enum value threaded through every switch that already handles this type.
/// </summary>
public enum PolarionCredentialKind
{
    Password,
    AccessToken,
}

/// <summary>
/// What an <see cref="IUpstreamCredentialResolver"/> hands back for one request's upstream Polarion
/// login. Deliberately `sealed class`, never `record` — same rationale as
/// <see cref="Auth.McpAuthOptions"/>: a record's generated <c>ToString()</c> would put
/// <see cref="Secret"/> into any accidental <c>{@credential}</c> log call.
/// </summary>
public sealed class PolarionSessionCredential
{
    public required PolarionCredentialKind Kind { get; init; }
    public required string Username { get; init; }

    /// <summary>The password or access token value, depending on <see cref="Kind"/>. Never log this.</summary>
    public required string Secret { get; init; }

    public override string ToString() =>
        $"PolarionSessionCredential {{ Kind = {Kind}, Username = {Username}, Secret = [REDACTED] }}";
}
