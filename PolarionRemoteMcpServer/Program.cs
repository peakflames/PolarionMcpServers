using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

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

            // SSE stream disconnection workaround for Cline/TypeScript MCP SDK (streamableHttp only)
            // The TypeScript MCP SDK has a bug where GET requests wait in a loop that can timeout.
            // This middleware intercepts GET requests to streamableHttp endpoints and sends a dummy response.
            // NOTE: This only applies to streamableHttp transport (GET /{projectId}), NOT legacy SSE (GET /{projectId}/sse)
            // See: https://github.com/cline/cline/issues/8367
            // See: https://github.com/modelcontextprotocol/typescript-sdk/issues/1211
            app.Use(async (context, next) =>
            {
                // Only intercept GET requests for streamableHttp transport (NOT /sse or /message endpoints)
                var path = context.Request.Path.Value;
                if (context.Request.Method == "GET" &&
                    path != null &&
                    !path.EndsWith("/sse") &&
                    !path.EndsWith("/message") &&
                    !path.Equals("/", StringComparison.Ordinal))
                {
                    Log.Debug("StreamableHttp workaround: Intercepting GET {Path}", context.Request.Path);

                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers.Connection = "keep-alive";

                    // Use a hardcoded JSON string to avoid reflection-based serialization issues in AOT
                    const string fakeResponseJson = """{"id":0,"jsonrpc":"2.0","result":{}}""";
                    await context.Response.WriteAsync($"event: message\ndata: {fakeResponseJson}\n\n");
                    return; // Short-circuit, don't call next middleware
                }

                await next();
            });

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
