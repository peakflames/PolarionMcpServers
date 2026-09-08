namespace StubAuthorizationServer;

public static class DiscoveryEndpoint
{
    /// <summary>Computed from the request, not a fixed config value — the stub runs both under a
    /// TestServer pinned to a fixed host and as a real standalone process on a real port for
    /// manual smoke testing, and the issuer must match whichever one actually answered.</summary>
    public static string BaseUrl(HttpContext httpContext) => $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

    public static IResult Handle(HttpContext httpContext)
    {
        var baseUrl = BaseUrl(httpContext);

        var document = new Dictionary<string, object?>
        {
            ["issuer"] = baseUrl,
            ["token_endpoint"] = $"{baseUrl}/connect/token",
            ["jwks_uri"] = $"{baseUrl}/jwks",
            ["grant_types_supported"] = new[] { "client_credentials" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
            ["response_types_supported"] = Array.Empty<string>(),
            ["subject_types_supported"] = new[] { "public" },
        };

        return Results.Json(document);
    }
}
