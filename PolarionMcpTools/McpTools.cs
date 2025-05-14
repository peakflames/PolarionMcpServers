using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider and CreateAsyncScope
using ModelContextProtocol.Server;
using Polarion;
using Polarion.Generated.Tracker;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System; // Added for IServiceProvider

namespace PolarionMcpTools;

public sealed partial class McpTools
{
    private readonly IServiceProvider _serviceProvider;

    public McpTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

}
