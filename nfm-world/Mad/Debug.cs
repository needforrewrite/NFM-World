using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public static class Debug
{
    private static readonly BasicEffect VertexEffect = new(GameSparker.GraphicsDevice) { VertexColorEnabled = true };
    private static VertexPositionColor[] _selectionOutlineVertices = [];
    private static readonly Dictionary<Rad3d, Vector3[]> SelectionOutlineLocalEdges = new(ReferenceEqualityComparer.Instance);

    public static void RenderHighlights(IEnumerable<StageObject> pieces, Camera activeCamera)
    {
        VertexEffect.View = activeCamera.ViewMatrix;
        VertexEffect.Projection = activeCamera.ProjectionMatrix;
        VertexEffect.World = Matrix.Identity;

        int neededVertices = 0;
        foreach (var piece in pieces)
        {
            neededVertices += 24;
        }

        if (neededVertices == 0)
            return;

        if (_selectionOutlineVertices.Length < neededVertices)
            _selectionOutlineVertices = new VertexPositionColor[neededVertices];

        var color = new Color(1.0f, 1.0f, 0.0f, 1.0f);
        int cursor = 0;

        foreach (var piece in pieces)
        {
            var yaw = -piece.Rotation.Yaw.Radians;
            var pitch = piece.Rotation.Pitch.Radians;
            var roll = piece.Rotation.Roll.Radians;
            var rotationMatrix =
                Matrix.CreateRotationY((float)yaw) *
                Matrix.CreateRotationX((float)pitch) *
                Matrix.CreateRotationZ((float)roll);

            var position = new Vector3(
                (float)piece.Position.X,
                (float)piece.Position.Y,
                (float)piece.Position.Z);

            var localOutline = GetOrCreateOutlineEdges(piece.Rad);
            for (int i = 0; i < localOutline.Length; i++)
            {
                var world = Vector3.Transform(localOutline[i], rotationMatrix) + position;
                _selectionOutlineVertices[cursor++] = new VertexPositionColor(world, color.ToXna());
            }
        }

        if (cursor == 0)
            return;

        var oldDepthStencilState = GameSparker.GraphicsDevice.DepthStencilState;
        GameSparker.GraphicsDevice.DepthStencilState = DepthStencilState.None;

        foreach (var pass in VertexEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GameSparker.GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _selectionOutlineVertices,
                0,
                cursor / 2);
        }

        GameSparker.GraphicsDevice.DepthStencilState = oldDepthStencilState;
    }
    
    private static Vector3[] GetOrCreateOutlineEdges(Rad3d rad)
    {
        if (SelectionOutlineLocalEdges.TryGetValue(rad, out var cached))
            return cached;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        float maxZ = float.MinValue;

        foreach (var poly in rad.Polys)
        {
            foreach (var p in poly.Points)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
                maxZ = Math.Max(maxZ, p.Z);
            }
        }

        // Fallback for malformed assets with no poly points.
        if (minX > maxX)
        {
            float r = rad.MaxRadius > 0 ? rad.MaxRadius : 500f;
            minX = -r;
            minY = -r;
            minZ = -r;
            maxX = r;
            maxY = r;
            maxZ = r;
        }

        var c0 = new Vector3(minX, minY, minZ);
        var c1 = new Vector3(maxX, minY, minZ);
        var c2 = new Vector3(maxX, maxY, minZ);
        var c3 = new Vector3(minX, maxY, minZ);
        var c4 = new Vector3(minX, minY, maxZ);
        var c5 = new Vector3(maxX, minY, maxZ);
        var c6 = new Vector3(maxX, maxY, maxZ);
        var c7 = new Vector3(minX, maxY, maxZ);

        // 12 line segments represented as 24 vertices.
        var lines = new[]
        {
            c0, c1, c1, c2, c2, c3, c3, c0,
            c4, c5, c5, c6, c6, c7, c7, c4,
            c0, c4, c1, c5, c2, c6, c3, c7
        };

        SelectionOutlineLocalEdges[rad] = lines;
        return lines;
    }

    public static void RenderGizmo(Vector3 gizmoPos, Camera activeCamera, ref GizmoAxis gizmoHovered, ref GizmoAxis gizmoDragging, Vector2 mousePos)
    {
        var piecePos = gizmoPos;
        var gizmoMetrics = ComputeGizmoMetrics(piecePos, activeCamera);
        var xEnd = piecePos + new Vector3(gizmoMetrics.ArrowLength, 0, 0);
        // Y arrow points up in world space (negative Y in FNA because Y is flipped)
        var yEnd = piecePos + new Vector3(0, -gizmoMetrics.ArrowLength, 0);
        var zEnd = piecePos + new Vector3(0, 0, gizmoMetrics.ArrowLength);
        
        var oldDepth = GameSparker.GraphicsDevice.DepthStencilState;
        GameSparker.GraphicsDevice.DepthStencilState = DepthStencilState.None;
        
        VertexEffect.View = activeCamera.ViewMatrix;
        VertexEffect.Projection = activeCamera.ProjectionMatrix;
        VertexEffect.World = Matrix.Identity;
        
        // Colors: red=X, yellow=Y(up), blue=Z, green=RotY ring
        var colX = (gizmoHovered == GizmoAxis.X || gizmoDragging == GizmoAxis.X
            ? new Color(1f, 0.6f, 0.6f, 1f)
            : new Color(1f, 0.1f, 0.1f, 1f)).ToXna();
        var colY = (gizmoHovered == GizmoAxis.Y || gizmoDragging == GizmoAxis.Y
            ? new Color(1f, 1f, 0.6f, 1f)
            : new Color(1f, 0.9f, 0.1f, 1f)).ToXna();
        var colZ = (gizmoHovered == GizmoAxis.Z || gizmoDragging == GizmoAxis.Z
            ? new Color(0.6f, 0.6f, 1f, 1f)
            : new Color(0.1f, 0.1f, 1f, 1f)).ToXna();
        var colRot = (gizmoHovered == GizmoAxis.RotY || gizmoDragging == GizmoAxis.RotY
            ? new Color(0.6f, 1f, 0.6f, 1f)
            : new Color(0.1f, 0.9f, 0.1f, 1f)).ToXna();
        
        // Arrowhead side fins and tip offsets for each axis
        var xSide     = new Vector3(0, gizmoMetrics.ArrowThickness * 2, 0);
        var ySide     = new Vector3(gizmoMetrics.ArrowThickness * 2, 0, 0);
        var zSide     = new Vector3(gizmoMetrics.ArrowThickness * 2, 0, 0);
        var xTipOffset = new Vector3(gizmoMetrics.ArrowLength * 0.15f, 0, 0);
        var yTipOffset = new Vector3(0, -gizmoMetrics.ArrowLength * 0.15f, 0); // negative = upward
        var zTipOffset = new Vector3(0, 0, gizmoMetrics.ArrowLength * 0.15f);
        
        List<VertexPositionColor> verts =
        [
            new(piecePos, colX),
            new(xEnd, colX),
            // X arrowhead
            new(xEnd - xTipOffset + xSide, colX),
            new(xEnd, colX),
            new(xEnd - xTipOffset - xSide, colX),
            new(xEnd, colX),
            // Y arrow shaft (points up)
            new(piecePos, colY),
            new(yEnd, colY),
            // Y arrowhead
            new(yEnd - yTipOffset + ySide, colY),
            new(yEnd, colY),
            new(yEnd - yTipOffset - ySide, colY),
            new(yEnd, colY),
            // Z arrow shaft
            new(piecePos, colZ),
            new(zEnd, colZ),
            // Z arrowhead
            new(zEnd - zTipOffset + zSide, colZ),
            new(zEnd, colZ),
            new(zEnd - zTipOffset - zSide, colZ),
            new(zEnd, colZ)
        ];

        // Rotation ring (circle of line segments at piece Y level)
        const int ringSegs = 32;
        for (int i = 0; i < ringSegs; i++)
        {
            float a0 = i / (float)ringSegs * (2f * MathF.PI);
            float a1 = (i + 1) / (float)ringSegs * (2f * MathF.PI);
            verts.Add(new VertexPositionColor(piecePos + new Vector3(MathF.Cos(a0) * gizmoMetrics.RotRadius, 0, MathF.Sin(a0) * gizmoMetrics.RotRadius), colRot));
            verts.Add(new VertexPositionColor(piecePos + new Vector3(MathF.Cos(a1) * gizmoMetrics.RotRadius, 0, MathF.Sin(a1) * gizmoMetrics.RotRadius), colRot));
        }
        
        var arr = verts.ToArray();
        
        // Compute camera-relative perpendicular offsets so lines appear ~5px wide at any distance.
        // Camera right/up come from the columns of the view matrix (orthonormal rotation part).
        float dist = Vector3.Distance(activeCamera.Position, piecePos);
        float halfFovRad = activeCamera is PerspectiveCamera perspectiveCamera ? perspectiveCamera.Fov * MathF.PI / 180f * 0.5f : 60f;
        // World units that map to 1 screen pixel at this distance
        float pixelSize = dist * MathF.Tan(halfFovRad) * 2f / GameSparker.GraphicsDevice.Viewport.Height;
        float s = pixelSize * 2f; // 2 px each side = ~5px total visual width
        var camRight = new Vector3(activeCamera.ViewMatrix.M11, activeCamera.ViewMatrix.M21, activeCamera.ViewMatrix.M31);
        var camUp    = new Vector3(activeCamera.ViewMatrix.M12, activeCamera.ViewMatrix.M22, activeCamera.ViewMatrix.M32);
        var thickOffsets = new[]
        {
            Vector3.Zero,
            camRight *  s, camRight * -s,
            camUp    *  s, camUp    * -s,
        };
        
        foreach (var offset in thickOffsets)
        {
            var offsetArr = offset == Vector3.Zero
                ? arr
                : arr.Select(v => new VertexPositionColor(v.Position + offset, v.Color)).ToArray();
            foreach (var pass in VertexEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GameSparker.GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, offsetArr, 0, offsetArr.Length / 2);
            }
        }
        
        GameSparker.GraphicsDevice.DepthStencilState = oldDepth;
        
        // Update hover state based on screen-space distances
        UpdateGizmoHover(piecePos, gizmoMetrics, ref gizmoDragging, ref gizmoHovered, activeCamera, mousePos);
    }
    
    public static void UpdateGizmoHover(Vector3 piecePos, GizmoMetrics gizmoMetrics, ref GizmoAxis gizmoDragging, ref GizmoAxis gizmoHovered, Camera activeCamera, Vector2 mousePos)
    {
        if (gizmoDragging != GizmoAxis.None) return;

        float closestDist = 20f; // hover threshold in pixels
        gizmoHovered = GizmoAxis.None;
        
        // Check X arrow
        if (WorldToScreen(piecePos, out var ss0, activeCamera) &&
            WorldToScreen(piecePos + new Vector3(gizmoMetrics.ArrowLength, 0, 0), out var ss1, activeCamera))
        {
            float d = DistanceToSegment(mousePos, ss0, ss1);
            if (d < closestDist) { closestDist = d; gizmoHovered = GizmoAxis.X; }
        }
        // Check Y arrow (up)
        if (WorldToScreen(piecePos, out ss0, activeCamera) &&
            WorldToScreen(piecePos + new Vector3(0, -gizmoMetrics.ArrowLength, 0), out ss1, activeCamera))
        {
            float d = DistanceToSegment(mousePos, ss0, ss1);
            if (d < closestDist) { closestDist = d; gizmoHovered = GizmoAxis.Y; }
        }
        // Check Z arrow
        if (WorldToScreen(piecePos, out ss0, activeCamera) &&
            WorldToScreen(piecePos + new Vector3(0, 0, gizmoMetrics.ArrowLength), out ss1, activeCamera))
        {
            float d = DistanceToSegment(mousePos, ss0, ss1);
            if (d < closestDist) { closestDist = d; gizmoHovered = GizmoAxis.Z; }
        }
        // Check rotation ring (test each segment)
        const int ringSegs = 32;
        for (int i = 0; i < ringSegs; i++)
        {
            float a0 = i / (float)ringSegs * (2f * MathF.PI);
            float a1 = (i + 1) / (float)ringSegs * (2f * MathF.PI);
            var p0 = piecePos + new Vector3(MathF.Cos(a0) * gizmoMetrics.RotRadius, 0, MathF.Sin(a0) * gizmoMetrics.RotRadius);
            var p1 = piecePos + new Vector3(MathF.Cos(a1) * gizmoMetrics.RotRadius, 0, MathF.Sin(a1) * gizmoMetrics.RotRadius);
            if (WorldToScreen(p0, out ss0, activeCamera) && WorldToScreen(p1, out ss1, activeCamera))
            {
                float d = DistanceToSegment(mousePos, ss0, ss1);
                if (d < closestDist) { closestDist = d; gizmoHovered = GizmoAxis.RotY; }
            }
        }
    }
    
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
        t = Math.Clamp(t, 0f, 1f);
        return Vector2.Distance(p, a + t * ab);
    }

    // Project a world-space point to screen coordinates (returns false if behind camera)
    public static bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos, Camera activeCamera)
    {
        var viewport = GameSparker.GraphicsDevice.Viewport;
        var clip = Vector4.Transform(new Vector4(worldPos, 1f), activeCamera.ViewMatrix * activeCamera.ProjectionMatrix);
        screenPos = default;
        if (clip.W <= 0f) return false;
        var ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W); // XNA Vector3
        screenPos = new Vector2(
            (ndc.X + 1f) * 0.5f * viewport.Width,
            (1f - ndc.Y) * 0.5f * viewport.Height);
        return true;
    }
    
    private const float GIZMO_ARROW_LENGTH = 600f;
    private const float GIZMO_ARROW_THICKNESS = 12f;
    private const float GIZMO_ROT_RADIUS = 400f;
    private const float GIZMO_TARGET_AXIS_PIXELS = 120f;
    private const float GIZMO_MAX_SCALE = 8f;
    public static GizmoMetrics ComputeGizmoMetrics(Vector3 piecePos, Camera activeCamera)
    {
        float scale = 1f;

        if (TryGetProjectedAxisLength(piecePos, GIZMO_ARROW_LENGTH, out var axisPx, activeCamera) && axisPx > 1f)
        {
            // Keep the gizmo readable at long distances by targeting a minimum on-screen axis length.
            scale = Math.Clamp(GIZMO_TARGET_AXIS_PIXELS / axisPx, 1f, GIZMO_MAX_SCALE);
        }

        return new GizmoMetrics(
            GIZMO_ARROW_LENGTH * scale,
            GIZMO_ARROW_THICKNESS * scale,
            GIZMO_ROT_RADIUS * scale);
    }

    public static bool TryGetProjectedAxisLength(Vector3 piecePos, float axisLength, out float projectedAxisLength, Camera activeCamera)
    {
        projectedAxisLength = 0f;

        if (!WorldToScreen(piecePos, out var ss0, activeCamera))
            return false;

        if (WorldToScreen(piecePos + new Vector3(axisLength, 0, 0), out var ssX, activeCamera))
            projectedAxisLength = Math.Max(projectedAxisLength, Vector2.Distance(ss0, ssX));
        if (WorldToScreen(piecePos + new Vector3(0, -axisLength, 0), out var ssY, activeCamera))
            projectedAxisLength = Math.Max(projectedAxisLength, Vector2.Distance(ss0, ssY));
        if (WorldToScreen(piecePos + new Vector3(0, 0, axisLength), out var ssZ, activeCamera))
            projectedAxisLength = Math.Max(projectedAxisLength, Vector2.Distance(ss0, ssZ));

        return projectedAxisLength > 0f;
    }

    public static void RenderGhost(Rad3d rad, Matrix world, Color fillColor, Color wireColor, Camera activeCamera)
    {
        var oldDepth = GameSparker.GraphicsDevice.DepthStencilState;
        var oldBlend = GameSparker.GraphicsDevice.BlendState;
        var oldRasterizer = GameSparker.GraphicsDevice.RasterizerState;
        
        VertexEffect.View = activeCamera.ViewMatrix;
        VertexEffect.Projection = activeCamera.ProjectionMatrix;
        VertexEffect.World = world;
        
        // Semi-transparent fill (both faces so it looks solid from any angle)
        GameSparker.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GameSparker.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        GameSparker.GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
        
        var fillVerts = new List<VertexPositionColor>();
        var fillInds = new List<int>();
        
        foreach (var poly in rad.Polys)
        {
            if (poly.Points.Length < 3) continue;

            var startIndex = (uint)fillVerts.Count;
            for (int i = 0; i < poly.Points.Length; i++)
            {
                fillVerts.Add(new(poly.Points[i], fillColor.ToXna()));
            }
            
            foreach (var idx in poly.Triangles)
            {
                fillInds.Add((int)(startIndex + idx));
            }
        }
        
        if (fillVerts.Count > 0)
        {
            foreach (var pass in VertexEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GameSparker.GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    fillVerts.ToArray(),
                    0,
                    fillVerts.Count,
                    fillInds.ToArray(),
                    0,
                    fillInds.Count / 3);
            }
        }
        
        // Bright wireframe on top (depth-ignore so it's always visible)
        GameSparker.GraphicsDevice.DepthStencilState = DepthStencilState.None;
        GameSparker.GraphicsDevice.BlendState = BlendState.Opaque;
        var wireVerts = new List<VertexPositionColor>();
        
        foreach (var poly in rad.Polys)
        {
            if (poly.Points.Length < 2) continue;
            for (int i = 0; i < poly.Points.Length; i++)
            {
                int next = (i + 1) % poly.Points.Length;
                wireVerts.Add(new(poly.Points[i], wireColor.ToXna()));
                wireVerts.Add(new(poly.Points[next], wireColor.ToXna()));
            }
        }
        
        if (wireVerts.Count > 0)
        {
            foreach (var pass in VertexEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GameSparker.GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, wireVerts.ToArray(), 0, wireVerts.Count / 2);
            }
        }
        
        GameSparker.GraphicsDevice.DepthStencilState = oldDepth;
        GameSparker.GraphicsDevice.BlendState = oldBlend;
        GameSparker.GraphicsDevice.RasterizerState = oldRasterizer;
    }
}

public enum GizmoAxis { None, X, Y, Z, RotY }
public readonly record struct GizmoMetrics(float ArrowLength, float ArrowThickness, float RotRadius);