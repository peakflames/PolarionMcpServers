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
}
