#!/usr/bin/env python3
"""
Build script for PolarionMcpServers
Supports: build, run, start, stop, status, mcp commands
"""

import sys
import os

# Fix Unicode output on Windows
if sys.platform == 'win32':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
import platform
import subprocess
import signal
import time
import json
from pathlib import Path
from typing import Optional

try:
    import psutil
except ImportError:
    print("Error: psutil is required. Install with: pip install psutil")
    sys.exit(1)

# Optional: fastmcp for MCP client functionality
FASTMCP_AVAILABLE = False
try:
    import asyncio
    from fastmcp import Client as McpClient
    FASTMCP_AVAILABLE = True
except ImportError:
    pass

# Project configuration
SOLUTION_PATH = "PolarionMcpServers.sln"
PROJECT_PATH = "PolarionRemoteMcpServer/PolarionRemoteMcpServer.csproj"
PID_FILE = Path(".polarion-mcp.pid")
LOG_FILE = Path("polarion-mcp.log")
DEV_PORT = 5090


def is_process_running(pid: int) -> bool:
    """Check if a process with given PID is running"""
    try:
        process = psutil.Process(pid)
        return process.is_running() and process.status() != psutil.STATUS_ZOMBIE
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return False


def get_running_pid() -> Optional[int]:
    """Get PID of running application if it exists"""
    if not PID_FILE.exists():
        return None
    
    try:
        with open(PID_FILE, 'r') as f:
            pid = int(f.read().strip())
        
        if is_process_running(pid):
            return pid
        else:
            # Clean up stale PID file
            PID_FILE.unlink()
            return None
    except (ValueError, IOError):
        return None


def check_status() -> None:
    """Check if application is running"""
    pid = get_running_pid()
    
    if pid:
        print(f"✓ Application is running (PID: {pid})")
        print(f"  URL: http://localhost:{DEV_PORT}")
        print(f"  MCP: http://localhost:{DEV_PORT}/mcp")
        print(f"  API Docs: http://localhost:{DEV_PORT}/scalar/v1")
        print(f"  Log file: {LOG_FILE}")
    else:
        print("✗ Application is not running")


def start_background() -> None:
    """Start application in background"""
    # Check if already running
    existing_pid = get_running_pid()
    if existing_pid:
        print(f"Application is already running (PID: {existing_pid})")
        print("Use 'python build.py stop' to stop it first")
        return
    
    print("Starting application in background...")
    
    # Open log file
    log_file = open(LOG_FILE, 'w')
    
    # Set environment for Development mode
    env = os.environ.copy()
    env["ASPNETCORE_ENVIRONMENT"] = "Development"
    
    # Start process
    if platform.system() == "Windows":
        # On Windows, use CREATE_NEW_PROCESS_GROUP to prevent Ctrl+C propagation
        process = subprocess.Popen(
            ["dotnet", "run", "--project", PROJECT_PATH, "--no-restore"],
            stdout=log_file,
            stderr=subprocess.STDOUT,
            env=env,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP
        )
    else:
        # On Unix, use start_new_session
        process = subprocess.Popen(
            ["dotnet", "run", "--project", PROJECT_PATH, "--no-restore"],
            stdout=log_file,
            stderr=subprocess.STDOUT,
            env=env,
            start_new_session=True
        )
    
    # Save PID
    with open(PID_FILE, 'w') as f:
        f.write(str(process.pid))
    
    print(f"Application started (PID: {process.pid})")
    print(f"Log output: {LOG_FILE}")
    
    # Wait a few seconds and check if it's still running
    time.sleep(5)
    
    if is_process_running(process.pid):
        print(f"✓ Application is running at http://localhost:{DEV_PORT}")
        print(f"  MCP endpoint: http://localhost:{DEV_PORT}/mcp")
        print(f"  API Docs: http://localhost:{DEV_PORT}/scalar/v1")
    else:
        print("✗ Application failed to start. Check log file for details:")
        print(f"  python build.py log --tail 50")


