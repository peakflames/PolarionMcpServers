namespace StubAuthorizationServer;

/// <summary>
/// Deliberately not top-level statements — those emit an internal `class Program` in the global
/// namespace, invisible to the test project and an ambiguity hazard against the test project's
/// `global using PolarionRemoteMcpServer` (which also defines a `Program` type).
/// </summary>
public class StubAuthorizationServerProgram
{
    public static void Main(string[] args)
    {
        var app = StubAuthorizationServerApp.Build(args);
        app.Run();
    }
}
