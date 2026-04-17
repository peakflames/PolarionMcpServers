using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PolarionRemoteMcpServer;

namespace PolarionRemoteMcpServer.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TestConfiguration _testConfig;

    public TestWebApplicationFactory()
    {
        _testConfig = TestConfiguration.Instance;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Use test configuration
            config.AddJsonFile("testsettings.json", optional: true, reloadOnChange: false);
            config.AddEnvironmentVariables();
        });

        // Set environment to "Test" to trigger test-specific logging configuration
        builder.UseEnvironment("Test");

        // Additional test-specific configuration can be added here
        builder.ConfigureServices(services =>
        {
            // Can override services for testing if needed
        });
    }

    /// <summary>
    /// Creates an HTTP client configured with authentication headers.
    /// Timeout is set to 10 minutes to accommodate long-running scenarios
    /// (e.g. branched historical revision queries that make many sequential Polarion API calls).
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.Add("X-API-Key", _testConfig.Settings.ApiKey);
        return client;
    }
}