def stop_background() -> None:
    """Stop background application"""
    pid = get_running_pid()
    
    if not pid:
        print("No running application found")
        return
    
    print(f"Stopping application (PID: {pid})...")
    
    try:
        process = psutil.Process(pid)
        
        # Try graceful shutdown first
        if platform.system() == "Windows":
            process.terminate()
        else:
            process.send_signal(signal.SIGTERM)
        
        # Wait up to 10 seconds for graceful shutdown
        try:
            process.wait(timeout=10)
            print("✓ Application stopped gracefully")
        except psutil.TimeoutExpired:
            # Force kill if still running
            print("Application did not stop gracefully, forcing shutdown...")
            process.kill()
            process.wait(timeout=5)
            print("✓ Application stopped forcefully")
        
        # Clean up PID file
        if PID_FILE.exists():
            PID_FILE.unlink()
    
    except psutil.NoSuchProcess:
        print("Process already stopped")
        if PID_FILE.exists():
            PID_FILE.unlink()
    
    except Exception as e:
        print(f"Error stopping application: {e}")
        sys.exit(1)


def run_dotnet_command(command: str) -> None:
    """Execute the appropriate dotnet command"""
    try:
        if command == "run":
            print("Running project in foreground...")
            print(f"URL: http://localhost:{DEV_PORT}")
            print("Press Ctrl+C to stop...")
            
            # Set environment for Development mode
            env = os.environ.copy()
            env["ASPNETCORE_ENVIRONMENT"] = "Development"
            
            subprocess.run(
                ["dotnet", "run", "--project", PROJECT_PATH, "--no-restore"],
                env=env
            )
        
        elif command == "build":
            # Auto-stop any running application to prevent file lock issues
            pid = get_running_pid()
            if pid:
                print("Stopping running application before build...")
                stop_background()
            
            print("Building solution...")
            subprocess.run(
                ["dotnet", "build", SOLUTION_PATH],
                check=True
            )
            print("✓ Successfully built solution")
        
        elif command == "start":
            # Auto-stop any running application to prevent file lock issues
            pid = get_running_pid()
            if pid:
                print("Stopping running application before build...")
                stop_background()
            
            # Auto-build solution before starting
            print("Building solution before start...")
            subprocess.run(
                ["dotnet", "build", SOLUTION_PATH],
                check=True
            )
            start_background()
        
        elif command == "stop":
            stop_background()
        
        elif command == "status":
            check_status()
        
        else:
            print(f"Unknown command: {command}")
            print_usage()
            sys.exit(1)
    
    except KeyboardInterrupt:
        # Handle Ctrl+C gracefully for interactive commands
        print("\n\nShutdown requested. Exiting cleanly...")
        sys.exit(0)
    
    except subprocess.CalledProcessError:
        print(f"Error: Failed to {command} project")
        sys.exit(1)


def search_log(pattern: Optional[str] = None, tail: int = 0, level: Optional[str] = None) -> None:
    """Search or tail the log file
    
    Args:
        pattern: Regex pattern to search for (case-insensitive)
        tail: Number of lines to show from end (0 = all)
        level: Filter by log level (error, warn, info, debug)
    """
    import re
    
    if not LOG_FILE.exists():
        print(f"Log file not found: {LOG_FILE}")
        print("Start the application first with: python build.py start")
        return
    
    try:
        with open(LOG_FILE, 'r', encoding='utf-8', errors='replace') as f:
            lines = f.readlines()
        
        # Apply tail
        if tail > 0:
            lines = lines[-tail:]
        
        # Filter by level if specified
        if level:
            level_upper = level.upper()
            level_pattern = re.compile(rf'\b{level_upper}\b|{level_upper}:', re.IGNORECASE)
            lines = [line for line in lines if level_pattern.search(line)]
        
        # Filter by pattern if specified
        if pattern:
            try:
                regex = re.compile(pattern, re.IGNORECASE)
                lines = [line for line in lines if regex.search(line)]
            except re.error as e:
                print(f"Invalid regex pattern: {e}")
                return
        
        if lines:
            for line in lines:
                print(line.rstrip())
            print(f"\n--- {len(lines)} line(s) shown ---")
        else:
            print("No matching log entries found")
    
    except Exception as e:
        print(f"Error reading log file: {e}")


