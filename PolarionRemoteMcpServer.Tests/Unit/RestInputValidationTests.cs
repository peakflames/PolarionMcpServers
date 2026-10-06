using System.Net;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// In-process REST input-validation tests. Every request here must be rejected with 400 before a
/// handler resolves a project or creates a Polarion client, so the dummy, never-dialed project
/// configuration is never used.
/// </summary>
public sealed class RestInputValidationTests : IDisposable
{
    private const string ApiKey = "unit-test-api-key";
    private const string Alias = "starlight";

    private readonly PolarionMcpServerFactory _factory;
    private readonly HttpClient _client;

    public RestInputValidationTests()
    {
        _factory = new PolarionMcpServerFactory()
            .WithProject(Alias, "Starlight_Main", isDefault: true)
            .With("ApiConsumers:Consumers:unit:Name", "unit")
            .With("ApiConsumers:Consumers:unit:ApplicationKey", ApiKey)
            .With("ApiConsumers:Consumers:unit:Active", "true")
            .With("ApiConsumers:Consumers:unit:AllowedScopes:0", "polarion:read");
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("-2")]
    [InlineData("abc")]
    public async Task DocumentWorkItems_RejectsInvalidRevision(string revision)
    {
        var response = await _client.GetAsync(
            $"/polarion/rest/v1/projects/{Alias}/spaces/MySpace/documents/MyDoc/workitems?revision={Uri.EscapeDataString(revision)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("revision");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/revisions")]
    [InlineData("/linkedworkitems")]
    [InlineData("/backlinkedworkitems")]
    public async Task WorkItemRoutes_RejectInvalidWorkItemId(string suffix)
    {
        foreach (var workitemId in new[] { "WI'1", "WI 1", "-WI", "WI)" })
        {
            var response = await _client.GetAsync(
                $"/polarion/rest/v1/projects/{Alias}/workitems/{Uri.EscapeDataString(workitemId)}{suffix}");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"workitemId '{workitemId}' on '{suffix}'");
            (await response.Content.ReadAsStringAsync()).Should().Contain("workitemId");
        }
    }
}
