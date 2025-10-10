using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polarion;
using PolarionMcpTools;
using Serilog;


namespace PolarionMcpServer;

[RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public static int Main(string[] args)
    {
        try
        {
            // Parse command line arguments to get the project alias
            string? projectAlias = null;
            string? configPath = null;
            
            // Simple command line parsing for --project or -p argument
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--project" || args[i] == "-p") && i + 1 < args.Length)
                {
                    projectAlias = args[i + 1];
                }
                else if (args[i].StartsWith("--project="))
                {
                    projectAlias = args[i].Substring("--project=".Length);
                }
                else if (args[i].StartsWith("-p="))
                {
                    projectAlias = args[i].Substring("-p=".Length);
                }
                else if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
                {
                    configPath = args[i + 1];
                }
                else if (args[i].StartsWith("--config="))
                {
                    configPath = args[i].Substring("--config=".Length);
                }
                else if (args[i].StartsWith("-c="))
                {
                    configPath = args[i].Substring("-c=".Length);
                }
            }

            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose() // Capture all log levels
                            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "polarion_mcp_stdio_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();

            // Report application version as early as possible to console and log
            try
            {
                var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string appName = entry?.GetName().Name ?? "polarion-mcp";
                string version = "unknown";
                // Prefer informational version, then assembly version, then file version
                var infoAttr = entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(infoAttr))
                {
                    version = infoAttr!;
                }
                else if (entry?.GetName().Version != null)
                {
                    version = entry.GetName().Version!.ToString()!;
                }
                else
                {
                    // Fallback to AssemblyFileVersionAttribute if available (works in single-file publish)
                    var fileVer = entry?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
                    if (!string.IsNullOrEmpty(fileVer))
                    {
                        version = fileVer!;
                    }
                }

                var versionLine = $"{appName} {version}";
                Console.WriteLine(versionLine);
                Log.Information("Starting {AppName} version {Version}", appName, version);
            }
            catch (Exception ex)
            {
                // If version detection fails, log but continue starting
                Log.Warning(ex, "Failed to detect or report application version at startup.");
            }

            if (projectAlias != null)
            {
                Log.Information("Using project alias from command line: {ProjectAlias}", projectAlias);
            }

            // Create the DI container
            //
            var builder = Host.CreateApplicationBuilder(args);

            // Make environment variables available to the configuration binder as well.
            // This allows overrides like PolarionProjects__0__SessionConfig__Password if you prefer
            // to use the built-in configuration binding naming convention.
            builder.Configuration.AddEnvironmentVariables();

            if (!string.IsNullOrEmpty(configPath))
            {
                builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);
                Log.Information("Using configuration file from command line: {ConfigFilePath}", configPath);
            }
 
            // Configure JsonSerializerOptions to use the source generator context
            //
            builder.Services.Configure<JsonSerializerOptions>(options =>
            {
                // Ensure our source generator context is prioritized for JSON operations
                options.TypeInfoResolverChain.Insert(0, PolarionConfigJsonContext.Default);
            });


            // print the entire file path to the app setting file that is being used by the builder.Configuration
            var fileProvider = builder.Configuration.GetFileProvider();
            if (fileProvider == null)
            {
                Log.Warning("Configuration file provider is null. Cannot determine configuration file path.");
            }
            else
            {
                var fileInfo = fileProvider.GetFileInfo("appsettings.json");
                if (fileInfo == null || !fileInfo.Exists)
                {
                    var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    Log.Warning("Configuration file 'appsettings.json' not found. Expected at: {ExpectedPath}", expectedPath);
                }
                else
                {
                    Log.Information("Using configuration file: {ConfigFilePath}", fileInfo.PhysicalPath);
                }
            }
 
            // Get the entire application configuration from appsettings.json using source generation context
            //
            var appConfig = builder.Configuration.Get<PolarionAppConfig>() ??
                            throw new InvalidOperationException("Application configuration (PolarionAppConfig) is missing or invalid.");

            if (appConfig.PolarionProjects is null)
            {
                // for debugging we need to read the entire config file as raw test and print it to the log
                 Log.Information("PolarionAppConfig: {PolarionAppConfig}", JsonSerializer.Serialize(appConfig, PolarionConfigJsonContext.Default.PolarionAppConfig));
            }

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
            

            
            // Allow overriding passwords via environment variables.
            // Supported env var names:
            //  - POLARION_{ALIAS}_PASSWORD  (alias normalized to [A-Z0-9_] )
            //  - POLARION_PASSWORD  (applies to the project marked Default)
            try
            {
                foreach (var proj in polarionProjects)
                {
                    if (proj == null) continue;
                    var alias = proj.ProjectUrlAlias ?? string.Empty;
                    // Normalize alias to upper-case letters, digits and underscores
                    var norm = Regex.Replace(alias, "[^A-Za-z0-9]", "_").ToUpperInvariant();
                    var envName = $"POLARION_{norm}_PASSWORD";
                    var envVal = Environment.GetEnvironmentVariable(envName);
                    if (string.IsNullOrEmpty(envVal) && proj.Default)
                    {
                        envVal = Environment.GetEnvironmentVariable("POLARION_PASSWORD");
                        envName = "POLARION_PASSWORD";
                    }

                    if (!string.IsNullOrEmpty(envVal))
                    {
                        if (proj.SessionConfig != null)
                        {
                            proj.SessionConfig.Password = envVal;
                            Log.Information("Overrode SessionConfig.Password for project '{ProjectAlias}' from env var '{EnvVarName}'", alias, envName);
                        }
                        else
                        {
                            Log.Warning("Found environment variable '{EnvVarName}' but SessionConfig is null for project '{ProjectAlias}'. Password override skipped.", envName, alias);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while attempting to override Polarion passwords from environment variables.");
            }

            
            // Add Serilog
            //
            builder.Services.AddSerilog();

            // Add the configurations and the factory to the DI container
            //
            builder.Services.AddSingleton(polarionProjects); // Register the list of project configurations
            
            // Register the factory with the command line project alias
            builder.Services.AddScoped<IPolarionClientFactory>(sp => 
                new PolarionStdioClientFactory(
                    polarionProjects,
                    sp.GetRequiredService<ILogger<PolarionStdioClientFactory>>(),
                    projectAlias
                )
            );

            // Add the McpServer to the DI container
            //
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<PolarionMcpTools.McpTools>();

            // Build and Run the McpServer
            //
            // Log.Information("Starting PolarionMcpServer...");
            builder.Build().Run();
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
