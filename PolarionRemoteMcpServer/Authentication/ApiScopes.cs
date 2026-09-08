namespace PolarionRemoteMcpServer.Authentication;

/// <summary>
/// Defines the API scopes used for authorization.
/// </summary>
public static class ApiScopes
{
    /// <summary>
    /// Scope for read operations on Polarion data.
    /// </summary>
    public const string PolarionRead = "polarion:read";

    /// <summary>
    /// Scope for create operations on Polarion data (future use).
    /// </summary>
    public const string PolarionCreate = "polarion:create";

    /// <summary>
    /// Scope for update operations on Polarion data (future use).
    /// </summary>
    public const string PolarionUpdate = "polarion:update";

    /// <summary>
    /// Scope for delete operations on Polarion data (future use).
    /// </summary>
    public const string PolarionDelete = "polarion:delete";

    /// <summary>
    /// All available scopes for policy registration.
    /// </summary>
    public static readonly string[] All = new[]
    {
        PolarionRead,
        PolarionCreate,
        PolarionUpdate,
        PolarionDelete
    };

    /// <summary>
    /// Authorization policy name gating the MCP endpoint. Deliberately distinct from the REST
    /// scope policies of the same name above (e.g. <see cref="PolarionRead"/>) — those are pinned
    /// to the ApiKey scheme, whereas this one is checked via <c>RequireAssertion</c> against
    /// whatever scheme authenticated the caller (JwtBearer), so pinning a scheme here would break
    /// MCP OAuth discovery. Both still gate on the same <see cref="PolarionRead"/> scope value.
    /// </summary>
    public const string McpReadPolicy = "PolarionMcpRead";
}
