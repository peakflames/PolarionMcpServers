using FluentResults;
using Polarion;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Unit.Infrastructure;

/// <summary>
/// Minimal <see cref="IPolarionClientFactory"/> that hands back a caller-supplied
/// <see cref="IPolarionClient"/> (typically a strict Moq), so a tool's query-shaping and its
/// exact transport call can be observed without a live Polarion connection.
/// </summary>
public sealed class StubPolarionClientFactory : IPolarionClientFactory
{
    private readonly IPolarionClient _client;

    public StubPolarionClientFactory(IPolarionClient client)
    {
        _client = client;
    }

    public string? ProjectId => "Starlight_Main";

    public Task<Result<IPolarionClient>> CreateClientAsync()
        => Task.FromResult(Result.Ok(_client));
}
