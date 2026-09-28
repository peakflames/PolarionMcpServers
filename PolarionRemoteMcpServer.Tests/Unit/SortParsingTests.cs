using FluentAssertions;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Tests for sort parsing shared by search_workitems, search_workitems_sql, and REST search.
/// A leading '-' maps to Polarion's '~' descending prefix.
/// </summary>
public sealed class SortParsingTests
{
    [Theory]
    [InlineData(null, "created")]
    [InlineData("created", "created")]
    [InlineData("Updated", "updated")]
    [InlineData("-updated", "~updated")]
    [InlineData(" -Title ", "~title")]
    [InlineData("-id", "~id")]
    public void TryParseSort_ValidValues_MapToPolarionSyntax(string? input, string expected)
    {
        McpTools.TryParseSort(input, out var polarionSort).Should().BeTrue();
        polarionSort.Should().Be(expected);
    }

    [Theory]
    [InlineData("not-a-field")]
    [InlineData("-")]
    [InlineData("--updated")]
    [InlineData("~updated")]
    public void TryParseSort_InvalidValues_AreRejected(string input)
    {
        McpTools.TryParseSort(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("~updated", "updated (descending)")]
    [InlineData("created", "created")]
    public void DescribeSort_ReadableForResultHeaders(string polarionSort, string expected)
    {
        McpTools.DescribeSort(polarionSort).Should().Be(expected);
    }
}
