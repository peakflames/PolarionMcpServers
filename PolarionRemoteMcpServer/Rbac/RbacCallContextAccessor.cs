namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Mirrors the actual implementation of ASP.NET Core's <c>HttpContextAccessor</c>: the value is
/// wrapped in a private <see cref="Holder"/> rather than stored directly in the
/// <c>AsyncLocal&lt;T&gt;</c>. Setting a new value clears the *old* holder's field first — without
/// that, a child async flow that captured the parent's <c>ExecutionContext</c> before the parent
/// later overwrote its own local would still see the old holder mutate underneath it, because setting
/// an <c>AsyncLocal</c> to a new object only rebinds the parent's slot, not any copy a child flow
/// already branched from.
/// </summary>
public sealed class RbacCallContextAccessor : IRbacCallContextAccessor
{
    private static readonly AsyncLocal<Holder> CurrentHolder = new();

    public RbacCallContext? Current
    {
        get => CurrentHolder.Value?.Context;
        set
        {
            var holder = CurrentHolder.Value;
            if (holder is not null)
                holder.Context = null;

            if (value is not null)
                CurrentHolder.Value = new Holder { Context = value };
        }
    }

    private sealed class Holder
    {
        public RbacCallContext? Context;
    }
}
