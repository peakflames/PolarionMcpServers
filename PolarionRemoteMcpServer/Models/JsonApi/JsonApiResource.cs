using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// Base class for JSON:API resources.
/// </summary>
public class JsonApiResource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual object? Attributes { get; set; }

    [JsonPropertyName("relationships")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonApiRelationship>? Relationships { get; set; }

    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiLinks? Links { get; set; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiResourceMeta? Meta { get; set; }
}

/// <summary>
/// Metadata for a resource.
/// </summary>
public class JsonApiResourceMeta
{
    [JsonPropertyName("revision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Revision { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}
