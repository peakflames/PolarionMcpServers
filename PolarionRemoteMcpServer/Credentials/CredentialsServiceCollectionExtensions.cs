using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Credentials;

public static class CredentialsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the credential-resolution seam. Unlike <c>AddMcpAuth</c>/<c>AddRbac</c>, this is
    /// never a no-op: <see cref="SharedCredentialResolver"/> is always registered as the default,
    /// since it reproduces today's shared-service-account behavior exactly. Only when
    /// <c>Credentials:Mode</c> is explicitly <see cref="CredentialsOptions.HttpBrokerMode"/> does this
    /// swap in <see cref="HttpBrokerCredentialResolver"/>. Returns true when broker mode is active.
    /// </summary>
    public static bool AddCredentials(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<CredentialsOptions>()
            .Bind(builder.Configuration.GetSection(CredentialsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CredentialsOptions>, CredentialsOptionsValidator>();

        services.AddScoped<IUpstreamCredentialResolver, SharedCredentialResolver>();

        var mode = builder.Configuration.GetValue($"{CredentialsOptions.SectionName}:Mode", CredentialsOptions.SharedMode);
        if (!string.Equals(mode, CredentialsOptions.HttpBrokerMode, StringComparison.OrdinalIgnoreCase))
            return false;

        services.AddHttpClient<HttpBrokerCredentialResolver>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<CredentialsOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.BrokerTimeoutSeconds);
        });
        services.Replace(ServiceDescriptor.Scoped<IUpstreamCredentialResolver>(
            sp => sp.GetRequiredService<HttpBrokerCredentialResolver>()));

        return true;
    }
}
