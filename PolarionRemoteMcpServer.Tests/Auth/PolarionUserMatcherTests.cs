using PolarionRemoteMcpServer.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>The email join and the id tiebreak as unit tests, over placeholder users only.</summary>
public class PolarionUserMatcherTests
{
    private static (string? Id, string? Email) U(string? id, string? email) => (id, email);

    [Fact]
    public void NoEmailMatch_IsNotFound()
    {
        var result = PolarionUserMatcher.Match(
            [U("alice", "alice@example.invalid"), U("bob", "bob@example.invalid")],
            "carol@example.invalid");

        Assert.Equal(IdentityOutcome.NotFound, result.Outcome);
        Assert.Null(result.Username);
    }

    [Fact]
    public void SingleMatch_ResolvesEvenWhenIdDiffersFromLocalPart()
    {
        var result = PolarionUserMatcher.Match(
            [U("carol.ext", "carol@example.invalid"), U("alice", "alice@example.invalid")],
            "carol@example.invalid");

        Assert.Equal(IdentityOutcome.Resolved, result.Outcome);
        Assert.Equal("carol.ext", result.Username);
    }

    [Fact]
    public void ServiceAccountAndOwner_ResolvesToOwner_RegardlessOfOrder()
    {
        var owner = U("alice", "alice@example.invalid");
        var service = U("alice_toolkit_access", "alice@example.invalid");

        var forward = PolarionUserMatcher.Match([owner, service], "alice@example.invalid");
        var reversed = PolarionUserMatcher.Match([service, owner], "alice@example.invalid");

        Assert.Equal(IdentityOutcome.Resolved, forward.Outcome);
        Assert.Equal("alice", forward.Username);
        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void OwnerAndSuffixedAltAccount_ResolvesToOwner()
    {
        var result = PolarionUserMatcher.Match(
            [U("bobrct", "bob@example.invalid"), U("bob", "bob@example.invalid")],
            "bob@example.invalid");

        Assert.Equal(IdentityOutcome.Resolved, result.Outcome);
        Assert.Equal("bob", result.Username);
    }

    [Fact]
    public void ManyServiceAccountsAndOwner_ResolvesToOwner()
    {
        var result = PolarionUserMatcher.Match(
            [
                U("dave_svc_a", "dave@example.invalid"),
                U("dave_svc_b", "dave@example.invalid"),
                U("dave", "dave@example.invalid"),
                U("dave_svc_c", "dave@example.invalid"),
                U("dave_svc_d", "dave@example.invalid"),
            ],
            "dave@example.invalid");

        Assert.Equal(IdentityOutcome.Resolved, result.Outcome);
        Assert.Equal("dave", result.Username);
    }

    [Fact]
    public void SeveralMatches_NoneWithLocalPartId_IsAmbiguous()
    {
        var result = PolarionUserMatcher.Match(
            [U("erin_a", "erin@example.invalid"), U("erin_b", "erin@example.invalid")],
            "erin@example.invalid");

        Assert.Equal(IdentityOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Username);
    }

    [Fact]
    public void UserWithLocalPartIdButDifferentEmail_IsNeverChosen()
    {
        // "frank" has the right id but another address — the tiebreak picks only among email matches.
        var result = PolarionUserMatcher.Match(
            [
                U("frank", "frank@other.example.invalid"),
                U("frank_a", "frank@example.invalid"),
                U("frank_b", "frank@example.invalid"),
            ],
            "frank@example.invalid");

        Assert.Equal(IdentityOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Username);
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        var result = PolarionUserMatcher.Match(
            [U("alice", "alice@example.invalid"), U("alice_toolkit_access", "alice@example.invalid")],
            "Alice@Example.Invalid");

        Assert.Equal(IdentityOutcome.Resolved, result.Outcome);
        Assert.Equal("alice", result.Username);
    }

    [Theory]
    [InlineData("alice")]                       // no '@'
    [InlineData("a@b@example.invalid")]         // two '@'
    [InlineData("@example.invalid")]            // empty local part
    public void MalformedClaim_GetsNoTiebreak(string claimValue)
    {
        // Every user carries the claim value as their email, and one has an id equal to what a naive
        // split would call the local part — a malformed claim must still not be tiebroken.
        var result = PolarionUserMatcher.Match(
            [U("alice", claimValue), U("a", claimValue), U("alice_svc", claimValue)],
            claimValue);

        Assert.Equal(IdentityOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Username);
    }

    [Theory]
    [InlineData(null, "alice@example.invalid")]
    [InlineData("", "alice@example.invalid")]
    [InlineData("alice", null)]
    [InlineData("alice", "")]
    public void UsersWithNullOrEmptyIdOrEmail_AreSkipped(string? id, string? email)
    {
        var result = PolarionUserMatcher.Match([U(id, email)], "alice@example.invalid");

        Assert.Equal(IdentityOutcome.NotFound, result.Outcome);
        Assert.Null(result.Username);
    }

    [Fact]
    public void SkippedUsers_DoNotCountTowardAmbiguity()
    {
        var result = PolarionUserMatcher.Match(
            [U("alice_svc", "alice@example.invalid"), U(null, "alice@example.invalid")],
            "alice@example.invalid");

        Assert.Equal(IdentityOutcome.Resolved, result.Outcome);
        Assert.Equal("alice_svc", result.Username);
    }
}
