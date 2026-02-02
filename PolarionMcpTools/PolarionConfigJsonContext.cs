using System.Text.Json.Serialization;

namespace PolarionMcpTools;

/// <summary>
/// Provides source-generated JSON serialization metadata for configuration types,
/// enabling efficient binding in trimmed or AOT-compiled applications.
/// </summary>
[JsonSerializable(typeof(PolarionAppConfig))]             // Added for the new root config type
[JsonSerializable(typeof(List<PolarionProjectConfig>))]
[JsonSerializable(typeof(PolarionProjectConfig))]
[JsonSerializable(typeof(PolarionClientConfiguration))]
[JsonSerializable(typeof(List<ArtifactCustomFieldConfig>))]
[JsonSerializable(typeof(ArtifactCustomFieldConfig))]
public partial class PolarionConfigJsonContext : JsonSerializerContext
{
}

// Note: JSON:API types for REST endpoints are registered in PolarionRemoteMcpServer.PolarionRestApiJsonContext