async def run_mcp_command(subcommand: str, tool_name: Optional[str] = None,
                          tool_args: Optional[str] = None, timeout: int = 600,
                          project: Optional[str] = None) -> int:
    """Execute MCP client commands against the running server.

    Args:
        subcommand: The MCP subcommand (tools, call, ping, info)
        tool_name: Name of the tool to call (for 'call' subcommand)
        tool_args: JSON string of arguments for tool call
        timeout: Timeout in seconds for operations (default: 200)
        project: Optional project alias (e.g., 'midnight-limitations').
                 If not specified, uses default endpoint which routes to default project

    Returns:
        Exit code (0 for success, 1 for error)
    """
    if not FASTMCP_AVAILABLE:
        print("✗ fastmcp package is not installed")
        print("\nInstall it with:")
        print("  pip install fastmcp")
        return 1

    # Check if server is running
    pid = get_running_pid()
    if not pid:
        print("✗ Application is not running")
        print("Start it first with: python build.py start")
        return 1

    # Build MCP URL with optional project routing
    if project:
        mcp_url = f"http://localhost:{DEV_PORT}/{project}/mcp"
        print(f"Connecting to project: {project}")
    else:
        mcp_url = f"http://localhost:{DEV_PORT}/mcp"
        print(f"Connecting to default project (midnight)")
    
    try:
        # Pass timeout to client constructor - this applies to all MCP operations
        client = McpClient(mcp_url, timeout=timeout)
        
        async with client:
            if subcommand == "ping":
                await client.ping()
                print(f"✓ MCP server is reachable at {mcp_url}")
                return 0
            
            elif subcommand == "info":
                # Show server info from initialization
                init_result = client.initialize_result
                if init_result:
                    print("MCP Server Information:")
                    print(f"  Name: {init_result.serverInfo.name}")
                    print(f"  Version: {init_result.serverInfo.version}")
                    if init_result.instructions:
                        print(f"  Instructions: {init_result.instructions}")
                    if init_result.capabilities:
                        caps = init_result.capabilities
                        print("  Capabilities:")
                        if caps.tools:
                            print("    - Tools: ✓")
                        if caps.resources:
                            print("    - Resources: ✓")
                        if caps.prompts:
                            print("    - Prompts: ✓")
                return 0
            
            elif subcommand == "tools":
                tools = await client.list_tools()
                if not tools:
                    print("No tools available")
                    return 0
                
                print(f"Available MCP Tools ({len(tools)}):")
                print("-" * 60)
                for tool in tools:
                    print(f"\n  {tool.name}")
                    if tool.description:
                        # Wrap description nicely
                        desc_lines = tool.description.split('\n')
                        for line in desc_lines[:3]:  # Show first 3 lines
                            print(f"    {line.strip()}")
                        if len(desc_lines) > 3:
                            print(f"    ...")
                    if tool.inputSchema:
                        props = tool.inputSchema.get('properties', {})
                        required = tool.inputSchema.get('required', [])
                        if props:
                            print("    Parameters:")
                            for param, schema in props.items():
                                req_marker = "*" if param in required else ""
                                param_type = schema.get('type', 'any')
                                param_desc = schema.get('description', '')
                                print(f"      - {param}{req_marker}: {param_type}")
                                if param_desc:
                                    desc_truncated = param_desc[:60] + "..." if len(param_desc) > 60 else param_desc
                                    print(f"          {desc_truncated}")
                return 0
            
            elif subcommand == "call":
                if not tool_name:
                    print("✗ Tool name is required for 'call' subcommand")
                    print("Usage: python build.py mcp call <tool_name> ['{\"arg\": \"value\"}']")
                    return 1
                
                # Parse tool arguments
                args_dict = {}
                if tool_args:
                    try:
                        args_dict = json.loads(tool_args)
                    except json.JSONDecodeError as e:
                        print(f"✗ Invalid JSON arguments: {e}")
                        return 1
                
                print(f"Calling tool: {tool_name}")
                if args_dict:
                    print(f"Arguments: {json.dumps(args_dict, indent=2)}")
                
                result = await client.call_tool(tool_name, args_dict)
                
                print("\nResult:")
                print("-" * 40)
                # Handle different result types
                if hasattr(result, 'content'):
                    for content in result.content:
                        if hasattr(content, 'text'):
                            print(content.text)
                        else:
                            print(content)
                else:
                    print(result)
                return 0
            
            else:
                print(f"✗ Unknown MCP subcommand: {subcommand}")
                print("Available subcommands: ping, info, tools, call")
                return 1
    
    except Exception as e:
        print(f"✗ MCP Error ({type(e).__name__}): {e}")
        import traceback
        traceback.print_exc()
        return 1


