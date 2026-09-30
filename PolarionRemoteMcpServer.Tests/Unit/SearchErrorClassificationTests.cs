using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tests for classifying Polarion search errors. Polarion echoes the submitted query in its
/// error text, so classification must run on the redacted message only.
/// </summary>
public sealed class SearchErrorClassificationTests
{
    [Fact]
    public void ParseError_OnQueryContainingTimeout_IsNotClassifiedAsTimeout()
    {
        var query = "title:timeout AND (";
        var raw = $"Error with Message='Cannot parse '{query}': Encountered <EOF>'";

        var redacted = McpTools.RedactQueryEcho(raw, query);

        McpTools.IsTimeoutError(redacted).Should().BeFalse();
        redacted.Should().Contain("parse").And.NotContain("timeout");
    }

    [Theory]
    [InlineData("The request channel timed out while waiting for a reply after 00:01:00.")]
    [InlineData("System.TimeoutException: The HTTP request was aborted")]
    public void WcfTimeoutMessages_AreClassifiedAsTimeout(string raw)
    {
        McpTools.IsTimeoutError(McpTools.RedactQueryEcho(raw, "title:foo")).Should().BeTrue();
    }

    [Fact]
    public void SyntaxWordInQuery_DoesNotMakeBareQueryFailedLookLikeParseError()
    {
        var query = "SQL:(SELECT item.C_PK FROM WORKITEM item WHERE item.C_TITLE LIKE '%syntax%')";
        var raw = $"Error with Message='Query failed: {query}'";

        McpTools.IsBareQueryFailed(McpTools.RedactQueryEcho(raw, query)).Should().BeTrue();
    }
}
