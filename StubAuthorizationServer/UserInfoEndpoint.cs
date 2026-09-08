namespace StubAuthorizationServer;

/// <summary>
/// GET /oauth2/v1/userinfo — the OIDC endpoint <c>OktaUserInfoEmailSource</c> calls with the caller's
/// own bearer token. The path is Okta's literal org-authorization-server path, not this stub's
/// invention: the resource server derives it from <c>McpAuth:Issuer</c> rather than from discovery, so
/// the stub must answer on exactly that path for the integration to be a real test of the URL
/// construction as well as the parsing.
///
/// Counts requests so a test can prove the cache holds — one token should cost exactly one call no
/// matter how many tool calls ride on it.
/// </summary>
public static class UserInfoEndpoint
{
    public static IResult Handle(HttpContext httpContext, StubAuthorizationServerState state)
    {
        Interlocked.Increment(ref state.UserInfoRequestCount);

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            || authorization.Length <= "Bearer ".Length)
        {
            return Results.Json(new { error = "invalid_token" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (state.UserInfoRateLimited)
        {
            httpContext.Response.Headers.RetryAfter = "30";
            return Results.Json(new { errorCode = "E0000047" }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (state.UserInfoStatusCode is { } forced && forced != StatusCodes.Status200OK)
        {
            return Results.Json(new { errorCode = "forced" }, statusCode: forced);
        }

        if (state.UserInfoMalformed)
        {
            return Results.Text("{ not json at all", "application/json");
        }

        var payload = new Dictionary<string, object?>
        {
            ["sub"] = state.DefaultSubject,
            ["email_verified"] = state.UserInfoEmailVerified,
        };

        // Omitted entirely (not empty-string) when the test wants the no-email-at-the-IdP case.
        if (state.UserInfoEmail is not null)
        {
            payload["email"] = state.UserInfoEmail;
        }

        return Results.Json(payload);
    }
}
