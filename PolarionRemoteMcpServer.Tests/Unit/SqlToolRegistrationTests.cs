using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Verifies the opt-in switch: search_workitems_sql is registered on the MCP server if and only
/// if SqlQueryTool:Enabled is true.
/// </summary>
public sealed class SqlToolRegistrationTests
{
    private static IReadOnlyList<string> RegisteredToolNames(string? sqlEnabled)
    {
        var factory = new PolarionMcpServerFactory()
            .WithProject("starlight", "Starlight_Main", isDefault: true);

        if (sqlEnabled is not null)
        {
            factory.With("SqlQueryTool:Enabled", sqlEnabled);
        }

        using (factory)
        {
            return factory.Services.GetServices<McpServerTool>()
                .Select(t => t.ProtocolTool.Name)
                .ToList();
        }
    }

    [Fact]
    public void SqlTool_IsRegistered_WhenFlagTrue()
    {
        RegisteredToolNames("true").Should().Contain("search_workitems_sql");
    }

    [Fact]
    public void SqlTool_IsNotRegistered_WhenFlagFalse()
    {
        RegisteredToolNames("false").Should().NotContain("search_workitems_sql");
    }

    [Fact]
    public void SqlTool_IsNotRegistered_ByDefault()
    {
        RegisteredToolNames(null).Should().NotContain("search_workitems_sql");
    }

    [Fact]
    public void PlainSearchTool_IsAlwaysRegistered()
    {
        RegisteredToolNames("false").Should().Contain("search_workitems");
    }
}
