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
    // Adjacent-operator forms — previously missed by the (^|\s|\() anchor.
    // All of these must be detected; passing them to the plain search would bypass
    // the SqlQueryGuard and execute raw SQL through Polarion's credential.
    [InlineData("-SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene exclusion prefix
    [InlineData("+SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene required prefix
    [InlineData("!SQL:(SELECT item.C_PK FROM WORKITEM item)")]   // Lucene NOT prefix
    [InlineData("x:SQL:(SELECT item.C_PK FROM WORKITEM item)")] // colon-adjacent (field:SQL:)
    public void ContainsSqlFilter_DetectsEmbeddedSql(string query)
    {
        McpTools.ContainsSqlFilter(query).Should().BeTrue(
            "SQL:(...) filters must be recognized so they can be blocked from the plain search path");
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    [InlineData("MySQL:database")]        // 'SQL' not at a token boundary — 'y' is an identifier char
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
    // Escaped-quote bypass: the attacker uses \" (Lucene backslash-escape for a literal ")
    // to make the naive toggler think the ) is "inside a phrase" when Lucene sees it as a
    // real close-paren.  Input: \")\" OR project.id:other OR \"(\"
    // Without the fix the checker returned balanced=true; Lucene sees ) at depth=-1.
    [InlineData("\\\")\\\" OR project.id:other OR \\\"(\\\"")]
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

    // --- IsSafeForPolarionPathParam ------------------------------------------

    [Theory]
    [InlineData("MySpace")]
    [InlineData("My Space - Section")]          // example space name with spaces and dashes
    [InlineData("my_doc_id")]
    [InlineData("System Requirements")]
    public void IsSafeForPolarionPathParam_AcceptsValidNames(string value)
    {
        McpTools.IsSafeForPolarionPathParam(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("' OR '1'='1")]            // SQL injection via single-quote
    [InlineData("--injection")]            // SQL line-comment token
    [InlineData("foo; DROP TABLE WORKITEM")] // statement terminator
    [InlineData("x /* comment */")]        // block-comment open
    [InlineData("x */ y")]                 // block-comment close
    [InlineData(null)]
    [InlineData("")]
    public void IsSafeForPolarionPathParam_RejectsInjectionAttempts(string? value)
    {
        McpTools.IsSafeForPolarionPathParam(value).Should().BeFalse();
    }
}
