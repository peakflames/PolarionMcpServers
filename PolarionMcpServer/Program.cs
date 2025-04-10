using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polarion;

namespace PolarionMcpServer;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "polarion-mcp.config.json");
            Console.WriteLine($"Loading configuration from {filePath}");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Failed to find configuration file at {filePath}");
                return 1;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<PolarionClientConfiguration>(json);
            if (config is null)
            {
                Console.WriteLine("Failed to load configuration");
                return 1;
            }


            // Establish connection to Polarion server
            //
            Console.WriteLine($"Establishiing Connection to Polarion server {config.ServerUrl}");
            Console.WriteLine($"\tLogging in as {config.Username}");
            Console.WriteLine($"\tProject Id: {config.ProjectId}");
            Console.WriteLine($"\tTimeout: {config.TimeoutSeconds} seconds");
            Console.WriteLine();

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

            // Add the Polarion client to the DI container
            //
            builder.Services.AddSingleton<IPolarionClient>(polarionClient);

            // Add the McpServer to the DI container
            //
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<PolarionMcpTools.McpTools>();

            // Add the console logger
            //
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
           

            // Build and Run the McpServer
            //
            Console.WriteLine("Starting PolarionMcpServer...");
            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Host terminated unexpectedly. Exception: {ex}");
            Console.ResetColor();
            return 1;
        }
    }
}
