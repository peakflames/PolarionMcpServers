

namespace PolarionMcpTools
{
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
    }
}
