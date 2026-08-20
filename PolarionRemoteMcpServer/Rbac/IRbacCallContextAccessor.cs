namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// The <c>IHttpContextAccessor</c> shape, deliberately: a singleton wrapping an <c>AsyncLocal</c>, so
/// the value set by <see cref="RbacIdentityFilter"/> flows through <c>await next(ctx, ct)</c> into a
/// tool body's own DI scope regardless of scoping. This matters because nested
/// <c>CreateAsyncScope()</c> calls are siblings, not children — they resolve the singleton
/// <c>IServiceScopeFactory</c> and root at the same root provider — so a scoped DI service resolved
/// inside a tool's own scope would be a *different instance* than one resolved in the filter. An
/// <c>AsyncLocal</c> has no such problem: it flows with the async call chain, not with the DI scope
/// tree.
/// </summary>
public interface IRbacCallContextAccessor
{
    RbacCallContext? Current { get; set; }
}