def run_mcp(subcommand: str, tool_name: Optional[str] = None,
            tool_args: Optional[str] = None, timeout: int = 600,
            project: Optional[str] = None) -> int:
    """Synchronous wrapper for run_mcp_command."""
    return asyncio.run(run_mcp_command(subcommand, tool_name, tool_args, timeout, project))


def run_rest(method: str, path: str, query_params: dict, project_alias: Optional[str] = None,
             output_format: str = "pretty") -> int:
    """Execute REST API calls with automatic auth and project translation.

    Args:
        method: HTTP method (GET, POST, etc.)
        path: REST API path (can use {project} placeholder)
        query_params: Dictionary of query parameters
        project_alias: Optional project alias (defaults to 'midnight')
        output_format: Output format ('pretty' or 'raw')

    Returns:
        Exit code (0 for success, 1 for error)
    """
    # Check if server is running
    pid = get_running_pid()
    if not pid:
        print("✗ Application is not running")
        print("Start it first with: python build.py start")
        return 1

    # Load appsettings to get API key and project config
    try:
        appsettings_path = Path("PolarionRemoteMcpServer/appsettings.Development.json")
        if not appsettings_path.exists():
            appsettings_path = Path("PolarionRemoteMcpServer/appsettings.json")

        with open(appsettings_path, 'r') as f:
            appsettings = json.load(f)
    except Exception as e:
        print(f"✗ Failed to load appsettings: {e}")
        return 1

    # Get first active API consumer
    api_key = None
    consumers = appsettings.get('ApiConsumers', {}).get('Consumers', {})
    for consumer_id, consumer in consumers.items():
        if consumer.get('Active', False):
            api_key = consumer.get('ApplicationKey')
            break

    if not api_key:
        print("✗ No active API consumer found in appsettings")
        return 1

    # Translate project alias to SessionConfig.ProjectId if {project} is in path
    project_alias = project_alias or "midnight"
    if "{project}" in path:
        # Find project config by alias
        project_id = None
        projects = appsettings.get('PolarionProjects', [])
        for proj in projects:
            if proj.get('ProjectUrlAlias') == project_alias:
                project_id = proj.get('SessionConfig', {}).get('ProjectId')
                break

        if not project_id:
            print(f"✗ Project alias '{project_alias}' not found in configuration")
            return 1

        path = path.replace("{project}", project_id)
        print(f"Project: {project_alias} → {project_id}")

    # Build URL
    base_url = f"http://localhost:{DEV_PORT}"
    if not path.startswith('/'):
        path = '/' + path

    # Build query string from parameters
    query_string = ""
    if query_params:
        query_parts = []
        for key, value in query_params.items():
            if value is not None:
                # Handle page[size] special case
                if key == "page[size]":
                    query_parts.append(f"page%5Bsize%5D={value}")
                else:
                    from urllib.parse import quote
                    query_parts.append(f"{key}={quote(str(value))}")
        if query_parts:
            query_string = "?" + "&".join(query_parts)

    full_url = base_url + path + query_string

    print(f"Request: {method} {path}{query_string}")

    # Make request using curl
    try:
        import subprocess
        cmd = [
            "curl", "-s", "-X", method,
            full_url,
            "-H", f"X-API-Key: {api_key}",
            "-w", "\n\nHTTP_STATUS:%{http_code}"
        ]

        result = subprocess.run(cmd, capture_output=True, text=True)
        output = result.stdout

        # Split response and status code
        parts = output.rsplit("\n\nHTTP_STATUS:", 1)
        response_body = parts[0] if len(parts) > 0 else output
        status_code = parts[1].strip() if len(parts) > 1 else "unknown"

        print(f"Status: {status_code}")
        print()

        # Format output
        if output_format == "pretty" and response_body:
            try:
                data = json.loads(response_body)
                print(json.dumps(data, indent=2))
            except json.JSONDecodeError:
                print(response_body)
        else:
            print(response_body)

        return 0 if status_code.startswith('2') else 1

    except Exception as e:
        print(f"✗ REST Error: {e}")
        return 1


