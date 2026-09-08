using System.Net.Http.Json;
using System.Security.Claims;
using FluentResults;
using Microsoft.Extensions.Options;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Credentials;

/// <summary>Wire contract with the (not-yet-built) external broker component. Carries no secret —
/// <see cref="Subject"/> is the caller's opaque `sub` claim, never their JWT.</summary>
public sealed record BrokerCredentialRequest(string Subject, string ProjectAlias);

/// <summary>Wire contract with the (not-yet-built) external broker component. Deliberately
/// `sealed class`, never `record` — <see cref="Secret"/> is a real credential value.</summary>
public sealed class BrokerCredentialResponse
{
    public PolarionCredentialKind Kind { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    public override string ToString() =>
        $"BrokerCredentialResponse {{ Kind = {Kind}, Username = {Username}, Secret = [REDACTED] }}";
}

/// <summary>
/// Active only when <c>Credentials:Mode</c> is <c>"HttpBroker"</c> — off by default. Calls a
/// configured external broker endpoint with this server's own <c>Credentials:BrokerApiKey</c> and
/// the caller's validated `sub` claim; the caller's own JWT is never forwarded. The broker itself —
/// the enrollment UI, the credential vault, the identity binding — is a separate component this
/// public repo does not implement; this class is only the generic HTTP client for it.
/// </summary>
public sealed class HttpBrokerCredentialResolver : IUpstreamCredentialResolver
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<CredentialsOptions> _options;
    private readonly ILogger<HttpBrokerCredentialResolver> _logger;

    public HttpBrokerCredentialResolver(
        HttpClient httpClient, IOptions<CredentialsOptions> options, ILogger<HttpBrokerCredentialResolver> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<PolarionSessionCredential>> ResolveAsync(
        PolarionProjectConfig projectConfig, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        var subject = user?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            _logger.LogWarning(
                "HttpBrokerCredentialResolver: no 'sub' claim on the caller; cannot resolve a per-user credential for project '{Alias}'.",
                projectConfig.ProjectUrlAlias);
            return Result.Fail("No authenticated subject to resolve an upstream credential for.");
        }

        var options = _options.Value;
        var brokerRequest = new BrokerCredentialRequest(subject, projectConfig.ProjectUrlAlias);

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.BrokerUrl)
            {
                Content = JsonContent.Create(brokerRequest, PolarionRestApiJsonContext.Default.BrokerCredentialRequest),
            };
            httpRequest.Headers.Add("X-API-Key", options.BrokerApiKey);
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HttpBrokerCredentialResolver: request to broker failed for project '{Alias}'.", projectConfig.ProjectUrlAlias);
            return Result.Fail($"Broker request failed: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "HttpBrokerCredentialResolver: broker returned {StatusCode} for project '{Alias}'.",
                (int)response.StatusCode, projectConfig.ProjectUrlAlias);
            return Result.Fail($"Broker returned status {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync(PolarionRestApiJsonContext.Default.BrokerCredentialResponse, cancellationToken);
        if (body is null || string.IsNullOrEmpty(body.Username) || string.IsNullOrEmpty(body.Secret))
        {
            _logger.LogWarning("HttpBrokerCredentialResolver: broker returned an incomplete credential for project '{Alias}'.", projectConfig.ProjectUrlAlias);
            return Result.Fail("Broker returned an incomplete credential.");
        }

        return Result.Ok(new PolarionSessionCredential
        {
            Kind = body.Kind,
            Username = body.Username,
            Secret = body.Secret,
        });
    }
}
