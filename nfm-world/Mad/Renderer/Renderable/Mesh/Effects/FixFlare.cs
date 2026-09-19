using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorld;

public class FixFlare : IDisposable, IImmediateRenderElement
{
    private readonly BackendCar _car;
    private readonly CarVisual _visual;
    private readonly IGraphicsDevice _graphicsDevice;

    private readonly PositionColorVertex[] _verts = new PositionColorVertex[16];
    private int _vertexCount;
    private static readonly ushort[] Indices =
    [
        // Outer octagon (verts 0-7)  — triangle fan → list
        0,1,2, 0,2,3, 0,3,4, 0,4,5, 0,5,6, 0,6,7,
        // Inner octagon (verts 8-15)
        8,9,10, 8,10,11, 8,11,12, 8,12,13, 8,13,14, 8,14,15
    ];
    private int _indexCount = 36;
    // Upload happens in Render() rather than SetFixFx() - see Sparks.cs's identical pattern for why.
    private bool _dirty;
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;

    public FixFlare(BackendCar car, CarVisual visual, IGraphicsDevice graphicsDevice)
    {
        _car = car;
        _visual = visual;
        _graphicsDevice = graphicsDevice;

        var maxVertexBytes = _verts.Length * PositionColorVertex.Stride;
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, maxVertexBytes), new byte[maxVertexBytes]);
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, Indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)Indices));
    }

    public void DeleteFixFx()
    {
        _vertexCount = 0;
    }

    public void SetFixFx(int fcnt)
    {
        // ──────────────────────────────────────────────────────
        // Step 1: 4 wheel anchors → world space
        //   The original does:  anchor[i] = keyx[i]+x, grat+y, keyz[i]+z
        //   then rotates through model-local XY, ZY, XZ.
        //   We do the same, but stop BEFORE camera rotations.
        // ──────────────────────────────────────────────────────
        var anchors = new InlineArray4<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            anchors[i] = new Vector3(
                (float)_car.Wheels[i].Position.X,
                _car.GroundAt,
                (float)_car.Wheels[i].Position.Z);
        }

        // RotateXY(anchors, carCenter, xyDeg);
        // RotateZY(anchors, carCenter, zyDeg);
        // RotateXZ(anchors, carCenter, xzDeg);
        // ── STOP: do NOT apply camera Medium.xz / Medium.zy here ──

        // ──────────────────────────────────────────────────────
        // Step 2: compute spans from rotated wheel anchors
        //   Matches the O(n²) max-difference loop exactly.
        // ──────────────────────────────────────────────────────
        float spanX = 0, spanY = 0;
        float maxDistSq = 0;
        for (int a = 0; a < 4; a++)
        {
            for (int b = 0; b < 4; b++)
            {
                float dx = MathF.Abs(anchors[a].X - anchors[b].X);
                float dy = MathF.Abs(anchors[a].Y - anchors[b].Y);
                if (dx > spanX) spanX = dx;
                if (dy > spanY) spanY = dy;

                float d2 = dx * dx + dy * dy;
                if (d2 > maxDistSq) maxDistSq = d2;
            }
        }
        float spanDiag = MathF.Sqrt(maxDistSq) / 1.5f;
        spanX = MathF.Max(spanX, spanDiag);
        spanY = MathF.Max(spanY, spanDiag);

        // ──────────────────────────────────────────────────────
        // Step 3: build world-space octagon vertices
        //   Vertices are in the XY plane at the car's Z position.
        //   The World matrix will billboard them toward the camera.
        //   (They're pre-built in world space so we can apply
        //    fcnt-dependent screen-space rotation if needed.)
        // ──────────────────────────────────────────────────────
        var outer = BuildOctagonWorld(spanX, spanY,
            0.8f, 1.92f, 2.4f, 5.67f);   // outer divisors
        var inner = BuildOctagonWorld(spanX, spanY,
            1.0f, 2.4f, 4.0f, 9.6f);     // inner divisors (tighter)

        // fcnt-dependent rotation (applied in world space around car center Z axis)
        float rotDeg = 0;
        if      (fcnt == 3 || fcnt == 4) rotDeg =  22;
        else if (fcnt == 6 || fcnt == 7) rotDeg = -22;

        if (rotDeg != 0)
        {
            float rad = MathHelper.ToRadians(rotDeg);
            float c = MathF.Cos(rad), s = MathF.Sin(rad);
            for (int i = 0; i < 8; i++)
            {
                RotatePoint(ref outer[i], c, s);
                RotatePoint(ref inner[i], c, s);
            }
        }

        // ──────────────────────────────────────────────────────
        // Step 4: colors (exact match to original)
        // ──────────────────────────────────────────────────────
        Color outerColor = new Color(
            Math.Clamp((int)(191 + 191 * (World.Snap[0] / 350f)), 0, 255),
            Math.Clamp((int)(232 + 232 * (World.Snap[1] / 350f)), 0, 255),
            Math.Clamp((int)(255 + 255 * (World.Snap[2] / 350f)), 0, 255));

        Color innerColor = new Color(
            Math.Clamp((int)(213 + 213 * (World.Snap[0] / 350f)), 0, 255),
            Math.Clamp((int)(239 + 239 * (World.Snap[1] / 350f)), 0, 255),
            Math.Clamp((int)(255 + 255 * (World.Snap[2] / 350f)), 0, 255));

        // ──────────────────────────────────────────────────────
        // Step 5: stage for upload (Render() does the actual GPU upload)
        // ──────────────────────────────────────────────────────
        for (int i = 0; i < 8; i++)
        {
            _verts[i]     = new PositionColorVertex(outer[i], outerColor);
            _verts[i + 8] = new PositionColorVertex(inner[i], innerColor);
        }

        _vertexCount = 16;
        _dirty = true;
    }

    /// <summary>
    /// Builds an 8-vertex octagon around `center` in the XY plane at center.Z.
    /// Matches the original vertex layout:
    ///
    ///   v0: X-left    Y-down       v4: X+right   Y-up
    ///   v1: X-left    Y-up         v5: X+right   Y-down
    ///   v2: X-midleft Y+farup      v6: X+midright Y+fardown
    ///   v3: X+midright Y+farup     v7: X-midleft  Y+fardown
    ///
    /// divXX = divisor for "narrow" axis, divYY = divisor for "wide" axis
    /// randXX = random factor divisor for X, randYY = for Y
    /// </summary>
    private static InlineArray8<Vector3> BuildOctagonWorld(
        float spanX, float spanY,
        float divNarrow, float divWide,
        float randDivNarrow, float randDivWide)
    {
        // Outer: divNarrow=0.8, divWide=1.92, randDivNarrow=2.4, randDivWide=5.67
        // Inner: divNarrow=1.0, divWide=2.4,  randDivNarrow=4.0, randDivWide=9.6

        // ── Pre-generate 8 random values (each call to Medium.random() is independent) ──
        var rx = new InlineArray8<float>();
        var ry = new InlineArray8<float>();
        for (int i = 0; i < 8; i++)
        {
            rx[i] = URandom.Single();
            ry[i] = URandom.Single();
        }

        var result = new InlineArray8<Vector3>();

        result[0] = new Vector3(
            -spanX / divNarrow - rx[0] * (spanX / randDivNarrow),
            -spanY / divWide - ry[0] * (spanY / randDivWide),
            0);
        result[1] = new Vector3(
            -spanX / divNarrow - rx[1] * (spanX / randDivNarrow),
            +spanY / divWide + ry[1] * (spanY / randDivWide),
            0);
        result[2] = new Vector3(
            -spanX / divWide - rx[2] * (spanX / randDivWide),
            +spanY / divNarrow + ry[2] * (spanY / randDivNarrow),
            0);
        result[3] = new Vector3(
            +spanX / divWide + rx[3] * (spanX / randDivWide),
            +spanY / divNarrow + ry[3] * (spanY / randDivNarrow),
            0);
        result[4] = new Vector3(
            +spanX / divNarrow + rx[4] * (spanX / randDivNarrow),
            +spanY / divWide + ry[4] * (spanY / randDivWide),
            0);
        result[5] = new Vector3(
            +spanX / divNarrow + rx[5] * (spanX / randDivNarrow),
            -spanY / divWide - ry[5] * (spanY / randDivWide),
            0);
        result[6] = new Vector3(
            +spanX / divWide + rx[6] * (spanX / randDivWide),
            -spanY / divNarrow - ry[6] * (spanY / randDivNarrow),
            0);
        result[7] = new Vector3(
            -spanX / divWide - rx[7] * (spanX / randDivWide),
            -spanY / divNarrow - ry[7] * (spanY / randDivNarrow),
            0);

        return result;
    }

    private static void RotatePoint(ref Vector3 pt, float cos, float sin)
    {
        float dx = pt.X;
        float dy = pt.Y;
        pt.X = dx * cos - dy * sin;
        pt.Y = dx * sin + dy * cos;
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (_vertexCount == 0 || _indexCount == 0)
        {
            return;
        }

        if (_dirty)
        {
            cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes(_verts.AsSpan(0, _vertexCount)));
            _dirty = false;
        }

        var p = Effects.ParticleParameters;

        cb.SetPipeline(Effects.ParticleNoDepthPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        p.World.SetValue(cb, Matrix.CreateBillboard(
            (Vector3)_visual.Position,
            camera.Position,
            Vector3.Up,
            null));
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);

        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: _indexCount / 3);
    }

    private void ReleaseUnmanagedResources()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~FixFlare()
    {
        ReleaseUnmanagedResources();
    }
}
