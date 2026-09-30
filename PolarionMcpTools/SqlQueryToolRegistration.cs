namespace PolarionMcpTools;

/// <summary>
/// Opt-in registration for the SQL query tool. The tool is off unless the operator
/// sets <c>SqlQueryTool:Enabled=true</c>, so a deployment that cannot accept its residual (a
/// yes/no oracle about other projects' data via heavy joins) simply leaves it disabled.
/// </summary>
public static class SqlQueryToolRegistration
{
    /// <summary>Configuration key that gates registration of <see cref="McpSqlTools"/>.</summary>
    public const string EnabledKey = "SqlQueryTool:Enabled";

    /// <summary>
    /// Registers <see cref="McpSqlTools"/> on the MCP server only when <see cref="EnabledKey"/>
    /// is <c>true</c>. Returns whether the tool was registered so the caller can log the state.
    /// </summary>
    public static bool AddSqlQueryTool(this IMcpServerBuilder mcpBuilder, IConfiguration configuration)
    {
        var enabled = configuration.GetValue(EnabledKey, false);
        if (enabled)
        {
            mcpBuilder.WithTools<McpSqlTools>();
        }

        return enabled;
    }
}
