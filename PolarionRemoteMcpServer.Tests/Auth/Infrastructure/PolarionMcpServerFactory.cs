using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Drives Program.BuildApp directly with UseTestServer(), rather than through
/// WebApplicationFactory&lt;Program&gt;'s reflection-based host resolution — that is the only path
/// where ConfigureAppConfiguration additions are visible to BuildApp's imperative
/// <c>builder.Configuration.Get&lt;PolarionAppConfig&gt;()</c> read, which runs before
/// <c>builder.Build()</c>.
///
/// <see cref="PolarionRemoteMcpServer.Tests.Fixtures.TestWebApplicationFactory"/> is a different,
/// deliberately untouched fixture — it needs a <c>testsettings.json</c> pointed at a reachable
/// Polarion instance for the existing REST integration tests. This factory is for the opposite
/// case: Auth/RBAC tests that must never depend on ambient machine state.
///
/// <c>builder.Configuration.Sources.Clear()</c> before adding the in-memory collection is
/// deliberate, not a straight port of the equivalent in peakflames/team-city-mcp: Polarion's config
/// carries a <c>PolarionProjects</c> array, and the developer machine's own (gitignored)
/// <c>appsettings.json</c> may already define several. Leaving the default file-based sources in
/// place would let ConfigurationBinder's array-by-index merge blend real, machine-specific project
/// entries into what a test believes is a clean, self-contained project list — a source of
/// non-deterministic tests that that repo's scalar-keys-only config shape never had to guard
/// against. Clearing sources makes the in-memory collection built by <c>With*</c> the sole source
/// of truth.
/// </summary>
public class PolarionMcpServerFactory : IDisposable
{
    private readonly Dictionary<string, string?> _configValues = new(StringComparer.Ordinal);
    private Action<IServiceCollection>? _configureServices;
    private Action<IServiceCollection>? _postAuthConfigureServices;
    private string? _environmentName;
    private int _projectCount;
    private WebApplication? _app;

    public PolarionMcpServerFactory With(string key, string? value)
    {
        _configValues[key] = value;
        return this;
    }

    /// <summary>McpAuthOptionsValidator only allows a plaintext http Issuer in Development.
    /// Passed as a `--environment` command-line argument to Program.BuildApp, not set via
    /// builder.Environment.EnvironmentName after the fact — WebApplicationBuilder combined with
    /// WebHost.UseTestServer() re-derives IHostEnvironment from host configuration during Build(),
    /// silently discarding a post-hoc mutation of the Environment property. The command-line
    /// switch is read during WebApplication.CreateBuilder(args) itself, before anything else runs,
    /// so it is not subject to that override.</summary>
    public PolarionMcpServerFactory WithEnvironment(string environmentName)
    {
        _environmentName = environmentName;
        return this;
    }

    public PolarionMcpServerFactory WithMcpAuth(Action<Dictionary<string, string?>> configure)
    {
        configure(_configValues);
        return this;
    }

    /// <summary>Appends one entry to the bound <c>PolarionProjects</c> list. Dummy, never-dialed
    /// SessionConfig values — every test that reaches this factory also replaces
    /// <c>IPolarionClientFactory</c> (see PolarionFakeFactory) so nothing ever opens a real
    /// connection with them.</summary>
    public PolarionMcpServerFactory WithProject(string alias, string realProjectId, bool isDefault = false)
    {
        var prefix = $"PolarionProjects:{_projectCount++}";
        _configValues[$"{prefix}:ProjectUrlAlias"] = alias;
        _configValues[$"{prefix}:Default"] = isDefault ? "true" : "false";
        _configValues[$"{prefix}:SessionConfig:ServerUrl"] = "https://polarion.example.invalid";
        _configValues[$"{prefix}:SessionConfig:Username"] = "test-service-account";
        _configValues[$"{prefix}:SessionConfig:Password"] = "test-password";
        _configValues[$"{prefix}:SessionConfig:ProjectId"] = realProjectId;
        return this;
    }

    public PolarionMcpServerFactory WithTestServices(Action<IServiceCollection> configure)
    {
        _configureServices = _configureServices is null ? configure : _configureServices + configure;
        return this;
    }

    /// <summary>Runs after <c>AddMcpAuth</c>/<c>AddRbac</c>, unlike <see cref="WithTestServices"/> —
    /// the only seam that can substitute a service AddRbac's own <c>Replace()</c> call would
    /// otherwise clobber (e.g. <c>IProjectVisibilityGate</c>).</summary>
    public PolarionMcpServerFactory WithPostAuthServices(Action<IServiceCollection> configure)
    {
        _postAuthConfigureServices = _postAuthConfigureServices is null ? configure : _postAuthConfigureServices + configure;
        return this;
    }

    /// <summary>Substitutes TimeProvider with the given FakeTimeProvider. Works because this
    /// registration (via the `configure` callback) is enumerated before AddRbac's
    /// TryAddSingleton(TimeProvider.System) — TryAdd is a no-op once a TimeProvider registration
    /// already exists.</summary>
    public PolarionMcpServerFactory WithFakeTime(FakeTimeProvider time) =>
        WithTestServices(services => services.AddSingleton<TimeProvider>(time));

    public IServiceProvider Services => GetOrBuildApp().Services;

    public HttpClient CreateClient() => GetOrBuildApp().GetTestClient();

    private WebApplication GetOrBuildApp()
    {
        if (_app is not null)
            return _app;

        var args = _environmentName is not null
            ? new[] { "--environment", _environmentName }
            : [];

        _app = Program.BuildApp(
            args,
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(_configValues);
                if (_configureServices is not null)
                    _configureServices(builder.Services);
            },
            builder =>
            {
                if (_postAuthConfigureServices is not null)
                    _postAuthConfigureServices(builder.Services);
            });

        _app.Start();
        return _app;
    }

    public void Dispose()
    {
        if (_app is null)
            return;

        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().GetAwaiter().GetResult();
    }
}
