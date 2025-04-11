using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polarion;
using Serilog;

namespace PolarionMcpServer;

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
                            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "TestServer_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();


            var filePath = Path.Combine(AppContext.BaseDirectory, "polarion-mcp.config.json");
            Log.Information($"Loading configuration from {filePath}");
            if (!File.Exists(filePath))
            {
                Log.Error($"Failed to find configuration file at {filePath}");
                return 1;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<PolarionClientConfiguration>(json);
            if (config is null)
            {
                Log.Error("Failed to load configuration");
                return 1;
            }


            // Establish connection to Polarion server
            //
            Log.Information($"Establishiing Connection to Polarion server {config.ServerUrl}, " +
                            $"Logging in as {config.Username}, " +
                            $"Project Id: {config.ProjectId}, " +
                            $"Timeout: {config.TimeoutSeconds} seconds");

            var polarionConfig = new PolarionClientConfiguration
            {
                ServerUrl = config.ServerUrl,
                Username = config.Username,
                Password = config.Password,
                ProjectId = config.ProjectId,
                TimeoutSeconds = config.TimeoutSeconds
            };

            // Create the Polarion client
            //
            var polarionClientResult = await PolarionClient.CreateAsync(polarionConfig);
            if (polarionClientResult.IsFailed)
            {
                throw new Exception($"Failed to create Polarion client: {polarionClientResult.Errors.First()}");
            }

            var polarionClient = polarionClientResult.Value;

            // Create the DI container
            //
            var builder = Host.CreateApplicationBuilder(args);

            // Add Serilog
            //
            builder.Services.AddSerilog();

            // Add the Polarion client to the DI container
            //
            builder.Services.AddSingleton<IPolarionClient>(polarionClient);

            // Add the McpServer to the DI container
            //
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<PolarionMcpTools.McpTools>();

            // Build and Run the McpServer
            //
            Log.Information("Starting PolarionMcpServer...");
            await builder.Build().RunAsync();
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
