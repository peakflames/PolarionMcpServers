using System.Text.Json;

namespace PolarionRemoteMcpServer.Tests;

/// <summary>
/// Configuration for test execution
/// </summary>
public sealed class TestSettings
{
    /// <summary>
    /// Base URL for the API under test
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5090";

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Test project configurations
    /// </summary>
    public List<TestProject> TestProjects { get; set; } = [];

    /// <summary>
    /// Test scenario definitions
    /// </summary>
    public List<TestScenario> TestScenarios { get; set; } = [];
}

/// <summary>
/// Represents a Polarion project configuration for testing
/// </summary>
public sealed class TestProject
{
    /// <summary>
    /// Polarion project ID
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Polarion server URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Polarion username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Polarion password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Represents a test scenario for document work item verification
/// </summary>
public sealed class TestScenario
{
    /// <summary>
    /// Name of the test scenario
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Polarion project ID
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Polarion space ID
    /// </summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>
    /// Polarion document ID
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Document revision (null for latest)
    /// </summary>
    public string? Revision { get; set; }

    /// <summary>
    /// Work item types to use when testing type-filter queries (e.g., ["requirement"] or ["requirement", "testCase"]).
    /// Passed as a comma-separated list in the types query parameter.
    /// Only required for scenarios used by the WithTypeFilter test.
    /// </summary>
    public List<string> FilterTypes { get; set; } = [];

    /// <summary>
    /// Expected work item IDs that should be present in the document
    /// </summary>
    public List<string> ExpectedWorkItemIds { get; set; } = [];
}

/// <summary>
/// Configuration loader and provider for tests
/// </summary>
public sealed class TestConfiguration
{
    private static TestConfiguration? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Test settings loaded from configuration
    /// </summary>
    public TestSettings Settings { get; }

    private TestConfiguration(TestSettings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Gets the singleton instance of test configuration
    /// </summary>
    public static TestConfiguration Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Loads test configuration from testsettings.json or environment variables
    /// </summary>
    private static TestConfiguration Load()
    {
        var testSettingsPath = Path.Combine(AppContext.BaseDirectory, "testsettings.json");

        TestSettings settings;

        if (File.Exists(testSettingsPath))
        {
            var json = File.ReadAllText(testSettingsPath);
            var root = JsonSerializer.Deserialize<TestSettingsRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            settings = root?.TestSettings ?? throw new InvalidOperationException(
                "Failed to load TestSettings from testsettings.json");
        }
        else
        {
            // Fall back to environment variables if no config file exists
            settings = new TestSettings
            {
                ApiBaseUrl = Environment.GetEnvironmentVariable("TEST_API_BASE_URL") ?? "http://localhost:5090",
                ApiKey = Environment.GetEnvironmentVariable("TEST_API_KEY") ?? string.Empty
            };

            // Note: Test projects and scenarios should be configured in testsettings.json
            // Environment variable support for individual projects can be added if needed
        }

        // Validate configuration
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "API key not configured. Please set ApiKey in testsettings.json or TEST_API_KEY environment variable.");
        }

        if (settings.TestProjects.Count == 0)
        {
            throw new InvalidOperationException(
                "No test projects configured. Please add TestProjects to testsettings.json.");
        }

        return new TestConfiguration(settings);
    }

    /// <summary>
    /// Gets a test scenario by name
    /// </summary>
    public TestScenario GetScenario(string name)
    {
        return Settings.TestScenarios.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Test scenario '{name}' not found in configuration.");
    }

    /// <summary>
    /// Gets a test project by ID
    /// </summary>
    public TestProject GetProject(string projectId)
    {
        return Settings.TestProjects.FirstOrDefault(p => p.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Test project '{projectId}' not found in configuration.");
    }

    /// <summary>
    /// Root wrapper for JSON deserialization
    /// </summary>
    private sealed class TestSettingsRoot
    {
        public TestSettings? TestSettings { get; set; }
    }
}
