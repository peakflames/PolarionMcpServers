using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tests for the shared input validators used by MCP tools and REST endpoints.
/// </summary>
public sealed class InputValidatorTests
{
    [Theory]
    [InlineData("ABC-12345")]
    [InlineData("abc_1")]
    [InlineData("1A")]
    [InlineData("X")]
    public void IsValidWorkItemId_AcceptsWellFormedIds(string value)
    {
        McpTools.IsValidWorkItemId(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-ABC")]
    [InlineData("_ABC")]
    [InlineData("ABC 1")]
    [InlineData("ABC'1")]
    [InlineData("ABC;1")]
    [InlineData("ABC/1")]
    [InlineData("ABC)")]
    [InlineData("ABC%")]
    [InlineData("ABC-1\n")]
    [InlineData("ÄBC-1")]
    public void IsValidWorkItemId_RejectsMalformedIds(string? value)
    {
        McpTools.IsValidWorkItemId(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidWorkItemId_EnforcesLengthCap()
    {
        McpTools.IsValidWorkItemId(new string('A', McpTools.MaxWorkItemIdLength)).Should().BeTrue();
        McpTools.IsValidWorkItemId(new string('A', McpTools.MaxWorkItemIdLength + 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1")]
    [InlineData("12345")]
    [InlineData("007")]
    [InlineData("999999999999999999")]
    public void IsValidRevision_AcceptsLatestOrPositiveInteger(string value)
    {
        McpTools.IsValidRevision(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("-2")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("abc")]
    [InlineData("١٢")]
    [InlineData("1000000000000000000")]
    public void IsValidRevision_RejectsInvalidRevisions(string? value)
    {
        McpTools.IsValidRevision(value).Should().BeFalse();
    }
}
