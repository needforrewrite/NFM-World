// LLM maintained.
//
// The base-vertex draw entry points, resolved by their ES names.
//
// ES 3.0 core has no base-vertex draw, and Silk.NET's GL.DrawElementsBaseVertex is not a way to get
// one: it binds the *desktop* spelling, which ANGLE's GLES entry table carries as a stub. Calling
// it is not a no-op that silently drops the offset - it raises GL_INVALID_OPERATION and draws
// nothing (measured: 0x0502 on this ANGLE build, for both the indexed and the instanced form). So
// the desktop name has to be avoided rather than used and hoped for, and the ES names - which only
// exist as EXT/OES extension entry points - have to be resolved by hand.
//
// Why this is a separate type rather than three lines inside GlCommandBuffer: resolution is a
// per-context cost paid once, the result is two delegates that have to stay paired with the context
// that produced them, and the fallback when they are absent is a policy decision that deserves to
// be readable in one place. See GlCommandBuffer.DrawInternal for the caller.

using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.EXT;
using Silk.NET.OpenGLES.Extensions.OES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// The ES base-vertex draw entry points for one context, or null when the context exposes none.
/// </summary>
internal sealed class GlBaseVertexDraw
{
    /// <summary><c>glDrawElementsBaseVertexEXT</c>: a base vertex added to every index.</summary>
    private readonly DrawElementsBaseVertex? _drawIndexed;

    /// <summary><c>glDrawElementsInstancedBaseVertexEXT</c>: the same, once per instance.</summary>
    private readonly DrawElementsInstancedBaseVertex? _drawIndexedInstanced;

    /// <summary>
    /// Whether this context can honour a non-zero base vertex at all. False means the caller must
    /// refuse the draw rather than approximate it - see <see cref="GlCommandBuffer.DrawInternal"/>.
    /// </summary>
    internal bool IsSupported => _drawIndexed is not null && _drawIndexedInstanced is not null;

    private GlBaseVertexDraw(DrawElementsBaseVertex? drawIndexed, DrawElementsInstancedBaseVertex? drawIndexedInstanced)
    {
        _drawIndexed = drawIndexed;
        _drawIndexedInstanced = drawIndexedInstanced;
    }

    /// <summary>
    /// Resolves both entry points out of <paramref name="gl"/>.
    /// </summary>
    internal static unsafe GlBaseVertexDraw Resolve(GL gl)
    {
        if (gl.TryGetExtension(out ExtDrawElementsBaseVertex ext))
        {
            return new GlBaseVertexDraw(
                ext.DrawElementsBaseVertex,
                ext.DrawElementsInstancedBaseVertex
            );
        }

        if (gl.TryGetExtension(out OesDrawElementsBaseVertex ext2))
        {
            return new GlBaseVertexDraw(
                ext2.DrawElementsBaseVertex,
                ext2.DrawElementsInstancedBaseVertex
            );
        }

        return new GlBaseVertexDraw(null, null);
    }

    /// <summary>
    /// Issues the indexed draw. Only legal when <see cref="IsSupported"/>.
    /// </summary>
    internal unsafe void DrawIndexed(PrimitiveType mode, uint count, DrawElementsType type, void* indices, int baseVertex) =>
        _drawIndexed!(mode, count, type, indices, baseVertex);

    /// <summary>Issues the instanced indexed draw. Only legal when <see cref="IsSupported"/>.</summary>
    internal unsafe void DrawIndexedInstanced(
        PrimitiveType mode, uint count, DrawElementsType type, void* indices, uint instanceCount, int baseVertex) =>
        _drawIndexedInstanced!(mode, count, type, indices, instanceCount, baseVertex);

    private unsafe delegate void DrawElementsBaseVertex(PrimitiveType mode, uint count, DrawElementsType type, void* indices, int baseVertex);
    private unsafe delegate void DrawElementsInstancedBaseVertex(PrimitiveType mode, uint count, DrawElementsType type, void* indices, uint instanceCount, int baseVertex);
}
