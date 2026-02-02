using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PolarionRemoteMcpServer.Models.JsonApi;
using Serilog;

namespace PolarionRemoteMcpServer.Authentication;

/// <summary>
/// Options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The HTTP header name for the API key.
    /// </summary>
    public const string HeaderName = "X-API-Key";
}

/// <summary>
/// Authentication handler that validates API keys against configured consumers.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ApiConsumersConfig _consumersConfig;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        ApiConsumersConfig consumersConfig)
        : base(options, loggerFactory, encoder)
    {
        _consumersConfig = consumersConfig;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key header
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeaderValues))
        {
            Log.Debug("API Key authentication: No {Header} header present", ApiKeyAuthenticationOptions.HeaderName);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            Log.Debug("API Key authentication: Empty API key provided");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Find matching consumer by API key
        var matchingConsumer = _consumersConfig.Consumers
            .FirstOrDefault(kvp => kvp.Value.ApplicationKey == providedApiKey);

        if (matchingConsumer.Key == null)
        {
            Log.Warning("API Key authentication: Invalid API key attempted");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var consumerId = matchingConsumer.Key;
        var consumer = matchingConsumer.Value;

        // Check if consumer is active
        if (!consumer.Active)
        {
            Log.Warning("API Key authentication: Inactive consumer '{ConsumerId}' attempted to authenticate", consumerId);
            return Task.FromResult(AuthenticateResult.Fail("Consumer is inactive"));
        }

        Log.Information("API Key authentication: Consumer '{ConsumerId}' ({Name}) authenticated successfully",
            consumerId, consumer.Name);

        // Build claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, consumerId),
            new Claim(ClaimTypes.Name, consumer.Name),
            new Claim("consumer_id", consumerId)
        };

        // Add scope claims
        foreach (var scope in consumer.AllowedScopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.ContentType = "application/vnd.api+json";

        var errorResponse = new JsonApiDocument<object>
        {
            Errors = new List<JsonApiError>
            {
                new JsonApiError
                {
                    Status = "401",
                    Title = "Unauthorized",
                    Detail = "Valid API key required. Provide the API key in the X-API-Key header."
                }
            }
        };

        await Response.WriteAsJsonAsync(errorResponse, PolarionRestApiJsonContext.Default.JsonApiDocumentObject);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        Response.ContentType = "application/vnd.api+json";

        var errorResponse = new JsonApiDocument<object>
        {
            Errors = new List<JsonApiError>
            {
                new JsonApiError
                {
                    Status = "403",
                    Title = "Forbidden",
                    Detail = "You do not have permission to access this resource."
                }
            }
        };

        await Response.WriteAsJsonAsync(errorResponse, PolarionRestApiJsonContext.Default.JsonApiDocumentObject);
    }
}
