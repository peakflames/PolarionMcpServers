using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polarion;
using PolarionMcpTools;
using PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Input-validation tests for the list_documents titleFilter parameter. The filter reaches a
/// SQL pattern in the SDK, so unsafe values must be rejected before any client call.
/// </summary>
public sealed class ListDocumentsTitleFilterTests
{
    private static McpTools NewTool(IPolarionClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolarionClientFactory>(new StubPolarionClientFactory(client));
        services.AddSingleton<List<PolarionProjectConfig>>(new List<PolarionProjectConfig>());
        return new McpTools(services.BuildServiceProvider());
    }

    [Theory]
    [InlineData("requirements")]
    [InlineData("Flight Controls (Rev-A)")]
    [InlineData("SW_Reqs v2.1")]
    [InlineData("Design & Analysis")]
    public void AcceptsPlainTitleFilter(string value)
    {
        McpTools.IsSafeForPolarionTitleFilter(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc'")]
    [InlineData("abc%")]
    [InlineData("abc;")]
    [InlineData("abc--")]
    [InlineData("abc\\")]
    [InlineData("abc/*")]
    [InlineData("abc*/")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsUnsafeTitleFilter(string? value)
    {
        McpTools.IsSafeForPolarionTitleFilter(value).Should().BeFalse();
    }

    [Fact]
    public void RejectsOverlongTitleFilter()
    {
        McpTools.IsSafeForPolarionTitleFilter(new string('a', McpTools.MaxTitleFilterLength + 1)).Should().BeFalse();
        McpTools.IsSafeForPolarionTitleFilter(new string('a', McpTools.MaxTitleFilterLength)).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc'")]
    [InlineData("abc%")]
    [InlineData("abc')")]
    public async Task ListDocuments_RejectsUnsafeTitleFilter_WithoutCallingClient(string titleFilter)
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);

        var result = await NewTool(client.Object).ListDocuments(titleFilter: titleFilter);

        result.Should().StartWith("ERROR: (108)");
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListDocuments_RejectsUnsafeTitleFilter_WhenSpaceGiven()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);

        var result = await NewTool(client.Object).ListDocuments(space: "My Space", titleFilter: "abc%");

        result.Should().StartWith("ERROR: (108)");
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListDocuments_PassesSafeTitleFilterToClient()
    {
        var client = new Mock<IPolarionClient>(MockBehavior.Strict);
        client
            .Setup(c => c.GetModulesThinAsync(It.IsAny<string?>(), "Flight Controls (Rev-A)"))
            .ReturnsAsync(Result.Ok(Array.Empty<ModuleThin>()));

        var result = await NewTool(client.Object).ListDocuments(titleFilter: "Flight Controls (Rev-A)");

        result.Should().StartWith("No documents found");
        client.Verify(c => c.GetModulesThinAsync(It.IsAny<string?>(), "Flight Controls (Rev-A)"), Times.Once);
    }
}
