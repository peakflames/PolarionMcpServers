using System.Text.Json.Serialization;
using System.Collections.Generic; // Required for List<>
using PolarionMcpTools; // For PolarionProjectConfig, PolarionClientConfiguration
using Polarion; // For PolarionClientConfiguration if it's in this namespace

namespace PolarionRemoteMcpServer
{
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
    internal partial class PolarionConfigJsonContext : JsonSerializerContext
    {
    }
}
