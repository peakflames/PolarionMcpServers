using System.Net;
using PolarionRemoteMcpServer.Tests.Fixtures;
using PolarionRemoteMcpServer.Tests.TestData;

namespace PolarionRemoteMcpServer.Tests.Integration;

/// <summary>
/// Snapshot tests for Documents REST API endpoints using Verify.
/// These tests capture the full JSON response for each scenario so that
/// changes to PolarionApiClient behaviour can be detected by diffing
/// .received.json against .verified.json.
///
/// Workflow:
///   1. Run tests before making PolarionApiClient changes — .verified.json baselines are created
///      on first run and the tests pass automatically.
///   2. Make changes to PolarionApiClient source.
///   3. Run tests again — any response differences surface as failures showing exactly what changed.
///   4. Accept intentional changes: copy .received.json → .verified.json
///      (or run `dotnet verify accept` if the Verify.Tool CLI is installed).
///
/// Snapshot files are gitignored because they contain company-specific Polarion data.
/// They are stored in Integration/Snapshots/ next to this file.
/// </summary>
public sealed class DocumentsEndpointsSnapshotTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentsEndpointsSnapshotTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    private static VerifySettings SnapshotSettings()
    {
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        return settings;
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario1_NonBranchedLatest_Snapshot()
    {
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedLatest");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content, SnapshotSettings());
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario2_BranchedLatest_Snapshot()
    {
        var scenario = ExpectedWorkItems.GetScenario("BranchedLatest");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content, SnapshotSettings());
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario3_NonBranchedHistoricalRevision_Snapshot()
    {
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedHistoricRevision");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems?revision={scenario.Revision}";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content, SnapshotSettings());
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario4_BranchedHistoricalRevision_Snapshot()
    {
        var scenario = ExpectedWorkItems.GetScenario("BranchedHistoricRevision");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems?revision={scenario.Revision}";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content, SnapshotSettings());
    }
}
