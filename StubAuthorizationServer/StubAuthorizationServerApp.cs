namespace StubAuthorizationServer;

/// <summary>
/// Mirrors Program.BuildApp's shape (args, optional pre-build configure callback) — that is the
/// only reason the test project can wrap this in a TestServer via UseTestServer() inside
/// `configure`. The stub itself never references Microsoft.AspNetCore.TestHost.
/// </summary>
public static class StubAuthorizationServerApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        builder.Services.AddSingleton<StubAuthorizationServerState>();

        var app = builder.Build();

        app.MapGet("/", () => Results.Text("Stub Authorization Server is running.", "text/plain"));
        app.MapGet("/.well-known/openid-configuration", DiscoveryEndpoint.Handle);
        app.MapGet("/jwks", JwksEndpoint.Handle);
        app.MapPost("/connect/token", TokenEndpoint.HandleAsync);
        app.MapGet("/oauth2/v1/userinfo", UserInfoEndpoint.Handle);

        return app;
    }
}
