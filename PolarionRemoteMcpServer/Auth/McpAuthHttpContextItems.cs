namespace PolarionRemoteMcpServer.Auth;

public static class McpAuthHttpContextItems
{
    /// <summary>
    /// The caller's raw bearer token, stashed by <see cref="ConfigureJwtBearerOptions"/>'s
    /// <c>OnTokenValidated</c> for the one consumer that genuinely needs it: an OIDC
    /// <c>/userinfo</c> call made on the caller's behalf.
    ///
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>, not a DI-scoped holder — the MCP
    /// SDK dispatches a tool call through a nested service scope, so a scoped service resolved
    /// inside the filter is not the same instance the authentication handler would have written to.
    /// Items lives on the HttpContext itself and is therefore scope-independent.
    ///
    /// Never read this into an audit record, a log message, or an exception message.
    /// </summary>
    public const string RawAccessToken = "PolarionMcp.RawAccessToken";
}
