// LLM maintained.
//
// Deferred GL object deletion.
//
// This is the ANGLE backend's copy of the file NFMWorld.Graphics.DesktopGL carries under the same
// name, and it exists for the same reason - a disposer cannot always delete its own object - with a
// different and much quieter failure behind it.
//
// GL object destruction happens on the finalizer thread when a caller forgets to dispose (and this
// game's renderables genuinely lean on their finalizers; see Mesh.Submeshes, whose owner disposes
// them only under `if (disposing)`, so an unreferenced mesh reaches its buffers through
// Submesh.~Submesh and nowhere else). This backend reaches every entry point past GL 1.1 through
// eglGetProcAddress, and unlike wglGetProcAddress that loader is context-independent: it resolves on
// the finalizer thread, finds the symbol, and returns a valid pointer. So the call goes through.
//
// And it is the wrong call, which is why the queue is here. Issuing a GL *call* from a thread with no
// current context is undefined; on ANGLE it happens to execute, with the context name mangled for
// that thread ("ANGLE (NameMangling)"). The symptom is not a crash but silent corruption: a delete
// that removes nothing (the leak reverts to unbounded), or - worse - one that lands on a different
// object because the mangled name maps somewhere else. The program deletes inside
// GlShaderProgram.Compile and Link are the ones to watch, because converting a link failure into a
// delete of whatever the mangled name happens to address is the same shape of hazard.
//
// So this is not "ANGLE has a bug and desktop GL does not". Desktop GL's loader refuses, loudly, on
// the finalizer thread and takes the process down; ANGLE's loader accepts and does something
// unspecified. Neither is correct, and the same queue is the fix for both.
using System.Collections.Concurrent;
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>Which kind of GL object a queued deletion refers to - i.e. which <c>glDelete*</c>.</summary>
internal enum GlObjectKind : byte
{
    Buffer,
    Texture,
    Sampler,
    Framebuffer,
    VertexArray,
    Program,
}

/// <summary>One object to delete: its kind and the GL name.</summary>
/// <remarks>
/// The name alone would not be enough - GL object names are namespaced per kind, so 3 is a buffer and
/// a texture and a framebuffer at once, and nothing can tell them apart after the fact. That matters
/// more here than on the desktop GL copy: a kind misread on the finalizer thread would pick the wrong
/// <c>glDelete*</c> from the mangled context, which is precisely the silent-corruption case.
/// </remarks>
internal readonly record struct GlDeletion(GlObjectKind Kind, uint Handle);

/// <summary>
/// Holds GL names that need deleting until a thread with a current context can delete them.
///
/// A <see cref="ConcurrentQueue{T}"/> rather than a lock-free array: the producer side is the
/// finalizer thread and the consumer side is the main thread, so the structure has to tolerate any
/// number of concurrent enqueues against a drain that is racing them - and it only has to be cheap
/// enough for a handful of objects per frame, which is a much lower bar than the rendering path's.
/// Enqueueing allocates a segment every 32 items and dequeueing allocates nothing.
///
/// No deduplication happens here, and that is deliberate: a caller may dispose the same handle
/// twice (the effect classes each keep a finalizer alongside <c>Dispose</c>, so disposing a parent
/// that disposes its children still finalizes the children), and deleting a name the driver has
/// already freed is harmless - GL ignores a name it does not recognise. Order is preserved, which
/// GL requires for the one dependent case there is: a framebuffer must go before the textures it
/// attached.
/// </summary>
internal sealed class GlDeletionQueue
{
    private readonly ConcurrentQueue<GlDeletion> _pending = new();

    /// <summary>
    /// Asks for a GL name to be deleted on the next drain.
    ///
    /// Callable from any thread, which is the point - see the note at the top of this file for why a
    /// disposer cannot simply call <c>glDelete*</c> itself.
    /// </summary>
    public void Request(GlObjectKind kind, uint handle) => _pending.Enqueue(new GlDeletion(kind, handle));

    /// <summary>
    /// Deletes everything queued, and returns how many. Must be called on the thread whose context is
    /// current - the main thread - which is what <see cref="GlGraphicsDevice.Submit"/> does at the end
    /// of every frame.
    ///
    /// Safe to call concurrently with <see cref="Request"/>: items enqueued during the drain are
    /// simply not seen by it and wait for the next one, because <see cref="ConcurrentQueue{T}"/>
    /// snapshots the head rather than the tail.
    /// </summary>
    public int Drain(GL gl)
    {
        var deleted = 0;
        while (_pending.TryDequeue(out var deletion))
        {
            Delete(gl, deletion);
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    /// The kind-to-entry-point mapping. Kept as a switch rather than a delegate table so the call is
    /// direct and so adding a kind without a delete is a compile error rather than a silent miss.
    /// </summary>
    private static void Delete(GL gl, GlDeletion deletion)
    {
        switch (deletion.Kind)
        {
            case GlObjectKind.Buffer: gl.DeleteBuffer(deletion.Handle); break;
            case GlObjectKind.Texture: gl.DeleteTexture(deletion.Handle); break;
            case GlObjectKind.Sampler: gl.DeleteSampler(deletion.Handle); break;
            case GlObjectKind.Framebuffer: gl.DeleteFramebuffer(deletion.Handle); break;
            case GlObjectKind.VertexArray: gl.DeleteVertexArray(deletion.Handle); break;
            case GlObjectKind.Program: gl.DeleteProgram(deletion.Handle); break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(deletion), deletion.Kind, "No delete entry point is mapped for this object kind.");
        }
    }
}
