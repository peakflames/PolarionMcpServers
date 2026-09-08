using Microsoft.Extensions.Options;
using PolarionRemoteMcpServer.Auth;

namespace PolarionRemoteMcpServer.Credentials;

/// <summary>
/// Mirrors <c>RbacOptionsValidator</c>: fires only when <c>Mode</c> is <see cref="CredentialsOptions.HttpBrokerMode"/>,
/// since <see cref="CredentialsOptions.SharedMode"/> (the default) reproduces today's behavior
/// exactly and must never be broken by a malformed section.
/// </summary>
public sealed class CredentialsOptionsValidator : IValidateOptions<CredentialsOptions>
{
    private readonly IOptions<McpAuthOptions> _mcpAuthOptions;

    public CredentialsOptionsValidator(IOptions<McpAuthOptions> mcpAuthOptions)
    {
        _mcpAuthOptions = mcpAuthOptions;
    }

    public ValidateOptionsResult Validate(string? name, CredentialsOptions options)
    {
        if (!string.Equals(options.Mode, CredentialsOptions.HttpBrokerMode, StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (!_mcpAuthOptions.Value.Enabled)
        {
            failures.Add(
                "Credentials:Mode=HttpBroker requires McpAuth:Enabled — the broker resolves a " +
                "per-user credential from the caller's validated 'sub' claim, which does not exist " +
                "without authentication.");
        }

        if (string.IsNullOrWhiteSpace(options.BrokerUrl))
            failures.Add("Credentials:BrokerUrl is required when Credentials:Mode is HttpBroker.");

        if (string.IsNullOrWhiteSpace(options.BrokerApiKey))
            failures.Add("Credentials:BrokerApiKey is required when Credentials:Mode is HttpBroker.");

        if (options.BrokerTimeoutSeconds < 1 || options.BrokerTimeoutSeconds > 120)
            failures.Add("Credentials:BrokerTimeoutSeconds must be between 1 and 120.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
