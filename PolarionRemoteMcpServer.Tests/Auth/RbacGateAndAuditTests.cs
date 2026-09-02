using PolarionRemoteMcpServer.Authentication;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarionMcpTools;
using PolarionMcpTools.Rbac;
using PolarionRemoteMcpServer.Auth;
using PolarionRemoteMcpServer.Rbac;
using PolarionRemoteMcpServer.Rbac.Audit;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>Polarion-specific RBAC coverage beyond the cid/scope/identity-source suites:
/// the real membership gate's unmapped-alias path, the audit record's client-id fallback, the
/// AuditOnly shadow mode, and the identity-lookup failure paths that never reach the gate at
/// all.</summary>
public class RbacGateAndAuditTests : IDisposable
{
    private const string Alias = McpAuthTestConfigBuilder.DefaultAlias;
    private const string Email = "member@example.invalid";

    private readonly McpAuthTestConfigBuilder _mcpAuth = new();

    public void Dispose() => _mcpAuth.Dispose();

    [Fact]
    public async Task UnmappedProjectAlias_Denies_WithUnmappedProjectAliasReason()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled();
        factory.IdentityLookup.Map(Email, "member-username");

        // Wired after PolarionFakeFactory's own constructor closure, so this Replace() wins: the
        // real gate's unmapped-alias branch never touches Polarion, since it returns before any
        // client is created.
        factory.WithPostAuthServices(services => services.Replace(ServiceDescriptor.Singleton<IProjectVisibilityGate>(sp =>
            new PolarionProjectUsersGate(
                sp.GetRequiredService<List<PolarionProjectConfig>>(),
                sp.GetRequiredService<IOptions<RbacOptions>>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<PolarionProjectUsersGate>>()))));

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: Email);

        var response = await CallListSpacesAsync(factory, "not-a-configured-alias", token);

        Assert.True(response.IsSuccessStatusCode);
        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal(AccessDecision.Deny, record.Decision);
        Assert.Equal("unmapped_project_alias", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    [Fact]
    public async Task MembershipGate_Denies_NonMember_AndBlocksTheCall()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled();
        factory.IdentityLookup.Map(Email, "member-username");
        factory.Gate = new FakeMembershipGate(); // allows nobody by default

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: Email);

        var response = await CallListSpacesAsync(factory, Alias, token);

        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal(AccessDecision.Deny, record.Decision);
        Assert.Equal("not_a_project_member", record.DecisionReason);
        Assert.True(record.Blocked);
        Assert.Contains(RbacIdentityFilter.DeniedMessage, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MembershipGate_AuditOnly_RecordsDeny_ButDoesNotBlock()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled(auditOnly: true);
        factory.IdentityLookup.Map(Email, "member-username");
        factory.Gate = new FakeMembershipGate();

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: Email);

        var response = await CallListSpacesAsync(factory, Alias, token);

        Assert.True(response.IsSuccessStatusCode);
        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal(AccessDecision.Deny, record.Decision);
        Assert.Equal("not_a_project_member", record.DecisionReason);
        Assert.False(record.Blocked);

        // AuditOnly shadow mode still calls through to the tool body — the fake client factory
        // always fails, so its own failure text proves the call really ran rather than having
        // been short-circuited by the gate.
        Assert.Contains("stub: no live Polarion connection", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditRecord_FallsBackToCidClaim_WhenNoClientIdClaim()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled();
        factory.IdentityLookup.Map(Email, "member-username");

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: Email, clientId: "0oaSomeClient");

        await CallListSpacesAsync(factory, Alias, token);

        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal("0oaSomeClient", record.OAuthClientId);
    }

    [Fact]
    public async Task UnmatchedEmail_Denies_WithUnresolvedReason_WithoutEverCallingTheGate()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled();
        // Deliberately unmapped: FakeIdentityLookup defaults to NotFound.
        var countingGate = new CountingGate();
        factory.Gate = countingGate;

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: "nobody@example.invalid");

        await CallListSpacesAsync(factory, Alias, token);

        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal("identity_unresolved", record.DecisionReason);
        Assert.True(record.Blocked);
        Assert.Equal(0, countingGate.CallCount);
    }

    [Fact]
    public async Task LookupFailure_Denies_WithUnresolvedReason_WithoutEverCallingTheGate()
    {
        using var factory = new PolarionFakeFactory();
        _mcpAuth.Apply(factory);
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        factory.WithRbacEnabled();
        factory.IdentityLookup.ThrowFor(Email);
        var countingGate = new CountingGate();
        factory.Gate = countingGate;

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], email: Email);

        await CallListSpacesAsync(factory, Alias, token);

        var record = Assert.Single(factory.AuditSink.Records);
        Assert.Equal("identity_unresolved", record.DecisionReason);
        Assert.True(record.Blocked);
        Assert.Equal(0, countingGate.CallCount);
    }

    // ----------------------------------------------------------------

    private static async Task<HttpResponseMessage> CallListSpacesAsync(
        PolarionFakeFactory factory, string alias, string bearerToken)
    {
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{alias}/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_spaces","arguments":{}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return await client.SendAsync(request);
    }
}
