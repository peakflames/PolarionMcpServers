using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// REST-API parity tests for the raw-Lucene passthrough.
///
/// The REST endpoint GET /polarion/rest/v1/projects/{projectId}/workitems previously kept
/// its own private BuildLuceneQuery/BuildTextSearchQuery that had not received the passthrough
/// change, so the REST API mangled raw Lucene while the MCP tool passed it through. The
/// endpoint now delegates to the shared <see cref="McpTools.BuildLuceneQuery"/>; these tests
/// exercise that exact method to prove the two paths produce identical output.
/// </summary>
public sealed class RestLuceneParityTests
{
    [Theory]
    [InlineData("category.KEY:MyCategory")]
    [InlineData("type:requirement")]
    [InlineData("HAS_VALUE:rationale")]
    public void RestBuilder_FieldScopedFilter_IsPassedThroughVerbatim(string query)
    {
        McpTools.BuildLuceneQuery(query, itemTypes: null, statusFilter: null)
            .Should().Be($"({query})",
                "the REST API must pass field-scoped Lucene through verbatim, exactly like the MCP tool");
    }

    [Theory]
    [InlineData("HVBIT AND timeout")]
    [InlineData("HVBIT OR timeout")]
    [InlineData("NOT HAS_VALUE:rationale")]
    [InlineData("category.KEY:MyCategory AND (timeout)")]
    public void RestBuilder_BooleanAndParenComposition_IsPassedThroughVerbatim(string query)
    {
        McpTools.BuildLuceneQuery(query, itemTypes: null, statusFilter: null)
            .Should().Be($"({query})",
                "boolean/parenthesized Lucene must be preserved through the REST path");
    }

    [Fact]
    public void RestBuilder_TwoSimpleTerms_StillBecomeOrGroup()
    {
        McpTools.BuildLuceneQuery("HVBIT timeout", itemTypes: null, statusFilter: null)
            .Should().Be("((HVBIT OR timeout))",
                "simple multi-term queries keep the default OR behavior on the REST path too");
    }

    [Fact]
    public void RestBuilder_RawLucenePlusTypeAndStatus_PreservesComposition()
    {
        McpTools.BuildLuceneQuery(
                "category.KEY:MyCategory AND (timeout)",
                itemTypes: "requirement",
                statusFilter: "open")
            .Should().Be(
                "(category.KEY:MyCategory AND (timeout)) AND type:requirement AND status:open");
    }
}
