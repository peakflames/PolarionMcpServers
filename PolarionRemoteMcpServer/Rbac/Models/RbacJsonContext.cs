using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Rbac.Models;

/// <summary>
/// The subset of an OIDC <c>/userinfo</c> response this server reads. Only two fields,
/// deliberately: anything else would be profile data we have no reason to hold, and
/// <c>email_verified</c> is non-optional here because an unverified address is an unauthenticated
/// assertion.
/// </summary>
public sealed class OidcUserInfoResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }
}

/// <summary>
/// Source-generated, trim-safe JSON context for RBAC's own <c>/userinfo</c> call — kept separate
/// from <c>PolarionRestApiJsonContext</c> so this server's RBAC types never leak into the Polarion
/// REST API's serializer options.
/// </summary>
[JsonSerializable(typeof(OidcUserInfoResponse))]
public partial class RbacJsonContext : JsonSerializerContext
{
}
