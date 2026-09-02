namespace StubAuthorizationServer;

/// <summary>POST /connect/token — a minimal client_credentials grant. The real external AS
/// (an Okta Custom AS, or any other OAuth 2.1 authorization server) mediates a human end-user
/// through the full authorization_code + PKCE dance; this stub skips straight to token issuance so
/// integration tests can mint access tokens with arbitrary claims for validation testing.</summary>
public static class TokenEndpoint
{
    public static async Task<IResult> HandleAsync(HttpContext httpContext, StubAuthorizationServerState state)
    {
        var form = await httpContext.Request.ReadFormAsync();

        if (form["grant_type"].ToString() != "client_credentials")
        {
            return Results.BadRequest(new { error = "unsupported_grant_type" });
        }

        var baseUrl = DiscoveryEndpoint.BaseUrl(httpContext);
        var now = DateTimeOffset.UtcNow;

        var audience = FirstNonEmpty(form["aud"].ToString(), state.DefaultAudience);
        var subject = FirstNonEmpty(form["sub"].ToString(), state.DefaultSubject);
        var scope = FirstNonEmpty(form["scope"].ToString(), state.DefaultScope);
        var email = form["email"].ToString();
        var clientId = form["cid"].ToString();
        var lifetimeSeconds = int.TryParse(form["exp_seconds"].ToString(), out var overrideSeconds)
            ? overrideSeconds
            : state.DefaultLifetimeSeconds;

        var claims = new Dictionary<string, object?>
        {
            ["iss"] = baseUrl,
            ["aud"] = audience,
            ["sub"] = subject,
            ["iat"] = Jwt.ToUnixTimeSeconds(now),
            ["exp"] = Jwt.ToUnixTimeSeconds(now.AddSeconds(lifetimeSeconds)),
            ["scope"] = scope,
        };

        // Optional passthrough for RBAC identity-claim testing: RbacOptions.IdentityClaim
        // defaults to "email", which this stub otherwise never emits. Left out of the claim
        // set entirely (not even empty-string) when the caller doesn't ask for it, so every
        // existing token shape and test assertion is unaffected.
        if (!string.IsNullOrEmpty(email))
        {
            claims["email"] = email;
        }

        // Same opt-in shape as `email` above, for ClientIdRequirement testing. An Okta org
        // authorization server always emits `cid`; omitting it here by default keeps every existing
        // token shape byte-identical and lets a test assert the missing-claim denial path.
        if (!string.IsNullOrEmpty(clientId))
        {
            claims["cid"] = clientId;
        }

        var signingKey = state.SignWithWrongKey ? RSA.Create(2048) : SigningKey.Rsa;
        var accessToken = Jwt.CreateSigned(claims, signingKey, SigningKey.KeyId);

        var response = new Dictionary<string, object?>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = lifetimeSeconds,
            ["scope"] = scope,
        };

        return Results.Json(response);
    }

    private static string FirstNonEmpty(string value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;
}
