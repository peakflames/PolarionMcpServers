using FluentAssertions;
using PolarionRemoteMcpServer.Tests.TestData;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Unit tests for ExpectedWorkItems and test configuration loading
/// Validates that test scenarios are properly loaded from configuration and contain expected structure
/// </summary>
public sealed class ExpectedWorkItemsTests
{
    [Fact]
    public void GetScenario_NonBranchedLatest_ShouldHaveValidData()
    {
        // Arrange & Act
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedLatest");

        // Assert
        scenario.Should().NotBeNull();
        scenario.Name.Should().Be("NonBranchedLatest");
        scenario.ProjectId.Should().NotBeNullOrWhiteSpace();
        scenario.SpaceId.Should().NotBeNullOrWhiteSpace();
        scenario.DocumentId.Should().NotBeNullOrWhiteSpace();
        scenario.Revision.Should().BeNull("latest revision scenarios should have null revision");
        scenario.ExpectedWorkItemIds.Should().NotBeEmpty("should have expected work item IDs");
    }

    [Fact]
    public void GetScenario_BranchedLatest_ShouldHaveValidData()
    {
        // Arrange & Act
        var scenario = ExpectedWorkItems.GetScenario("BranchedLatest");

        // Assert
        scenario.Should().NotBeNull();
        scenario.Name.Should().Be("BranchedLatest");
        scenario.ProjectId.Should().NotBeNullOrWhiteSpace();
        scenario.SpaceId.Should().NotBeNullOrWhiteSpace();
        scenario.DocumentId.Should().NotBeNullOrWhiteSpace();
        scenario.Revision.Should().BeNull("latest revision scenarios should have null revision");
        scenario.ExpectedWorkItemIds.Should().NotBeEmpty("should have expected work item IDs");
    }

    [Fact]
    public void GetScenario_NonBranchedHistoricalRevision_ShouldHaveValidData()
    {
        // Arrange & Act
        var scenario = ExpectedWorkItems.GetScenario("NonBranchedHistoricRevision");

        // Assert
        scenario.Should().NotBeNull();
        scenario.Name.Should().Be("NonBranchedHistoricRevision");
        scenario.ProjectId.Should().NotBeNullOrWhiteSpace();
        scenario.SpaceId.Should().NotBeNullOrWhiteSpace();
        scenario.DocumentId.Should().NotBeNullOrWhiteSpace();
        scenario.Revision.Should().NotBeNullOrWhiteSpace("historical revision scenarios should have a revision number");
        scenario.ExpectedWorkItemIds.Should().NotBeEmpty("should have expected work item IDs");
    }

    [Fact]
    public void GetScenario_BranchedHistoricalRevision_ShouldHaveValidData()
    {
        // Arrange & Act
        var scenario = ExpectedWorkItems.GetScenario("BranchedHistoricRevision");

        // Assert
        scenario.Should().NotBeNull();
        scenario.Name.Should().Be("BranchedHistoricRevision");
        scenario.ProjectId.Should().NotBeNullOrWhiteSpace();
        scenario.SpaceId.Should().NotBeNullOrWhiteSpace();
        scenario.DocumentId.Should().NotBeNullOrWhiteSpace();
        scenario.Revision.Should().NotBeNullOrWhiteSpace("historical revision scenarios should have a revision number");
        scenario.ExpectedWorkItemIds.Should().NotBeEmpty("should have expected work item IDs");
    }

    [Fact]
    public void GetAllScenarios_ShouldReturnAllConfiguredScenarios()
    {
        // Act
        var scenarios = ExpectedWorkItems.GetAllScenarios();

        // Assert
        scenarios.Should().NotBeEmpty("configuration should contain test scenarios");
        scenarios.Should().HaveCountGreaterOrEqualTo(4, "should have at least 4 test scenarios configured");
        scenarios.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Name), "all scenarios should have names");
        scenarios.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.ProjectId), "all scenarios should have project IDs");
        scenarios.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.SpaceId), "all scenarios should have space IDs");
        scenarios.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.DocumentId), "all scenarios should have document IDs");
        scenarios.Should().OnlyContain(s => s.ExpectedWorkItemIds.Count > 0, "all scenarios should have expected work item IDs");
    }

    [Fact]
    public void AllTestScenarios_ShouldHaveUniqueProjectSpaceDocumentRevisionCombinations()
    {
        // Arrange
        var scenarios = ExpectedWorkItems.GetAllScenarios();

        // Act - create tuples of unique identifiers
        var scenarioKeys = scenarios
            .Select(s => (s.ProjectId, s.SpaceId, s.DocumentId, s.Revision))
            .ToList();

        // Assert - each scenario should target a unique document/revision combination
        scenarioKeys.Should().OnlyHaveUniqueItems("each test scenario should target a unique document/revision combination");
    }

    [Fact]
    public void TestConfiguration_ShouldLoadFromFile()
    {
        // Act
        var config = TestConfiguration.Instance;

        // Assert
        config.Should().NotBeNull();
        config.Settings.Should().NotBeNull();
        config.Settings.ApiBaseUrl.Should().NotBeNullOrWhiteSpace("configuration should specify API base URL");
        config.Settings.TestProjects.Should().NotBeEmpty("configuration should contain test projects");
        config.Settings.TestScenarios.Should().NotBeEmpty("configuration should contain test scenarios");
    }

    [Fact]
    public void WorkItemIds_ShouldFollowExpectedFormat()
    {
        // Arrange
        var allScenarios = ExpectedWorkItems.GetAllScenarios();

        // Act & Assert
        foreach (var scenario in allScenarios)
        {
            foreach (var workItemId in scenario.ExpectedWorkItemIds)
            {
                // Work item IDs should contain a hyphen (e.g., "PRJ-12345")
                workItemId.Should().Contain("-",
                    $"work item ID '{workItemId}' in scenario '{scenario.Name}' should follow PROJECT-NUMBER format");

                // Should not be empty or whitespace
                workItemId.Should().NotBeNullOrWhiteSpace(
                    $"work item ID in scenario '{scenario.Name}' should not be empty");
            }
        }
    }
}
