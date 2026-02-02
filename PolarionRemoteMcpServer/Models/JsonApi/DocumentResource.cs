using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// JSON:API resource representing a Polarion Document/Module.
/// </summary>
public class DocumentResource : JsonApiResource
{
    public DocumentResource()
    {
        Type = "documents";
    }

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new DocumentAttributes? Attributes { get; set; }
}

/// <summary>
/// Attributes for a Document resource.
/// </summary>
public class DocumentAttributes
{
    [JsonPropertyName("moduleName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModuleName { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonPropertyName("homePageContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HomePageContent { get; set; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Created { get; set; }

    [JsonPropertyName("updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Updated { get; set; }

    [JsonPropertyName("updatedBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedBy { get; set; }

    [JsonPropertyName("moduleLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModuleLocation { get; set; }

    [JsonPropertyName("moduleFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModuleFolder { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? CustomFields { get; set; }
}

/// <summary>
/// JSON:API resource representing a Document revision.
/// </summary>
public class DocumentRevisionResource : JsonApiResource
{
    public DocumentRevisionResource()
    {
        Type = "document_revisions";
    }

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new DocumentRevisionAttributes? Attributes { get; set; }
}

/// <summary>
/// Attributes for a Document revision resource.
/// </summary>
public class DocumentRevisionAttributes
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
}