def print_usage() -> None:
    """Print usage information"""
    print("Usage: python build.py [command] [options]")
    print("")
    print("Build & Run Commands:")
    print("  build        - Build the solution (auto-stops running app)")
    print("  run          - Run the project in foreground (Ctrl+C to stop)")
    print("  start        - Build and start in background (port 5090)")
    print("  stop         - Stop the background application")
    print("  status       - Check if application is running")
    print("")
    print("MCP Commands (requires: pip install fastmcp psutil):")
    print("  mcp ping [--project <alias>]              - Check MCP server connectivity")
    print("  mcp info [--project <alias>]              - Show MCP server information")
    print("  mcp tools [--project <alias>]             - List available MCP tools")
    print("  mcp call <tool> ['{...}'] [--project <alias>] - Call an MCP tool with JSON args")
    print("")
    print("REST API Commands:")
    print("  rest <method> <path> [options]            - Call REST API endpoints")
    print("    --project <alias>    - Project to use (default: midnight)")
    print("    --query <text>       - Search query")
    print("    --types <types>      - Comma-separated work item types")
    print("    --status <status>    - Comma-separated status values")
    print("    --sort <field>       - Sort field")
    print("    --page-size <n>      - Results per page")
    print("    --limit <n>          - Limit results")
    print("    --format <fmt>       - Output format: pretty (default) or raw")
    print("")
    print("  Project aliases: midnight (default), midnight-limitations, product-lifecycle,")
    print("                   midnight-flight-test, blue-thunder, midnight-1-0, midnight-1-1")
    print("")
    print("Log Commands:")
    print("  log                      - Show last 50 lines of log")
    print("  log <pattern>            - Search log for regex pattern")
    print("  log --tail <n>           - Show last n lines")
    print("  log --level <level>      - Filter by level (error/warn/info/debug)")
    print("  log --tail 100 --level error - Combine options")
    print("")
    print("URLs (when running):")
    print(f"  http://localhost:{DEV_PORT}              - Landing page")
    print(f"  http://localhost:{DEV_PORT}/mcp          - MCP endpoint")
    print(f"  http://localhost:{DEV_PORT}/scalar/v1    - API documentation (Scalar)")
    print("")
    print("Examples:")
    print("  python build.py start                       # Build and start server")
    print("  python build.py mcp tools                   # List all MCP tools (default project)")
    print("  python build.py rest GET api/health         # Check API health")
    print('  python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query rigging --project midnight')
    print('  python build.py rest GET "polarion/rest/v1/projects/{project}/workitems/MD-12345" --project midnight')
    print('  python build.py rest GET "polarion/rest/v1/projects/{project}/spaces" --project midnight')
    print("  python build.py log --level error           # View error logs")
    print("  python build.py stop                        # Stop the server")


