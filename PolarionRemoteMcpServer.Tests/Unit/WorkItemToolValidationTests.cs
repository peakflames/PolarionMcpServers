using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using Polarion.Generated.Tracker;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Work item ID and revision validation tests for get_workitem, get_workitem_history, and
/// get_workitem_details. Invalid values must be rejected before any Polarion client call.
/// </summary>
public sealed class WorkItemToolValidationTests
{
    private static (McpTools Tool, Mock<IPolarionClient> Client) NewTool()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return (new McpTools(services.BuildServiceProvider()), client);
    }

    public static TheoryData<string> InvalidIds => new() { "WI'1", "WI 1", "-WI", "WI;1", "WI)", "WI%" };

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task GetWorkitem_RejectsInvalidWorkItemId(string workitemId)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetWorkitem(workitemId);
        result.Should().StartWith("ERROR: (109)");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    [InlineData("1'")]
    public async Task GetWorkitem_RejectsInvalidRevision(string revision)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetWorkitem("WI-1", revision);
        result.Should().StartWith("ERROR: (110)");
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetWorkitem_PassesValidRevisionToClient()
    {
        var (tool, client) = NewTool();
        client
            .Setup(c => c.GetWorkItemByIdAsync("WI-1", "42"))
            .ReturnsAsync(Result.Fail<WorkItem>("not found"));

        var result = await tool.GetWorkitem("WI-1", "42");

        result.Should().Contain("at revision '42'");
        client.Verify(c => c.GetWorkItemByIdAsync("WI-1", "42"), Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task GetWorkitemHistory_RejectsInvalidWorkItemId(string workitemId)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetWorkitemHistory(workitemId);
        result.Should().StartWith("ERROR: (109)");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task GetWorkitemDetails_RejectsAnyInvalidWorkItemId(string workitemId)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetWorkitemDetails($"WI-1,{workitemId}");
        result.Should().StartWith("ERROR: (109)");
        client.VerifyNoOtherCalls();
    }
}
