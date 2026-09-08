using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarionMcpTools;
using PolarionMcpTools.Rbac;
using PolarionRemoteMcpServer.Rbac;
using PolarionRemoteMcpServer.Rbac.Audit;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// A <see cref="PolarionMcpServerFactory"/> whose Polarion-facing dependencies are all fakes —
/// the harness for the MCP tool bodies' RBAC-gated <c>tools/call</c> path. Every substitution runs
/// through
/// <see cref="PolarionMcpServerFactory.WithPostAuthServices"/> (via <c>Replace()</c>, not
/// <c>Add</c>/<c>TryAdd</c>), the only seam that runs after <c>AddRbac</c>'s own <c>Replace()</c>
/// call and therefore the only one that reliably wins against it.
///
/// <see cref="Gate"/> defaults to a <see cref="RecordingGate"/> (always allow, records every call)
/// and is a plain mutable property rather than a constructor parameter — set it before the first
/// call to <see cref="PolarionMcpServerFactory.CreateClient"/> or
/// <see cref="PolarionMcpServerFactory.Services"/>, since the substitution closure reads it lazily
/// at app-build time, not at construction time.
/// </summary>
public sealed class PolarionFakeFactory : PolarionMcpServerFactory
{
    public CapturingAuditSink AuditSink { get; } = new();

    internal FakeIdentityLookup IdentityLookup { get; } = new();

    public IProjectVisibilityGate Gate { get; set; } = new RecordingGate();

    public PolarionFakeFactory()
    {
        WithPostAuthServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IIdentityLookup>(IdentityLookup));
            services.Replace(ServiceDescriptor.Singleton<IProjectVisibilityGate>(Gate));
            services.Replace(ServiceDescriptor.Scoped<IPolarionClientFactory>(sp =>
                new FakePolarionClientFactory(sp.GetRequiredService<IHttpContextAccessor>())));
            services.Replace(ServiceDescriptor.Singleton<IMcpAccessAuditSink>(AuditSink));
        });
    }
}
