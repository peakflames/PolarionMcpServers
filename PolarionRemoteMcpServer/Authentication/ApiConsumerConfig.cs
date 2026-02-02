namespace PolarionRemoteMcpServer.Authentication;

/// <summary>
/// Configuration for an individual API consumer.
/// </summary>
public class ApiConsumerConfig
{
    /// <summary>
    /// Display name for this API consumer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The API key/application key for authentication.
    /// </summary>
    public string ApplicationKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether this consumer is currently active and allowed to authenticate.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// List of scopes this consumer is allowed to access (e.g., "polarion:read").
    /// </summary>
    public List<string> AllowedScopes { get; set; } = new();

    /// <summary>
    /// Optional description of this API consumer.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Root configuration for all API consumers.
/// </summary>
public class ApiConsumersConfig
{
    /// <summary>
    /// Dictionary of consumer ID to consumer configuration.
    /// </summary>
    public Dictionary<string, ApiConsumerConfig> Consumers { get; set; } = new();
}
