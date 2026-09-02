namespace StubAuthorizationServer;

/// <summary>
/// Hand-rolled RS256 JWS — deliberately not Microsoft.IdentityModel.JsonWebTokens. This keeps the
/// stub at zero PackageReference (no NuGet unification fight against the IdentityModel graph
/// already in-process on the SUT side) and makes fault injection (alg=none, wrong key, wrong iss,
/// wrong aud, expired) a one-line payload edit instead of a fight with a handler that refuses to
/// emit invalid tokens.
/// </summary>
public static class Jwt
{
    public static string CreateSigned(IReadOnlyDictionary<string, object?> claims, RSA signingKey, string keyId)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = keyId,
        };

        var headerSegment = Base64Url.Encode(JsonSerializer.Serialize(header));
        var payloadSegment = Base64Url.Encode(JsonSerializer.Serialize(claims));
        var signingInput = $"{headerSegment}.{payloadSegment}";

        var signature = signingKey.SignData(
            Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    /// <summary>Fault injection for the "alg: none" attack: no signature segment, and the header
    /// declares alg=none instead of RS256.</summary>
    public static string CreateUnsigned(IReadOnlyDictionary<string, object?> claims)
    {
        var header = new Dictionary<string, object?> { ["alg"] = "none", ["typ"] = "JWT" };
        var headerSegment = Base64Url.Encode(JsonSerializer.Serialize(header));
        var payloadSegment = Base64Url.Encode(JsonSerializer.Serialize(claims));
        return $"{headerSegment}.{payloadSegment}.";
    }

    public static long ToUnixTimeSeconds(DateTimeOffset value) => value.ToUnixTimeSeconds();
}
