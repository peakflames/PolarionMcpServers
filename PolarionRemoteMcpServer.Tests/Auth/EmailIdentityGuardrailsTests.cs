using PolarionRemoteMcpServer.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>The guardrails as unit tests — the forms an HTTP round trip would never conveniently
/// produce.</summary>
public class EmailIdentityGuardrailsTests
{
    [Theory]
    [InlineData("user@example.invalid", "user@example.invalid")]
    [InlineData("User@Example.Invalid", "user@example.invalid")]
    [InlineData("  user@example.invalid  ", "user@example.invalid")]
    [InlineData("first.last@sub.example.invalid", "first.last@sub.example.invalid")]
    public void Normalizes_AcceptableAddresses(string input, string expected)
    {
        var ok = EmailIdentityGuardrails.TryNormalize(input, out var normalized, out var reason);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("user+tag@example.invalid")]          // plus-addressing — a mapping the IdP has not made
    [InlineData("user+@example.invalid")]
    [InlineData("user@a@example.invalid")]            // two '@'
    [InlineData("@example.invalid")]                  // no local part
    [InlineData("user@")]                             // no domain
    [InlineData("user")]                              // no '@' at all
    [InlineData("\"user name\"@example.invalid")]     // quoted local part
    [InlineData("user(comment)@example.invalid")]
    [InlineData("Display Name <user@example.invalid>")]
    [InlineData("user@example.invalid, other@example.invalid")]
    public void Rejects_AliasAndMalformedAddresses(string input)
    {
        var ok = EmailIdentityGuardrails.TryNormalize(input, out _, out var reason);

        Assert.False(ok);
        Assert.Equal(IdentityUnresolvedReasons.EmailRejectedForm, reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reports_AbsentAddressAsUnresolved_NotAsARejectedForm(string? input)
    {
        // Distinct from a rejection: an absent address is "no identity here", which is the reason a
        // caller with no email at the identity provider should see.
        var ok = EmailIdentityGuardrails.TryNormalize(input, out _, out var reason);

        Assert.False(ok);
        Assert.Equal(IdentityUnresolvedReasons.Unresolved, reason);
    }
}
