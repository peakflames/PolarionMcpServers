namespace StubAuthorizationServer;

/// <summary>
/// One RSA key generated once per process run, never disposed — a static Lazy&lt;RSA&gt;, not
/// fixed test material. Importing the *same* RSA material into separate instances has been
/// observed to cause cross-instance signature-validation flakiness in this repo's test suite;
/// reusing fixed material here would reproduce that pattern, and worse, a bug where the SUT
/// confuses its own key for the AS's would pass silently.
/// </summary>
public static class SigningKey
{
    public const string KeyId = "stub-as-signing-key-1";

    private static readonly Lazy<RSA> Instance = new(() => RSA.Create(2048));

    public static RSA Rsa => Instance.Value;
}
