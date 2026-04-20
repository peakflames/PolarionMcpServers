using System.Net;
using System.Text.Json;
using FluentAssertions;
using PolarionRemoteMcpServer.Tests.Fixtures;
using PolarionRemoteMcpServer.Tests.TestData;

namespace PolarionRemoteMcpServer.Tests.Integration;

/// <summary>
/// Integration tests for Documents REST API endpoints
/// Tests verify work items are correctly retrieved from documents at various revisions
/// All test data is loaded from testsettings.json to avoid hardcoding company-specific information
/// </summary>
public sealed class DocumentsEndpointsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestConfiguration _testConfig;

    public DocumentsEndpointsIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _testConfig = TestConfiguration.Instance;
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario1_ReturnsExpectedWorkItems()
    {
        // Arrange - Load test data from configuration
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedLatest");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        // Verify JSON:API structure
        jsonDoc.RootElement.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");
        dataElement.ValueKind.Should().Be(JsonValueKind.Array, "data should be an array");

        // Extract work item IDs from response
        var workItemIds = ExtractWorkItemIds(dataElement);

        // Verify all expected work item IDs are present
        foreach (var expectedId in scenario.ExpectedWorkItemIds)
        {
            workItemIds.Should().Contain(expectedId,
                $"document should contain work item {expectedId}");
        }

        // Additional assertions
        workItemIds.Should().NotBeEmpty("document should contain work items");
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario2_ReturnsExpectedWorkItems()
    {
        // Arrange - Load test data from configuration
        var scenario = ExpectedWorkItems.GetScenario("BranchedLatest");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        // Verify JSON:API structure
        jsonDoc.RootElement.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");
        dataElement.ValueKind.Should().Be(JsonValueKind.Array, "data should be an array");

        // Extract work item IDs from response
        var workItemIds = ExtractWorkItemIds(dataElement);

        // Verify all expected work item IDs are present
        foreach (var expectedId in scenario.ExpectedWorkItemIds)
        {
            workItemIds.Should().Contain(expectedId,
                $"document should contain work item {expectedId}");
        }

        // Additional assertions
        workItemIds.Should().NotBeEmpty("document should contain work items");
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario3_HistoricalRevision_ReturnsExpectedWorkItems()
    {
        // Arrange - Load test data from configuration
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedHistoricRevision");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems?revision={scenario.Revision}";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        // Verify JSON:API structure
        jsonDoc.RootElement.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");
        dataElement.ValueKind.Should().Be(JsonValueKind.Array, "data should be an array");

        // For historical revisions, verify metadata
        if (dataElement.GetArrayLength() > 0)
        {
            var firstItem = dataElement[0];
            firstItem.TryGetProperty("attributes", out var attributes).Should().BeTrue("work item should have attributes");

            // Historical queries should include revision metadata
            if (attributes.TryGetProperty("revision", out var revisionProp))
            {
                revisionProp.GetString().Should().Be(scenario.Revision, "revision metadata should match requested revision");
            }

            if (attributes.TryGetProperty("isHistorical", out var isHistoricalProp))
            {
                isHistoricalProp.GetBoolean().Should().BeTrue("isHistorical should be true for revision queries");
            }
        }

        // Extract work item IDs from response
        var workItemIds = ExtractWorkItemIds(dataElement);

        // Verify all expected work item IDs are present
        foreach (var expectedId in scenario.ExpectedWorkItemIds)
        {
            workItemIds.Should().Contain(expectedId,
                $"document at revision {scenario.Revision} should contain work item {expectedId}");
        }

        // Additional assertions
        workItemIds.Should().NotBeEmpty("document should contain work items");
    }

    [Fact]
    public async Task GetDocumentWorkItems_Scenario4_BranchedHistoricalRevision_ReturnsExpectedWorkItems()
    {
        // Arrange - Load test data from configuration
        var scenario = ExpectedWorkItems.GetScenario("BranchedHistoricRevision");
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems?revision={scenario.Revision}";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        // Verify JSON:API structure
        jsonDoc.RootElement.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");
        dataElement.ValueKind.Should().Be(JsonValueKind.Array, "data should be an array");

        // For historical revisions, verify metadata
        if (dataElement.GetArrayLength() > 0)
        {
            var firstItem = dataElement[0];
            firstItem.TryGetProperty("attributes", out var attributes).Should().BeTrue("work item should have attributes");

            // Historical queries should include revision metadata
            if (attributes.TryGetProperty("revision", out var revisionProp))
            {
                revisionProp.GetString().Should().Be(scenario.Revision, "revision metadata should match requested revision");
            }

            if (attributes.TryGetProperty("isHistorical", out var isHistoricalProp))
            {
                isHistoricalProp.GetBoolean().Should().BeTrue("isHistorical should be true for revision queries");
            }
        }

        // Extract work item IDs from response
        var workItemIds = ExtractWorkItemIds(dataElement);

        // Verify all expected work item IDs are present
        foreach (var expectedId in scenario.ExpectedWorkItemIds)
        {
            workItemIds.Should().Contain(expectedId,
                $"document at revision {scenario.Revision} should contain work item {expectedId}");
        }

        // Additional assertions
        workItemIds.Should().NotBeEmpty("document should contain work items");
    }

    [Fact]
    public async Task GetDocumentWorkItems_WithTypeFilter_ReturnsFilteredItems()
    {
        // Arrange - Load test data from configuration
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedLatest");
        scenario.FilterTypes.Should().NotBeEmpty("NonBranchedLatest scenario must have FilterTypes configured in testsettings.json");

        var typesParam = string.Join(",", scenario.FilterTypes);
        var url = $"/polarion/rest/v1/projects/{scenario.ProjectId}/spaces/{scenario.SpaceId}/documents/{scenario.DocumentId}/workitems?types={typesParam}";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the endpoint should return 200 OK");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        jsonDoc.RootElement.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");

        // Verify all returned items are one of the configured filter types
        if (dataElement.GetArrayLength() > 0)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                item.TryGetProperty("attributes", out var attributes).Should().BeTrue("work item should have attributes");
                attributes.TryGetProperty("type", out var typeProp).Should().BeTrue("work item should have type attribute");

                var typeValue = typeProp.GetString();
                scenario.FilterTypes.Should().Contain(typeValue, $"returned item type '{typeValue}' should be one of the configured filter types");
            }
        }
    }

    [Fact]
    public async Task GetDocumentWorkItems_InvalidProject_Returns404()
    {
        // Arrange
        var invalidProjectId = "NonExistentProject";
        var spaceId = "SomeSpace";
        var documentId = "SomeDocument";

        var url = $"/polarion/rest/v1/projects/{invalidProjectId}/spaces/{spaceId}/documents/{documentId}/workitems";

        // Act
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "non-existent project should return 404");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var jsonDoc = JsonDocument.Parse(content);

        // Verify JSON:API error structure
        jsonDoc.RootElement.TryGetProperty("errors", out var errorsElement).Should().BeTrue("error response should have 'errors' property");
        errorsElement.ValueKind.Should().Be(JsonValueKind.Array, "errors should be an array");

        if (errorsElement.GetArrayLength() > 0)
        {
            var firstError = errorsElement[0];
            firstError.TryGetProperty("status", out var statusProp).Should().BeTrue("error should have status");
            statusProp.GetString().Should().Be("404", "error status should be 404");
        }
    }

    /// <summary>
    /// Extracts work item IDs from JSON:API data array
    /// Strips the project prefix (e.g., "ProjectName/ITEM-12345" becomes "ITEM-12345")
    /// </summary>
    private static List<string> ExtractWorkItemIds(JsonElement dataElement)
    {
        var workItemIds = new List<string>();

        foreach (var item in dataElement.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    // Strip project prefix if present (e.g., "ProjectName/ITEM-12345" -> "ITEM-12345")
                    var idWithoutPrefix = id.Contains('/') ? id.Split('/')[1] : id;
                    workItemIds.Add(idWithoutPrefix);
                }
            }
        }

        return workItemIds;
    }
}
