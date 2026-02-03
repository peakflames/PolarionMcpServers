using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// JSON:API resource representing a Polarion WorkItem.
/// </summary>
public class WorkItemResource : JsonApiResource
{
    public WorkItemResource()
    {
        Type = "workitems";
    }

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new WorkItemAttributes? Attributes { get; set; }
}

/// <summary>
/// Attributes for a WorkItem resource.
/// </summary>
public class WorkItemAttributes
{
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("outlineNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutlineNumber { get; set; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Created { get; set; }

    [JsonPropertyName("updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Updated { get; set; }

    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    [JsonPropertyName("severity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Severity { get; set; }

    [JsonPropertyName("priority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Priority { get; set; }

    [JsonPropertyName("assignee")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Assignee { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? CustomFields { get; set; }
}

/// <summary>
/// JSON:API resource representing a WorkItem revision.
/// </summary>
public class WorkItemRevisionResource : JsonApiResource
{
    public WorkItemRevisionResource()
    {
        Type = "workitem_revisions";
    }

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new WorkItemRevisionAttributes? Attributes { get; set; }
}

/// <summary>
/// Attributes for a WorkItem revision resource.
/// </summary>
public class WorkItemRevisionAttributes
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Created { get; set; }

    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}
