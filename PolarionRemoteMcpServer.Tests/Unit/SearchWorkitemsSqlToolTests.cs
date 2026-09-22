using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using Polarion.Generated.Tracker;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tool-contract tests for the opt-in search_workitems_sql tool.
///
/// Rejection-path tests use a null service provider: any transport contact would throw, so a
/// returned guard error proves the tool never created a client. The transport test uses a strict
/// Moq client to prove the tool intersects with the project (never passes includeAllProjects) and
/// wraps the SQL in a single outer group.
/// </summary>
public sealed class SearchWorkitemsSqlToolTests
{
    private const string ValidSql = "SELECT item.C_PK FROM WORKITEM item";

    private static McpSqlTools NewToolWithNoTransport() => new(serviceProvider: null!);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UPDATE WORKITEM SET C_STATUS = 'x'")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item; DROP TABLE WORKITEM")]
    [InlineData("SELECT item.C_PK FROM WORKITEM item -- comment")]
    [InlineData("SELECT 1")]
    public async Task SearchWorkitemsSql_RejectsUnsafeSql_BeforeTransport(string sql)
    {
        (await NewToolWithNoTransport().SearchWorkitemsSql(sql)).Should().StartWith("ERROR: (1050)");
    }

    [Fact]
    public async Task SearchWorkitemsSql_RejectsSqlSmuggledInLuceneFilter()
    {
        var result = await NewToolWithNoTransport().SearchWorkitemsSql(
            ValidSql, luceneFilter: "SQL:(SELECT item.C_PK FROM WORKITEM item)");

        result.Should().StartWith("ERROR: (1051)");
    }

    [Fact]
    public async Task SearchWorkitemsSql_RejectsUnbalancedLuceneFilter()
    {
        var result = await NewToolWithNoTransport().SearchWorkitemsSql(
            ValidSql, luceneFilter: "x)) OR ((y");

        result.Should().StartWith("ERROR: (1055)");
    }

    [Fact]
    public async Task SearchWorkitemsSql_RejectsInvalidSortBy_BeforeTransport()
    {
        var result = await NewToolWithNoTransport().SearchWorkitemsSql(
            ValidSql, luceneFilter: null, sortBy: "not-a-field");

        result.Should().StartWith("ERROR: (1052)");
    }

    // --- Wrapper shape -------------------------------------------------------

    [Fact]
    public void BuildSqlLuceneQuery_WrapsInSingleOuterGroup()
    {
        McpSqlTools.BuildSqlLuceneQuery(ValidSql, null)
            .Should().Be("(SQL:(SELECT item.C_PK FROM WORKITEM item))");
    }

    [Fact]
    public void BuildSqlLuceneQuery_AndsSupplementaryLuceneFilterInsideOuterGroup()
    {
        McpSqlTools.BuildSqlLuceneQuery(ValidSql, "linkedWorkItems:parent_of=PROJ*")
            .Should().Be("(SQL:(SELECT item.C_PK FROM WORKITEM item) AND (linkedWorkItems:parent_of=PROJ*))");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("linkedWorkItems:parent_of=PROJ*")]
    public void BuildSqlLuceneQuery_OuterGroupNeverClosesBeforeEnd(string? luceneFilter)
    {
        // Prefix-depth invariant: parenthesis depth stays >= 1 everywhere except the final char,
        // so Polarion's appended " AND project.id:X" lands inside the outer group and cannot be
        // re-associated with an attacker-supplied OR.
        var query = McpSqlTools.BuildSqlLuceneQuery(ValidSql, luceneFilter);

        var depth = 0;
        for (var i = 0; i < query.Length; i++)
        {
            if (query[i] == '(') depth++;
            else if (query[i] == ')') depth--;

            if (i < query.Length - 1)
            {
                depth.Should().BeGreaterThanOrEqualTo(1, "the outer group must stay open until the final character");
            }
        }

        depth.Should().Be(0, "the outer group must close exactly at the end");
    }

    // --- Timeout error paths -------------------------------------------------

    [Theory]
    [InlineData("The request channel timed out attempting to send after 00:01:00")]
    [InlineData("Operation timed out")]
    [InlineData("SendTimeout exceeded")]
    public async Task SearchWorkitemsSql_ReturnsCode1056_OnTimeoutInFailedResult(string timeoutMessage)
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail<WorkItem[]>(timeoutMessage));

        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        var tool = new McpSqlTools(services.BuildServiceProvider());

        var result = await tool.SearchWorkitemsSql(ValidSql);

        result.Should().StartWith("ERROR: (1056)");
        result.Should().Contain("selective WHERE");
    }

    [Fact]
    public async Task SearchWorkitemsSql_ReturnsCode1056_OnTimeoutException()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ThrowsAsync(new TimeoutException("The operation has timed out."));

        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        var tool = new McpSqlTools(services.BuildServiceProvider());

        var result = await tool.SearchWorkitemsSql(ValidSql);

        result.Should().StartWith("ERROR: (1056)");
    }

    // --- Transport contract --------------------------------------------------

    [Fact]
    public async Task SearchWorkitemsSql_IntersectsProject_AndWrapsSql()
    {
        string? capturedQuery = null;
        bool? capturedIncludeAllProjects = null;

        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .Callback<string, string, List<string>, bool>((q, _, _, includeAll) =>
            {
                capturedQuery = q;
                capturedIncludeAllProjects = includeAll;
            })
            .ReturnsAsync(Result.Ok(new[] { new WorkItem { id = "STR-1" } }));

        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        var tool = new McpSqlTools(services.BuildServiceProvider());

        var result = await tool.SearchWorkitemsSql(ValidSql);

        result.Should().NotStartWith("ERROR");
        capturedIncludeAllProjects.Should().BeFalse(
            "the tool must let Polarion keep the project.id filter so results stay in the route project");
        capturedQuery.Should().Be("(SQL:(SELECT item.C_PK FROM WORKITEM item))");
    }
}
