using Polarion;

namespace PolarionRemoteMcpServer.Tests.Unit;

/// <summary>
/// The per-caller clone of a project's SessionConfig must keep every setting and must not share
/// mutable state with the singleton source.
/// </summary>
public sealed class ClientConfigCloneTests
{
    [Fact]
    public void CloneForCaller_KeepsMaxReceivedMessageSize()
    {
        var source = new PolarionClientConfiguration("https://polarion.test/", "svc", "secret", "TestProject", 60)
            with { MaxReceivedMessageSize = 1048576 };

        var clone = PolarionRemoteClientFactory.CloneForCaller(source);

        clone.MaxReceivedMessageSize.Should().Be(1048576);
    }

    [Fact]
    public void CloneForCaller_ReturnsDifferentInstance()
    {
        var source = new PolarionClientConfiguration("https://polarion.test/", "svc", "secret", "TestProject", 60);

        var clone = PolarionRemoteClientFactory.CloneForCaller(source);

        clone.Should().NotBeSameAs(source);
    }

    [Fact]
    public void CloneForCaller_ChangingClonePassword_DoesNotChangeSource()
    {
        var source = new PolarionClientConfiguration("https://polarion.test/", "svc", "secret", "TestProject", 60);

        var clone = PolarionRemoteClientFactory.CloneForCaller(source);
        clone.Password = "per-caller-value";

        source.Password.Should().Be("secret");
    }
}
