using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// Metadata specific to work item search results.
/// </summary>
public class WorkItemSearchMeta : JsonApiMeta
{
    [JsonPropertyName("query")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Query { get; set; }

    [JsonPropertyName("luceneQuery")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LuceneQuery { get; set; }

    [JsonPropertyName("typeFilter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeFilter { get; set; }

    [JsonPropertyName("statusFilter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusFilter { get; set; }
}
