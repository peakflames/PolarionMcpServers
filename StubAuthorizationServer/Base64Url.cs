namespace StubAuthorizationServer;

/// <summary>Self-contained base64url helpers — the stub takes no ProjectReference to
/// PolarionRemoteMcpServer, so nothing here can share code with (and therefore nothing here
/// can accidentally validate) production token-handling code.</summary>
public static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Encode(string utf8Text) => Encode(Encoding.UTF8.GetBytes(utf8Text));
}
