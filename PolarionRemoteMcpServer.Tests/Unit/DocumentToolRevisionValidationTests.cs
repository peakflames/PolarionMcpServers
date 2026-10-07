using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Revision-validation tests for the document tools: "-1" or a positive integer only. Invalid
/// values must be rejected before any Polarion client call.
/// </summary>
public sealed class DocumentToolRevisionValidationTests
{
    private static (McpTools Tool, Mock<IPolarionClient> Client) NewTool()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client.Object));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return (new McpTools(services.BuildServiceProvider()), client);
    }

    public static TheoryData<string> InvalidRevisions => new() { "0", "000", "-2", "1.5", "١" };

    [Theory]
    [MemberData(nameof(InvalidRevisions))]
    public async Task GetDocumentOutline_RejectsInvalidRevision(string revision)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetDocumentOutline("MySpace", "MyDoc", revision);
        result.Should().StartWith("ERROR: (105)");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidRevisions))]
    public async Task GetDocumentSection_RejectsInvalidRevision(string revision)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetDocumentSection("MySpace", "MyDoc", "1", revision);
        result.Should().StartWith("ERROR: (105)");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidRevisions))]
    public async Task GetWorkItemsInModule_RejectsInvalidRevision(string revision)
    {
        var (tool, client) = NewTool();
        var result = await tool.GetWorkItemsInModule("MySpace", "MyDoc", null, revision);
        result.Should().StartWith("ERROR: (102)");
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidRevisions))]
    public async Task SearchInDocument_RejectsInvalidRevision(string revision)
    {
        var (tool, client) = NewTool();
        var result = await tool.SearchInDocument("MySpace", "MyDoc", "voltage", revision);
        result.Should().StartWith("ERROR: (105)");
        client.VerifyNoOtherCalls();
    }
}
