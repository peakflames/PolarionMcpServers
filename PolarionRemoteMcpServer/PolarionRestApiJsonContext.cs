using System.Text.Json.Serialization;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Endpoints;
using PolarionRemoteMcpServer.Models.JsonApi;

namespace PolarionRemoteMcpServer;

/// <summary>
/// Provides source-generated JSON serialization metadata for REST API types,
/// enabling efficient binding in trimmed or AOT-compiled applications.
/// </summary>
// Core JSON:API types
[JsonSerializable(typeof(JsonApiLinks))]
[JsonSerializable(typeof(JsonApiMeta))]
[JsonSerializable(typeof(JsonApiError))]
[JsonSerializable(typeof(JsonApiErrorSource))]
[JsonSerializable(typeof(JsonApiRelationship))]
[JsonSerializable(typeof(JsonApiResourceIdentifier))]
[JsonSerializable(typeof(JsonApiResource))]
[JsonSerializable(typeof(JsonApiResourceMeta))]

// WorkItem types
[JsonSerializable(typeof(WorkItemResource))]
[JsonSerializable(typeof(WorkItemAttributes))]
[JsonSerializable(typeof(WorkItemRevisionResource))]
[JsonSerializable(typeof(WorkItemRevisionAttributes))]
[JsonSerializable(typeof(List<WorkItemResource>))]
[JsonSerializable(typeof(List<WorkItemRevisionResource>))]
[JsonSerializable(typeof(JsonApiDocument<WorkItemResource>))]
[JsonSerializable(typeof(JsonApiDocument<List<WorkItemResource>>))]
[JsonSerializable(typeof(JsonApiDocument<List<WorkItemRevisionResource>>))]

// Document types
[JsonSerializable(typeof(DocumentResource))]
[JsonSerializable(typeof(DocumentAttributes))]
[JsonSerializable(typeof(DocumentRevisionResource))]
[JsonSerializable(typeof(DocumentRevisionAttributes))]
[JsonSerializable(typeof(List<DocumentResource>))]
[JsonSerializable(typeof(List<DocumentRevisionResource>))]
[JsonSerializable(typeof(JsonApiDocument<DocumentResource>))]
[JsonSerializable(typeof(JsonApiDocument<List<DocumentResource>>))]
[JsonSerializable(typeof(JsonApiDocument<List<DocumentRevisionResource>>))]

// Space types
[JsonSerializable(typeof(SpaceResource))]
[JsonSerializable(typeof(SpaceAttributes))]
[JsonSerializable(typeof(List<SpaceResource>))]
[JsonSerializable(typeof(JsonApiDocument<List<SpaceResource>>))]

// Error response type
[JsonSerializable(typeof(JsonApiDocument<object>))]
[JsonSerializable(typeof(List<JsonApiError>))]

// Health endpoint types
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(string))]

// Common nullable types used in query parameters
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(int))]

// Authentication configuration types
[JsonSerializable(typeof(ApiConsumerConfig))]
[JsonSerializable(typeof(ApiConsumersConfig))]
[JsonSerializable(typeof(Dictionary<string, ApiConsumerConfig>))]
[JsonSerializable(typeof(List<string>))]
public partial class PolarionRestApiJsonContext : JsonSerializerContext
{
}
