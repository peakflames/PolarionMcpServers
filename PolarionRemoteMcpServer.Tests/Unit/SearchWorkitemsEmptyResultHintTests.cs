using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using Polarion.Generated.Tracker;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Empty-result hint for search_workitems: a bare document.id value (no "space/") matches nothing
/// in Polarion, so a zero-result reply must point at the space-qualified form.
/// </summary>
public sealed class SearchWorkitemsEmptyResultHintTests
{
    private static McpTools NewTool(IPolarionClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return new McpTools(services.BuildServiceProvider());
    }

    private static Mock<IPolarionClient> ClientReturningNoMatches()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok(Array.Empty<WorkItem>()));
        return client;
    }

    [Theory]
    [InlineData("document.id:\"MyDoc\"")]
    [InlineData("document.id:MyDoc")]
    [InlineData("type:requirement AND document.id:\"MyDoc\"")]
    [InlineData("(document.id:MyDoc)")]
    public async Task SearchWorkitems_AddsQualifiedFormHint_WhenDocumentIdHasNoSpace(string query)
    {
        var result = await NewTool(ClientReturningNoMatches().Object).SearchWorkitems(query);

        result.Should().StartWith("No work items matching");
        result.Should().Contain("document.id:\"<space>/<docId>\"");
    }

    [Theory]
    [InlineData("document.id:\"MySpace/MyDoc\"")]
    [InlineData("document.id:MySpace/MyDoc")]
    [InlineData("voltage")]
    [InlineData("type:requirement")]
    [InlineData("xdocument.id:MyDoc")]
    public async Task SearchWorkitems_OmitsHint_WhenDocumentIdIsQualifiedOrAbsent(string query)
    {
        var result = await NewTool(ClientReturningNoMatches().Object).SearchWorkitems(query);

        result.Should().StartWith("No work items matching");
        result.Should().NotContain("Hint:");
    }
}
