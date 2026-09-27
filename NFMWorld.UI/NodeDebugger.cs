namespace NFMWorld.Reactor;

public static class NodeDebugger
{
    public readonly struct DebugInfo(
        string ctorCallerFilePath,
        int ctorCallerLineNumber,
        string ctorCallerMemberName
    )
    {
        public readonly string CtorCallerFilePath = ctorCallerFilePath;
        public readonly string CtorCallerMemberName = ctorCallerMemberName;
        public readonly int CtorCallerLineNumber = ctorCallerLineNumber;
    }

    public static Component? YogaRoot { get; set; }

    public static DebugInfo GetDebugInfo(Component component)
    {
        return new DebugInfo(
            component.__INTERNAL_CtorCallerFilePath,
            component.__INTERNAL_CtorCallerLineNumber,
            component.__INTERNAL_CtorCallerMemberName
        );
    }
}