using System.Diagnostics.CodeAnalysis;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Polarion;
using PolarionMcpTools;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Preserves the real <see cref="PolarionRemoteMcpServer.PolarionRemoteClientFactory"/>'s one load
/// -bearing behavior — <see cref="ProjectId"/> comes from the request route, which is what
/// <see cref="PolarionRemoteMcpServer.Rbac.RbacIdentityFilter"/> reads the project alias from — but
/// always fails client creation. That lets an RBAC-allowed call run all the way through a real tool
/// body and come back as an ordinary "ERROR: ..." string inside a successful (HTTP 200) CallToolResult,
/// instead of opening a real SOAP connection to a Polarion server that does not exist in this test
/// process.
/// </summary>
public sealed class FakePolarionClientFactory : IPolarionClientFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FakePolarionClientFactory(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ProjectId => _httpContextAccessor.HttpContext?.GetRouteValue("projectId")?.ToString();

    [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
    public Task<Result<IPolarionClient>> CreateClientAsync() =>
        Task.FromResult(Result.Fail<IPolarionClient>("stub: no live Polarion connection in tests"));
}
