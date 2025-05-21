using System.Collections.Generic;

namespace PolarionMcpTools
{
    /// <summary>
    /// Represents the root configuration for the Polarion MCP application,
    /// encompassing both project-specific settings and global WorkItem type definitions.
    /// </summary>
    public class PolarionAppConfig
    {
        /// <summary>
        /// Gets or sets the list of Polarion project configurations.
        /// Each configuration defines settings for a specific Polarion project instance.
        /// </summary>
        public List<PolarionProjectConfig>? PolarionProjects { get; set; }
    }
}
