
using System.Collections.Generic;

namespace PolarionMcpTools
{
    /// <summary>
    /// Represents the configuration for a specific artifact type's custom fields.
    /// </summary>
    public class ArtifactCustomFieldConfig
    {
        /// <summary>
        /// The ID or type of the artifact (e.g., "requirement", "testcase").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// A list of custom field names to be retrieved for this artifact type.
        /// </summary>
        public List<string> Fields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the configuration for a single Polarion project instance
    /// defined in the application settings.
    /// </summary>
    public class PolarionProjectConfig
    {
        /// <summary>
        /// An alias or identifier for this project configuration, 
        /// typically matching the route parameter used to select it.
        /// </summary>
        public string ProjectUrlAlias { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this configuration should be used when no specific 
        /// project ID is provided in the request route. Only one configuration
        /// should be marked as default.
        /// </summary>
        public bool Default { get; set; } = false;

        /// <summary>
        /// Contains the actual connection details (ServerUrl, Username, Password, etc.)
        /// for this Polarion instance. This property name must match the JSON key ("SessionConfig").
        /// This property is required and expected to be populated by the configuration binder.
        /// </summary>
        public PolarionClientConfiguration? SessionConfig { get; set; }

        /// <summary>
        /// A string pattern used to filter out spaces that contain this string.
        /// If null or empty, no filtering is applied.
        /// </summary>
        public string? BlacklistSpaceContainingMatch { get; set; }

        /// <summary>
        /// Gets or sets the list of WorkItem type configurations specific to this project.
        /// Each configuration defines custom fields to be retrieved for a specific WorkItem type.
        /// </summary>
        public List<ArtifactCustomFieldConfig>? PolarionWorkItemTypes { get; set; }

        /// <summary>
        /// Gets or sets the default list of WorkItem fields (standard and custom) to be retrieved
        /// when no specific fields are requested by the user.
        /// </summary>
        public List<string>? PolarionWorkItemDefaultFields { get; set; }

        /// <summary>
        /// Gets or sets the prefix to be used when creating a Polarion WorkItem.
        /// If null or empty, no prefix will be used.
        public string? WorkItemPrefix { get; set; }
    }
}
