// LLM maintained.
//
// Deferred GL object deletion - the one place this backend has to work around the platform rather
// than the abstraction.
//
// GL object destruction happens on the finalizer thread when a caller forgets to dispose (and this
// game's renderables genuinely lean on their finalizers; see Mesh.Submeshes, whose owner disposes
// them only under `if (disposing)`, so an unreferenced mesh reaches its buffers through
// Submesh.~Submesh and nowhere else). On desktop GL that thread can delete nothing: every entry
// point past GL 1.1 - glDeleteBuffers included - is reached through wglGetProcAddress, which
// resolves only for the thread that made the context current.
//
// That is measured, not inferred. Probing SDL_GL_GetProcAddress for glGenBuffers, glDeleteBuffers,
// glDrawElementsBaseVertex and glUniformBlockBinding returns null on any thread but the context's
// own, and calling SDL_GL_MakeCurrent on the other thread first does not change it; glGetString,
// which opengl32.dll exports directly as GL 1.1, resolves everywhere. So the failure is not a
// missing context - it is the loader's contract.
//
// What a finalizer does with that is worse than failing to delete: the exception leaves
// Finalize(), and an exception thrown on the finalizer thread is unhandled by definition, so it
// takes the process down at a moment unrelated to the leak. Measured on this backend at 2.1s into
// the main menu: Silk.NET's SymbolLoadingException("Native symbol not found (Symbol:
// glDeleteBuffers)") out of Submesh.Finalize, for a submesh whose owner had already replaced it.
//
// Note this is a property of the loader and not of the abstraction's design - the ANGLE backend's
// eglGetProcAddress is context-independent and resolves on any thread, so its finalizers run their
// GL calls from the wrong thread and appear to work. Two things are worth saying about that: it is
// why this problem only appears now, and it does not make the ANGLE path correct - an
// eglMakeCurrent on one thread with a context owned by another is undefined by spec and merely
// happens to work through ANGLE's emulation. The queue below is the right fix for both.
using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace NFMWorld.Graphics.DesktopGL;

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
/// a texture and a framebuffer at once, and nothing can tell them apart after the fact.
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
