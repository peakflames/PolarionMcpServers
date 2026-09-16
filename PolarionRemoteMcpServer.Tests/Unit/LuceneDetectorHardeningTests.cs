using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Containment tests for the plain search path.
///
/// The server scopes every search to the route project by AND-ing <c>project.id</c> onto
/// the caller's Lucene. These tests prove three escape vectors are rejected before a query
/// is built or sent:
///   1. a SQL:(...) filter smuggled through the search text (owned by the opt-in SQL tool);
///   2. an unbalanced parenthesis/quote that could re-associate the project.id suffix;
///   3. a non-identifier type/status value that could re-open Lucene grouping.
/// </summary>
public sealed class LuceneDetectorHardeningTests
{
    // --- ContainsSqlFilter ---------------------------------------------------

    [Theory]
    [InlineData("SQL:(SELECT item.C_PK FROM WORKITEM item)")]
    [InlineData("sql:(SELECT item.C_PK FROM WORKITEM item)")]
    [InlineData("SQL :(SELECT 1)")]
    [InlineData("customFieldA:subsection_of=PROJ* AND SQL:(SELECT item.C_PK FROM WORKITEM item)")]
    public void ContainsSqlFilter_DetectsEmbeddedSql(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeTrue(
            "SQL:(...) filters must be recognized so they can be blocked from the plain search path");
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("MySQL:database")]        // 'SQL' not at a token boundary
    [InlineData("NoSQL migration")]
    public void ContainsSqlFilter_DoesNotFlagOrdinaryQueries(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeFalse();
    }

    // --- HasBalancedLuceneGrouping -------------------------------------------

    [Theory]
    [InlineData("(a OR b) AND c")]
    [InlineData("\"a (b\"")]              // '(' inside a phrase is literal text
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("plain text")]
    public void HasBalancedLuceneGrouping_AcceptsBalanced(string query)
    {
        McpTools.HasBalancedLuceneGrouping(query).Should().BeTrue();
    }

    [Theory]
    [InlineData("x)) AND project.id:other OR ((y")]
    [InlineData("a) OR project.id:other OR (b")]
    [InlineData("(unclosed")]
    [InlineData("\"unterminated phrase")]
    public void HasBalancedLuceneGrouping_RejectsUnbalanced(string query)
    {
        McpTools.HasBalancedLuceneGrouping(query).Should().BeFalse();
    }

    // --- IsSafeIdentifier ----------------------------------------------------

    [Theory]
    [InlineData("requirement")]
    [InlineData("testCase")]
    [InlineData("in-progress")]
    [InlineData("system.subsystem_req")]
    public void IsSafeIdentifier_AcceptsIdentifiers(string value)
    {
        McpTools.IsSafeIdentifier(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("req) OR project.id:other OR (x")]
    [InlineData("a b")]
    [InlineData("type:x")]
    [InlineData("\"quoted\"")]
    public void IsSafeIdentifier_RejectsNonIdentifiers(string value)
    {
        McpTools.IsSafeIdentifier(value).Should().BeFalse();
    }

    // --- Tool-level rejection (no transport contact) -------------------------

    [Fact]
    public async Task SearchWorkitems_RejectsSqlSmuggledThroughSearchText()
    {
        // A null service provider proves no Polarion client is ever created for a rejected
        // query: rejection happens before the service scope is opened.
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("SQL:(SELECT item.C_PK FROM WORKITEM item)");

        result.Should().StartWith("ERROR: (105)");
        result.Should().Contain("search_workitems_sql");
    }

    [Fact]
    public async Task SearchWorkitems_RejectsUnbalancedGrouping()
    {
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("a) OR project.id:other OR (b");

        result.Should().StartWith("ERROR: (106)");
    }

    [Fact]
    public async Task SearchWorkitems_RejectsNonIdentifierItemType()
    {
        var tools = new McpTools(serviceProvider: null!);

        var result = await tools.SearchWorkitems("timeout", itemTypes: "req) OR project.id:other OR (x");

        result.Should().StartWith("ERROR: (107)");
    }
}
