using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StubAuthorizationServer;
using Xunit;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>Boots the stub authorization server on a real loopback Kestrel listener (not a
/// TestServer) so the resource server's Authority-based JwtBearer configuration can fetch
/// discovery/JWKS over ordinary HTTP, exactly as it would against a real external AS.</summary>
public sealed class StubAuthorizationServerFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public string IssuerUrl { get; private set; } = string.Empty;

    public StubAuthorizationServerState State { get; private set; } = new();

    public async ValueTask InitializeAsync()
    {
        var app = StubAuthorizationServerApp.Build(
            [],
            builder => builder.WebHost.UseUrls("http://127.0.0.1:0"));

        await app.StartAsync();

        _app = app;
        IssuerUrl = app.Urls.First().TrimEnd('/');
        State = app.Services.GetRequiredService<StubAuthorizationServerState>();
    }

    public async Task<string> IssueTokenAsync(IEnumerable<KeyValuePair<string, string>>? formOverrides = null)
    {
        using var client = new HttpClient();
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
        foreach (var kvp in formOverrides ?? [])
        {
            form[kvp.Key] = kvp.Value;
        }

        using var response = await client.PostAsync($"{IssuerUrl}/connect/token", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return body![("access_token")].GetString()!;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
