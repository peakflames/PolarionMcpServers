namespace PolarionRemoteMcpServer.Credentials;

/// <summary>
/// Deliberately `sealed class`, never `record` — same rationale as
/// <see cref="Auth.McpAuthOptions"/>: a record's generated <c>ToString()</c> would print every
/// property, and <see cref="BrokerApiKey"/> here is a real secret.
/// </summary>
public sealed class CredentialsOptions
{
    public const string SectionName = "Credentials";
    public const string SharedMode = "Shared";
    public const string HttpBrokerMode = "HttpBroker";

    /// <summary><see cref="SharedMode"/> (default) or <see cref="HttpBrokerMode"/>. Any other value
    /// fails validation at startup.</summary>
    public string Mode { get; set; } = SharedMode;

    /// <summary>Required when <see cref="Mode"/> is <see cref="HttpBrokerMode"/>. The external
    /// credential broker's resolve endpoint — a separate component this repo does not
    /// implement.</summary>
    public string? BrokerUrl { get; set; }

    /// <summary>Required when <see cref="Mode"/> is <see cref="HttpBrokerMode"/>. This server's own
    /// credential for calling the broker — never the caller's JWT, which is never forwarded.</summary>
    public string? BrokerApiKey { get; set; }

    public int BrokerTimeoutSeconds { get; set; } = 10;
}
