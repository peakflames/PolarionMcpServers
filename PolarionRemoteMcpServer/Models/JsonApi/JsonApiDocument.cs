using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// Represents a JSON:API document wrapper with data, links, included, and meta fields.
/// </summary>
/// <typeparam name="T">The type of the primary data (single resource or array of resources).</typeparam>
public class JsonApiDocument<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiLinks? Links { get; set; }

    [JsonPropertyName("included")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonApiResource>? Included { get; set; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiMeta? Meta { get; set; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonApiError>? Errors { get; set; }
}

/// <summary>
/// Represents links within a JSON:API document or resource.
/// </summary>
public class JsonApiLinks
{
    [JsonPropertyName("self")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Self { get; set; }

    [JsonPropertyName("first")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? First { get; set; }

    [JsonPropertyName("last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Last { get; set; }

    [JsonPropertyName("prev")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prev { get; set; }

    [JsonPropertyName("next")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Next { get; set; }
}

/// <summary>
/// Represents metadata in a JSON:API document.
/// </summary>
public class JsonApiMeta
{
    [JsonPropertyName("totalCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Represents an error in a JSON:API document.
/// </summary>
public class JsonApiError
{
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiErrorSource? Source { get; set; }
}

/// <summary>
/// Represents the source of an error.
/// </summary>
public class JsonApiErrorSource
{
    [JsonPropertyName("pointer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pointer { get; set; }

    [JsonPropertyName("parameter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parameter { get; set; }
}

/// <summary>
/// Represents a relationship in a JSON:API resource.
/// </summary>
public class JsonApiRelationship
{
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonApiLinks? Links { get; set; }
}

/// <summary>
/// Represents a resource identifier for relationships.
/// </summary>
public class JsonApiResourceIdentifier
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
