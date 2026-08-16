using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private readonly GraphicsDevice _graphicsDevice;

    private readonly VertexPositionColor[] _verts = new VertexPositionColor[16];
    private int _vertexCount;
    private static readonly short[] Indices =
    [
        // Outer octagon (verts 0-7)  — triangle fan → list
        0,1,2, 0,2,3, 0,3,4, 0,4,5, 0,5,6, 0,6,7,
        // Inner octagon (verts 8-15)
        8,9,10, 8,10,11, 8,11,12, 8,12,13, 8,13,14, 8,14,15
    ];
    private int _indexCount = 36;
    private readonly DynamicVertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;

    public FixFlare(BackendCar car, CarVisual visual, GraphicsDevice graphicsDevice)
    {
        _car = car;
        _visual = visual;
        _graphicsDevice = graphicsDevice;
        
        _vertexBuffer = new DynamicVertexBuffer(graphicsDevice, VertexPositionColor.VertexDeclaration, 16, BufferUsage.WriteOnly);
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 36, BufferUsage.WriteOnly);
        _vertexBuffer.SetDataEXT(_verts);
        _indexBuffer.SetDataEXT(Indices);
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
        // Step 5: upload to vertex buffer
        // ──────────────────────────────────────────────────────
        for (int i = 0; i < 8; i++)
        {
            _verts[i]     = new VertexPositionColor(outer[i], outerColor);
            _verts[i + 8] = new VertexPositionColor(inner[i], innerColor);
        }

        _vertexBuffer.SetDataEXT(_verts);
        _vertexCount = 16;
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

    public void Render(Camera camera, Lighting? _)
    {
        if (_vertexCount == 0 || _indexCount == 0)
        {
            return;
        }
        
        Effects.Dust.World = Matrix.CreateBillboard(
            (Vector3)_visual.Position,
            camera.Position,
            Vector3.Up,
            null);
        Effects.Dust.View = camera.ViewMatrix;
        Effects.Dust.Projection = camera.ProjectionMatrix;
        
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
        foreach (var pass in Effects.Dust.CurrentTechnique.Passes)
        {
            pass.Apply();
            
            _graphicsDevice.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                0,
                _vertexCount,
                0,
                _indexCount / 3
            );
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
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