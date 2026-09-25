// LLM maintained.
//
// A sink for the few diagnostics the graphics abstraction raises on its own, set by the host at
// startup.
//
// This layer is deliberately below the application's logging stack - it references nothing but the
// shader abstraction, so `NFMWorldLibrary.Logging` is not available to it, and a direct reference
// would drag the whole application's logging (ZLogger, Sentry, rolling files) under a graphics
// binding that a headless test also links. So the sink is an injected delegate the host points at
// whatever it logs with, and the default of null simply drops the message.
//
// Everything here is advisory: a message means a caller passed something the wrapper declined to
// write, never that the process is in a bad state. A host that wants the detail sets the sink; a
// host that does not still gets correct rendering.
namespace NFMWorld.Graphics;

public static class GraphicsDiagnostics
{
    /// <summary>
    /// Called with a human-readable description of a value the graphics layer refused to apply.
    /// Null (the default) discards the message.
    /// </summary>
    public static Action<string>? Warning { get; set; }
}
