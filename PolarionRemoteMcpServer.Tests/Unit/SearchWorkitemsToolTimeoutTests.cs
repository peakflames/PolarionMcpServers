using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using Polarion.Generated.Tracker;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Timeout-path tests for search_workitems (Lucene tool).
/// A broad query hitting the WCF SendTimeout produces an error string containing "timed out";
/// these tests verify it returns (1049) with actionable guidance instead of a raw dump.
/// </summary>
public sealed class SearchWorkitemsToolTimeoutTests
{
    private static McpTools NewTool(IPolarionClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return new McpTools(services.BuildServiceProvider());
    }

    [Theory]
    [InlineData("The request channel timed out attempting to send after 00:01:00")]
    [InlineData("Operation timed out")]
    [InlineData("SendTimeout exceeded")]
    public async Task SearchWorkitems_ReturnsCode1049_OnTimeoutInFailedResult(string timeoutMessage)
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail<WorkItem[]>(timeoutMessage));

        var result = await NewTool(client.Object).SearchWorkitems("voltage");

        result.Should().StartWith("ERROR: (1049)");
        result.Should().Contain("too broad");
    }

    [Fact]
    public async Task SearchWorkitems_ReturnsCode1049_OnTimeoutException()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchWorkitemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ThrowsAsync(new TimeoutException("The operation has timed out."));

        var result = await NewTool(client.Object).SearchWorkitems("voltage");

        result.Should().StartWith("ERROR: (1049)");
    }
}
