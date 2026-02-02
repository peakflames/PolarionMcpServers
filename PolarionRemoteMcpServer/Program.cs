using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

// using Microsoft.Extensions.Hosting; // Not directly used for WebApplication
// using Microsoft.Extensions.Logging; // No longer directly used here, Serilog handles it
using Polarion;
using PolarionMcpTools; // Added for IPolarionClientFactory and PolarionClientFactory
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Endpoints;
using PolarionRemoteMcpServer.Services;
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

            // Configure JsonSerializerOptions to use the source generator contexts
            //
            builder.Services.Configure<JsonSerializerOptions>(options =>
            {
                // Ensure our source generator contexts are prioritized for JSON operations
                options.TypeInfoResolverChain.Insert(0, PolarionConfigJsonContext.Default);
                options.TypeInfoResolverChain.Insert(0, PolarionRestApiJsonContext.Default);
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

            // Add API key authentication for REST API endpoints
            //
            builder.Services.AddApiKeyAuthentication(builder.Configuration);

            // Add OpenAPI for REST API documentation
            // Note: OpenAPI requires its own JSON serializer options with reflection support for schema generation
            //
            builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info.Title = "Polarion MCP Server REST API";
                    document.Info.Version = "v1";
                    document.Info.Description = "REST API endpoints compatible with Polarion REST API format";

                    // Add security schemes to the document
                    document.Components ??= new();
                    document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
                    {
                        // API Key Authentication (header-based)
                        ["ApiKey"] = new()
                        {
                            Type = SecuritySchemeType.ApiKey,
                            In = ParameterLocation.Header,
                            Name = "X-API-Key",
                            Description = "API Key authentication. Obtain your API key from the system administrator."
                        }
                    };

                    // Apply security requirements globally
                    // This makes ALL endpoints require API Key auth by default in the documentation
                    document.SecurityRequirements =
                    [
                        new()
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new() { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                                },
                                new string[] { }
                            }
                        }
                    ];

                    return Task.CompletedTask;
                });
            });

            // Override the JSON options specifically for OpenAPI schema generation
            // This uses reflection-based serialization needed for schema generation
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                // Ensure the OpenAPI context is also available
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, PolarionRestApiJsonContext.Default);
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, PolarionConfigJsonContext.Default);
            });

            // Add the configurations and the factory to the DI container
            //
            builder.Services.AddSingleton(polarionProjects); // Register the list of project configurations
            builder.Services.AddScoped<IPolarionClientFactory, PolarionRemoteClientFactory>(); // For MCP endpoints (uses ProjectUrlAlias)
            builder.Services.AddScoped<RestApiProjectResolver>(); // For REST API endpoints (uses SessionConfig.ProjectId)

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

            // Add authentication and authorization middleware
            //
            app.UseApiKeyAuthentication();

            // SSE stream disconnection workaround for Cline/TypeScript MCP SDK (streamableHttp only)
            // The TypeScript MCP SDK has a bug where GET requests wait in a loop that can timeout.
            // This middleware intercepts GET requests to streamableHttp endpoints and sends a dummy response.
            // NOTE: This only applies to streamableHttp transport (GET /{projectId}), NOT legacy SSE (GET /{projectId}/sse)
            // See: https://github.com/cline/cline/issues/8367
            // See: https://github.com/modelcontextprotocol/typescript-sdk/issues/1211
            app.Use(async (context, next) =>
            {
                // Only intercept GET requests for streamableHttp transport
                // Exclude: /sse, /message, REST API, OpenAPI, Scalar, api/*, or root
                var path = context.Request.Path.Value;
                if (context.Request.Method == "GET" &&
                    path != null &&
                    !path.EndsWith("/sse") &&
                    !path.EndsWith("/message") &&
                    !path.StartsWith("/polarion/rest", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) &&
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

            // Get version info for logging
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";

            // Map OpenAPI and Scalar API documentation endpoints
            //
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("Polarion MCP Server REST API")
                    .WithTheme(ScalarTheme.DeepSpace)
                    .WithLayout(ScalarLayout.Modern)
                    .WithDarkMode(true)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
            Log.Information("Scalar API documentation available at /scalar/v1");

            // Map health and version endpoints
            //
            app.MapHealthEndpoints();
            Log.Information("Health endpoints mapped at /api/health and /api/version");

            // Map MCP endpoints
            //
            app.MapMcp("{projectId}");        // /{projectId}, /{projectId}/sse
            app.MapMcp("{projectId}/mcp");    // /{projectId}/mcp (streamable HTTP)

            // Map REST API endpoints (Polarion REST API compatible)
            //
            app.MapWorkItemsEndpoints();
            app.MapSpacesEndpoints();
            app.MapDocumentsEndpoints();
            Log.Information("REST API endpoints mapped at /polarion/rest/v1/projects/{{projectId}}/...");
            Log.Information("PolarionMcpServer v{Version} started successfully", version);

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
