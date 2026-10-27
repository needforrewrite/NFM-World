// A synchronous byte-range read of a URL, for callers that cannot await.
//
// System.Net.Http is asynchronous only on wasm, and its sync-over-async form throws
// Arg_PlatformNotSupported (the runtime has no threads to block on). ZipArchive needs a seekable
// stream whose reads return immediately, so the only route is a JavaScript call that has resolved
// by the time it returns: the page uses a synchronous XMLHttpRequest, which the browser allows for
// a same-origin request like this one. A track is fetched while the game is paused, so the brief
// block costs a frame rather than a hang.
//
// Values crossing the boundary are int and double only: those marshal without an attribute, while
// long and byte[] each need an explicit [JSMarshalAs] that this runtime's generator does not agree
// with. Ranges are therefore split into int-sized requests by the caller.

using System.Runtime.InteropServices.JavaScript;

namespace NFMWorld.Interop.Browser;

public static partial class BrowserHttp
{
    private const string Module = "http.js";

    /// <summary>Imports the module the [JSImport]s resolve against. Await it before any call.</summary>
    public static Task ImportAsync() => JSHost.ImportAsync(Module, "./http.js");

    /// <summary>The resource's length in bytes, or -1 when the server did not report one.</summary>
    public static long ContentLength(string url) => (long)ContentLengthNative(url);

    /// <summary>
    /// Reads <paramref name="count"/> bytes starting at <paramref name="start"/>. Returns the bytes
    /// read - fewer than asked only at end of file. Returned rather than written into a caller's
    /// buffer because a <c>byte[]</c> parameter is copied in only: the JavaScript side's writes
    /// never come back.
    /// </summary>
    public static byte[] ReadRange(string url, long start, int count) =>
        ReadRangeNative(url, (double)start, count);

    // A double, not a long: an unannotated double marshals as a JS number, which is what the
    // offsets and lengths are on the page side anyway.
    [JSImport("contentLength", Module)]
    private static partial double ContentLengthNative(string url);

    [JSImport("readRange", Module)]
    private static partial byte[] ReadRangeNative(string url, double start, int count);
}