def main() -> None:
    """Main entry point"""
    # Parse command argument
    command = sys.argv[1] if len(sys.argv) > 1 else "build"
    
    # Special commands that don't need dotnet
    if command in ["stop", "status"]:
        if command == "stop":
            stop_background()
        else:
            check_status()
        return
    
    # Log command
    if command == "log":
        pattern = None
        tail = 50  # Default to last 50 lines
        level = None
        
        # Parse log options
        args = sys.argv[2:]
        i = 0
        while i < len(args):
            if args[i] == "--tail" and i + 1 < len(args):
                try:
                    tail = int(args[i + 1])
                except ValueError:
                    print(f"Invalid tail value: {args[i + 1]}")
                    sys.exit(1)
                i += 2
            elif args[i] == "--level" and i + 1 < len(args):
                level = args[i + 1]
                i += 2
            elif not args[i].startswith("--"):
                pattern = args[i]
                i += 1
            else:
                print(f"Unknown option: {args[i]}")
                print_usage()
                sys.exit(1)
        
        search_log(pattern=pattern, tail=tail, level=level)
        return
    
    # MCP command
    if command == "mcp":
        if len(sys.argv) < 3:
            print("Error: mcp requires a subcommand")
            print("Usage: python build.py mcp <ping|info|tools|call> [--project <alias>] [args]")
            sys.exit(1)

        subcommand = sys.argv[2]
        tool_name = None
        tool_args = None
        project = None

        # Parse remaining arguments
        args = sys.argv[3:]
        i = 0
        while i < len(args):
            if args[i] == "--project" and i + 1 < len(args):
                project = args[i + 1]
                i += 2
            elif subcommand == "call":
                if tool_name is None:
                    tool_name = args[i]
                    i += 1
                elif tool_args is None:
                    tool_args = args[i]
                    i += 1
                else:
                    print(f"Unexpected argument: {args[i]}")
                    sys.exit(1)
            else:
                print(f"Unexpected argument: {args[i]}")
                sys.exit(1)

        sys.exit(run_mcp(subcommand, tool_name, tool_args, project=project))

    # REST command
    if command == "rest":
        if len(sys.argv) < 4:
            print("Error: rest requires method and path")
            print("Usage: python build.py rest <method> <path> [options]")
            print('Example: python build.py rest GET "polarion/rest/v1/projects/{project}/workitems" --query rigging')
            sys.exit(1)

        method = sys.argv[2].upper()
        path = sys.argv[3]

        # Parse remaining arguments
        query_params = {}
        project = None
        output_format = "pretty"

        args = sys.argv[4:]
        i = 0
        while i < len(args):
            if args[i] == "--project" and i + 1 < len(args):
                project = args[i + 1]
                i += 2
            elif args[i] == "--query" and i + 1 < len(args):
                query_params["query"] = args[i + 1]
                i += 2
            elif args[i] == "--types" and i + 1 < len(args):
                query_params["types"] = args[i + 1]
                i += 2
            elif args[i] == "--status" and i + 1 < len(args):
                query_params["status"] = args[i + 1]
                i += 2
            elif args[i] == "--sort" and i + 1 < len(args):
                query_params["sort"] = args[i + 1]
                i += 2
            elif args[i] == "--page-size" and i + 1 < len(args):
                query_params["page[size]"] = args[i + 1]
                i += 2
            elif args[i] == "--limit" and i + 1 < len(args):
                query_params["limit"] = args[i + 1]
                i += 2
            elif args[i] == "--format" and i + 1 < len(args):
                output_format = args[i + 1]
                i += 2
            else:
                print(f"Unknown option: {args[i]}")
                sys.exit(1)

        sys.exit(run_rest(method, path, query_params, project, output_format))

    # Help command
    if command in ["help", "--help", "-h"]:
        print_usage()
        return
    
    # Commands that need dotnet
    if command not in ["build", "run", "start"]:
        print(f"Unknown command: {command}")
        print_usage()
        sys.exit(1)
    
    # Execute command
    run_dotnet_command(command)


if __name__ == "__main__":
    main()
