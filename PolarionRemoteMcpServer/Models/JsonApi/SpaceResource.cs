using System.Text.Json.Serialization;

namespace PolarionRemoteMcpServer.Models.JsonApi;

/// <summary>
/// JSON:API resource representing a Polarion Space.
/// </summary>
public class SpaceResource : JsonApiResource
{
    public SpaceResource()
    {
        Type = "spaces";
    }

    [JsonPropertyName("attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new SpaceAttributes? Attributes { get; set; }
}

/// <summary>
/// Attributes for a Space resource.
/// </summary>
public class SpaceAttributes
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
