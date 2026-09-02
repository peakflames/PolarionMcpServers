using PolarionRemoteMcpServer.Rbac.Audit;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Collects every <see cref="AccessAuditRecord"/> the filter emits, so a test can assert on the
/// resolved identity and the decision reason directly instead of grepping rendered Serilog output.
/// Substituted via <see cref="PolarionMcpServerFactory.WithPostAuthServices"/>, the only seam that
/// runs after <c>AddRbac</c>'s own registration.
/// </summary>
public sealed class CapturingAuditSink : IMcpAccessAuditSink
{
    private readonly List<AccessAuditRecord> _records = [];
    private readonly object _gate = new();

    public IReadOnlyList<AccessAuditRecord> Records
    {
        get { lock (_gate) { return _records.ToArray(); } }
    }

    public void Record(AccessAuditRecord record)
    {
        lock (_gate) { _records.Add(record); }
    }
}
