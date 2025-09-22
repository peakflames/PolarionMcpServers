using System.Diagnostics.CodeAnalysis;
using System.Text.Json; // For JsonSerializerOptions

// using Microsoft.Extensions.Hosting; // Not directly used for WebApplication
// using Microsoft.Extensions.Logging; // No longer directly used here, Serilog handles it
using Polarion;
using PolarionMcpTools; // Added for IPolarionClientFactory and PolarionClientFactory
using Serilog;
using Microsoft.Extensions.Configuration;

namespace PolarionRemoteMcpServer;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
public class Program
{

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public static int Main(string[] args)
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose() // Capture all log levels
                            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "PolarionMcpServer_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();


            // Create the DI container
            //
            var builder = WebApplication.CreateBuilder(args);
            
            // Add to support the polarion client factory access to the route data
            //
            builder.Services.AddHttpContextAccessor();

            // Configure JsonSerializerOptions to use the source generator context
            //
            builder.Services.Configure<JsonSerializerOptions>(options =>
            {
                // Ensure our source generator context is prioritized for JSON operations
                options.TypeInfoResolverChain.Insert(0, PolarionConfigJsonContext.Default);
            });


            // Get the entire application configuration from appsettings.json using source generation context
            //
            var appConfig = builder.Configuration.Get<PolarionAppConfig>() ??
                            throw new InvalidOperationException("Application configuration (PolarionAppConfig) is missing or invalid.");

            var polarionProjects = appConfig.PolarionProjects ?? 
                                   throw new InvalidOperationException("PolarionProjects configuration section is missing or invalid within PolarionAppConfig.");
            
            // Validate the loaded project configurations
            //
            if (!polarionProjects.Any())
            {
                throw new InvalidOperationException("No Polarion projects configured in PolarionProjects section.");
            }
            if (polarionProjects.Count(p => p.Default) > 1)
            {
                throw new InvalidOperationException("Multiple Polarion projects are marked as Default. Only one can be default.");
            }

            // Log information about loaded projects
            //
            Log.Information("Loaded {Count} Polarion project configurations.", polarionProjects.Count);
            foreach(var proj in polarionProjects)
            {
                Log.Information(" - Project Alias: {Alias}, Server: {Server}, Default: {IsDefault}", 
                    proj.ProjectUrlAlias, proj.SessionConfig!.ServerUrl, proj.Default);
            }
            

            // Add Serilog
            //
            builder.Services.AddSerilog();

            // Add the configurations and the factory to the DI container
            //
            builder.Services.AddSingleton(polarionProjects); // Register the list of project configurations
            builder.Services.AddScoped<IPolarionClientFactory, PolarionRemoteClientFactory>();

            // Add the McpServer to the DI container
            //
            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<PolarionMcpTools.McpTools>();

            // Build and Run the McpServer
            //
            Log.Information("Starting PolarionMcpServer...");
            var app = builder.Build();

            // Map MCP endpoints
            //
            app.MapMcp("{projectId}");

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log.Fatal($"Host terminated unexpectedly. Exception: {ex}");
            Console.ResetColor();
            return 1;
        }
    }
}
