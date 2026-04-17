using FluentAssertions;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// Unit tests for test configuration loading and validation
/// </summary>
public sealed class TestConfigurationTests
{
    [Fact]
    public void TestConfiguration_Instance_ShouldLoadSuccessfully()
    {
        // Act
        var config = TestConfiguration.Instance;

        // Assert
        config.Should().NotBeNull("configuration should load successfully");
        config.Settings.Should().NotBeNull("settings should be loaded");
    }

    [Fact]
    public void TestConfiguration_Settings_ShouldHaveApiKey()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Act
        var apiKey = config.Settings.ApiKey;

        // Assert
        apiKey.Should().NotBeNullOrWhiteSpace("API key should be configured");
    }

    [Fact]
    public void TestConfiguration_Settings_ShouldHaveTestProjects()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Act
        var projects = config.Settings.TestProjects;

        // Assert
        projects.Should().NotBeNull("test projects should be configured");
        projects.Should().NotBeEmpty("at least one test project should be configured");
    }

    [Fact]
    public void TestConfiguration_GetProject_ValidProjectId_ShouldReturnProject()
    {
        // Arrange
        var config = TestConfiguration.Instance;
        var projectId = config.Settings.TestProjects.First().ProjectId;

        // Act
        var project = config.GetProject(projectId);

        // Assert
        project.Should().NotBeNull("project should be found");
        project.ProjectId.Should().Be(projectId, "returned project should match requested ID");
    }

    [Fact]
    public void TestConfiguration_GetProject_InvalidProjectId_ShouldThrowException()
    {
        // Arrange
        var config = TestConfiguration.Instance;
        var invalidProjectId = "NonExistentProject12345";

        // Act
        var act = () => config.GetProject(invalidProjectId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{invalidProjectId}*", "exception should mention the invalid project ID");
    }

    [Fact]
    public void TestConfiguration_GetScenario_ValidScenarioName_ShouldReturnScenario()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Verify at least one scenario is configured
        config.Settings.TestScenarios.Should().NotBeEmpty("at least one test scenario should be configured");

        var scenarioName = config.Settings.TestScenarios.First().Name;

        // Act
        var scenario = config.GetScenario(scenarioName);

        // Assert
        scenario.Should().NotBeNull("scenario should be found");
        scenario.Name.Should().Be(scenarioName, "returned scenario should match requested name");
    }

    [Fact]
    public void TestConfiguration_GetScenario_InvalidScenarioName_ShouldThrowException()
    {
        // Arrange
        var config = TestConfiguration.Instance;
        var invalidScenarioName = "NonExistentScenario12345";

        // Act
        var act = () => config.GetScenario(invalidScenarioName);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{invalidScenarioName}*", "exception should mention the invalid scenario name");
    }

    [Fact]
    public void TestConfiguration_TestProjects_ShouldHaveRequiredFields()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Act & Assert
        foreach (var project in config.Settings.TestProjects)
        {
            project.ProjectId.Should().NotBeNullOrWhiteSpace("project ID should be configured");
            project.ServerUrl.Should().NotBeNullOrWhiteSpace("server URL should be configured");
            project.Username.Should().NotBeNullOrWhiteSpace("username should be configured");
            project.Password.Should().NotBeNullOrWhiteSpace("password should be configured");
        }
    }

    [Fact]
    public void TestConfiguration_TestScenarios_ShouldHaveRequiredFields()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Skip if no scenarios configured (might be using environment variables)
        if (config.Settings.TestScenarios.Count == 0)
        {
            return;
        }

        // Act & Assert
        foreach (var scenario in config.Settings.TestScenarios)
        {
            scenario.Name.Should().NotBeNullOrWhiteSpace("scenario name should be configured");
            scenario.ProjectId.Should().NotBeNullOrWhiteSpace("project ID should be configured");
            scenario.SpaceId.Should().NotBeNullOrWhiteSpace("space ID should be configured");
            scenario.DocumentId.Should().NotBeNullOrWhiteSpace("document ID should be configured");
            scenario.ExpectedWorkItemIds.Should().NotBeNull("expected work item IDs should be configured");
        }
    }

    [Fact]
    public void TestConfiguration_ApiBaseUrl_ShouldBeValidUrl()
    {
        // Arrange
        var config = TestConfiguration.Instance;

        // Act
        var apiBaseUrl = config.Settings.ApiBaseUrl;

        // Assert
        apiBaseUrl.Should().NotBeNullOrWhiteSpace("API base URL should be configured");
        Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out _).Should().BeTrue("API base URL should be a valid absolute URL");
    }
}
