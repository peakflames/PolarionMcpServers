using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polarion;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace PolarionRemoteMcpServer;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
public class Program
{

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public static async Task<int> Main(string[] args)
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

            // Get Polarion configuration from appsettings.json
            //
            var polarionConfigSection = builder.Configuration.GetSection("PolarionClientConfiguration");
            
            // Parse timeout value or use default
            int timeoutSeconds = 60; // Default value
            if (polarionConfigSection["TimeoutSeconds"] != null)
            {
                int.TryParse(polarionConfigSection["TimeoutSeconds"], out timeoutSeconds);
            }
            
            // Manually create the configuration object with required properties
            var polarionConfig = new PolarionClientConfiguration
            {
                ServerUrl = polarionConfigSection["ServerUrl"] ?? throw new InvalidOperationException("ServerUrl is required in configuration"),
                Username = polarionConfigSection["Username"] ?? throw new InvalidOperationException("Username is required in configuration"),
                Password = polarionConfigSection["Password"] ?? throw new InvalidOperationException("Password is required in configuration"),
                ProjectId = polarionConfigSection["ProjectId"] ?? throw new InvalidOperationException("ProjectId is required in configuration"),
                TimeoutSeconds = timeoutSeconds
            };

            Log.Information($"Establishing Connection to Polarion server {polarionConfig.ServerUrl}, " +
                            $"Logging in as {polarionConfig.Username}, " +
                            $"Project Id: {polarionConfig.ProjectId}, " +
                            $"Timeout: {polarionConfig.TimeoutSeconds} seconds");

            // Create the Polarion client
            //
            var polarionClientResult = await PolarionClient.CreateAsync(polarionConfig);
            if (polarionClientResult.IsFailed)
            {
                throw new Exception($"Failed to create Polarion client: {polarionClientResult.Errors.First()}");
            }

            var polarionClient = polarionClientResult.Value;

            // Add Serilog
            //
            builder.Services.AddSerilog();

            // Add the Polarion client to the DI container
            //
            builder.Services.AddSingleton<IPolarionClient>(polarionClient);

            // Add the McpServer to the DI container
            //
            builder.Services
                .AddMcpServer()
                .WithTools<PolarionMcpTools.McpTools>();

            // Build and Run the McpServer
            //
            Log.Information("Starting PolarionMcpServer...");
            var app = builder.Build();

            // Map MCP endpoints
            //
            app.MapMcp();

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
