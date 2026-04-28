namespace PolarionRemoteMcpServer.Tests.TestData;

/// <summary>
/// Provides access to expected work items from test configuration
/// This class loads expected work items from testsettings.json to avoid
/// hardcoding company-specific project names, server URLs, and work item IDs
/// </summary>
public static class ExpectedWorkItems
{
    /// <summary>
    /// Gets a test scenario by name from configuration
    /// </summary>
    public static TestScenario GetScenario(string scenarioName)
    {
        return TestConfiguration.Instance.GetScenario(scenarioName);
    }

    /// <summary>
    /// Gets all configured test scenarios
    /// </summary>
    public static List<TestScenario> GetAllScenarios()
    {
        return TestConfiguration.Instance.Settings.TestScenarios;
    }
}
