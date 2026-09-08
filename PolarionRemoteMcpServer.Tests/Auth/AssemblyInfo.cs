using Xunit;

// The Auth suite boots a real loopback Kestrel listener (the stub authorization server) per test
// and depends on the resource server's JwtBearer handler completing a real HTTP discovery/JWKS
// round trip against it. Running many of these concurrently was observed to produce intermittent
// spurious 401s under load — serializing test execution trades suite speed for determinism, which
// matters more here than anywhere else in this project because these tests assert on live network
// round trips, not just in-memory state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
