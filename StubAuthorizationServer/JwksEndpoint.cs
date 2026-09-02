namespace StubAuthorizationServer;

/// <summary>Mandatory, not optional — OpenIdConnectConfigurationValidator rejects a discovery
/// document with zero signing keys, so publishing discovery alone is not enough.</summary>
public static class JwksEndpoint
{
    public static IResult Handle()
    {
        var parameters = SigningKey.Rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = SigningKey.KeyId,
            ["n"] = Base64Url.Encode(parameters.Modulus!),
            ["e"] = Base64Url.Encode(parameters.Exponent!),
        };

        return Results.Json(new Dictionary<string, object?> { ["keys"] = new[] { jwk } });
    }
}
