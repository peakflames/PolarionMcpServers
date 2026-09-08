namespace StubAuthorizationServer;

/// <summary>Per-instance mutable stub configuration — registered as a singleton in the stub's own
/// DI container, so each StubAuthorizationServerApp instance (one per test) has its own
/// independent state.</summary>
public sealed class StubAuthorizationServerState
{
    public string DefaultSubject { get; set; } = "stub-subject";

    public string DefaultAudience { get; set; } = "https://polarion-mcp.example.invalid/starlight/mcp";

    public string DefaultScope { get; set; } = "polarion:read";

    public int DefaultLifetimeSeconds { get; set; } = 300;

    /// <summary>Fault injection: sign the access token with a throwaway key instead of
    /// SigningKey.Rsa, simulating a token signed by a key the resource server doesn't trust.</summary>
    public bool SignWithWrongKey { get; set; }

    // ---- /userinfo -------------------------------------------------------------------------

    /// <summary>Null means the endpoint omits `email` entirely — the "no email at the IdP" case,
    /// which is a definitive answer rather than a failure.</summary>
    public string? UserInfoEmail { get; set; } = "stub.user@example.invalid";

    public bool UserInfoEmailVerified { get; set; } = true;

    /// <summary>Fault injection: 429. Kept separate from <see cref="UserInfoStatusCode"/> because the
    /// resource server treats 429 differently from every other failure — it must never be cached and
    /// must be alertable.</summary>
    public bool UserInfoRateLimited { get; set; }

    public int? UserInfoStatusCode { get; set; }

    public bool UserInfoMalformed { get; set; }

    /// <summary>A public field, not a property — <see cref="Interlocked.Increment(ref int)"/> needs a
    /// ref to storage, and the endpoint handler is hit concurrently by design in the cache tests.</summary>
    public int UserInfoRequestCount;
}
