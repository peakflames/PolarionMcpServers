namespace PolarionRemoteMcpServer.Rbac.Audit;

public interface IMcpAccessAuditSink
{
    void Record(AccessAuditRecord record);
}
