using System;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using Color = Stride.Core.Mathematics.Color;

namespace NFMWorld.Mad;

public interface IPolygonShading
{
    void Shade(
        Matrix worldViewProjection,
        Transform parent,
        ref Color3 color,
        ReadOnlySpan<Vector3> pointsWorld,
        ReadOnlySpan<int> xScreen,
        ReadOnlySpan<int> yScreen,
        ReadOnlySpan<float> zDepth,
        float distanceToCamera
    );
}

public class FullBrightShading : IPolygonShading
{
    public void Shade(
        Matrix worldViewProjection,
        Transform parent,
        ref Color3 color,
        ReadOnlySpan<Vector3> pointsWorld,
        ReadOnlySpan<int> xScreen,
        ReadOnlySpan<int> yScreen,
        ReadOnlySpan<float> zDepth,
        float distanceToCamera
    )
    {
    }
}

public class OmarPolygonShading : IPolygonShading
{
    private float _projf = 1.0F;
    private float _deltaf = 1.0F;
    private float[] _hsb;
    private readonly Vector3 _normal;
    
    private static Vector3 LightDirection => new(0, -1, 0);

    public OmarPolygonShading(MeshPoly poly)
    {
        _hsb = new float[3];
        Colors.RGBtoHSB(poly.Color.R, poly.Color.G, poly.Color.B, out _hsb[0], out _hsb[1], out _hsb[2]);
        
        _normal = CalculateNormal(poly.Points);
    }
    
    // Newell's Method
    private static Vector3 CalculateNormal(ReadOnlySpan<Vector3> points)
    {
        var normal = Vector3.Zero;

        for (var i = 0; i < points.Length; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Length];

            normal.X += (current.Y - next.Y) * (current.Z + next.Z);
            normal.Y += (current.Z - next.Z) * (current.X + next.X);
            normal.Z += (current.X - next.X) * (current.Y + next.Y);
        }

        normal.Normalize();
        return normal;
    }

    private static Color3 SnapColor(Color3 color)
    {
        return color with
        {
            R = (short)Math.Clamp(color.R + color.R * (World.Snap.R / 100.0F), 0, 255),
            G = (short)Math.Clamp(color.G + color.G * (World.Snap.G / 100.0F), 0, 255),
            B = (short)Math.Clamp(color.B + color.B * (World.Snap.B / 100.0F), 0, 255),
        };
    }

    public void Shade(
        Matrix worldViewProjection,
        Transform parent,
        ref Color3 color,
        ReadOnlySpan<Vector3> pointsWorld,
        ReadOnlySpan<int> xScreen,
        ReadOnlySpan<int> yScreen,
        ReadOnlySpan<float> zDepth,
        float distanceToCamera
    )
    {
        var normal = Vector3.TransformNormal(_normal, parent.WorldMatrix);
        
        var diffuse = MathF.Abs(Vector3.Dot(normal, LightDirection));
        var colorVector = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f) * (0.37f + 0.63f * diffuse);
        color = new Color3((short)(colorVector.X * 255f), (short)(colorVector.Y * 255f), (short)(colorVector.Z * 255f));
        color = SnapColor(color);
    }
}

public class MeshPoly : IComparable<MeshPoly>
{
    public Color3 Color;
    public int? ColNum;
    public PolyType PolyType;
    public LineType? LineType;
    public Vector3[] Points;

    public int Gr
    {
        get;
        set
        { 
            field = value;
            _grMult = value != 0 ? MathF.Sqrt(value) : 1;
        }
    }

    public int Fs;

    private float DistanceToCamera;

    private float _grMult;
    
    public IPolygonShading Shading;

    public BoundingBoxExt BoundingBox { get; private set; }
    
    public MeshPoly(Rad3dPoly poly) : this(poly.Color, poly.ColNum, poly.PolyType, poly.LineType, poly.Points, poly.Gr, poly.Fs)
    {
    }

    public MeshPoly(Color3 color, int? colNum, PolyType polyType, LineType? lineType, IReadOnlyList<Vector3> points, int gr, int fs)
    {
        Color = color;
        ColNum = colNum;
        PolyType = polyType;
        LineType = lineType;
        Points = points.ToArray();
        Gr = gr;
        Fs = fs;
        Shading = new OmarPolygonShading(this);
        RecalculateBounds();
    }

    public MeshPoly(MeshPoly basePoly)
    {
        Color = basePoly.Color;
        ColNum = basePoly.ColNum;
        PolyType = basePoly.PolyType;
        LineType = basePoly.LineType;
        Points = basePoly.Points.ToArray();
        Gr = basePoly.Gr;
        Fs = basePoly.Fs;
        Shading = basePoly.Shading;
        RecalculateBounds();
    }

    public void RecalculateBounds()
    {
        BoundingBox = (BoundingBoxExt) Stride.Core.Mathematics.BoundingBox.FromPoints(Points);
    }

    public void Render(Matrix worldViewProjection, Transform parent, Camera camera)
    {
        // translate and rotate bbox to transform position and rotation
        var bboxWorld = BoundingBox;
        bboxWorld.Transform(parent.WorldMatrix);
        if (!camera.Frustum.Contains(in bboxWorld))
        {
            return;
        }

        // project points onto screen space
        var points = Points.AsSpan();
        
        Span<Vector3> pointsWorld = stackalloc Vector3[points.Length];
        Span<int> xScreen = stackalloc int[points.Length];
        Span<int> yScreen = stackalloc int[points.Length];
        Span<float> zDepth = stackalloc float[points.Length];
        float depthAccumulated = 0;
        for (var i = 0; i < points.Length; i++)
        {
            // translate and rotate points to transform position and rotation
            var point = points[i];
            pointsWorld[i] = Vector3.TransformCoordinate(point, parent.WorldMatrix);
            var pointScreen = Vector3.TransformCoordinate(point, worldViewProjection);
            // convert to screen space
            var x = (pointScreen.X + 1) * 0.5f * camera.Width;
            var y = (1 - (pointScreen.Y + 1) * 0.5f) * camera.Height;
            xScreen[i] = (int)x;
            yScreen[i] = (int)y;
            zDepth[i] = pointScreen.Z;
            depthAccumulated += pointScreen.Z;
        }
        
        DistanceToCamera = (depthAccumulated / points.Length) * _grMult;

        var color = Color;
        Shading.Shade(worldViewProjection, parent, ref color, pointsWorld, xScreen, yScreen, zDepth, DistanceToCamera);
        
        G.SetColor(color);
        G.FillPolygon(xScreen, yScreen, points.Length);
    }

    public int CompareTo(MeshPoly? other)
    {
        if (other == null) return 1;
        return other.DistanceToCamera.CompareTo(DistanceToCamera);
    }
}

public class Transform : IComparable<Transform>
{
    public Vector3 Position { get; private set; } = Vector3.Zero;
    public Euler Rotation { get; private set; } = new();
    
    public int Grounded = 1;
    protected float _grMult = 1;

    public float DistanceToCamera;

    public Matrix WorldMatrix { get; private set; }

    public Transform()
    {
        Move(Position, Rotation);
    }

    public void Move(Vector3 position, Euler rotation)
    {
        Position = position;
        Rotation = rotation;
        RecalculateWorldMatrix();
    }

    private void RecalculateWorldMatrix()
    {
        WorldMatrix = Matrix.RotationQuaternion(Rotation) * Matrix.Translation(Position);
    }

    public int CompareTo(Transform? other)
    {
        if (other == null) return 1;
        return other.DistanceToCamera.CompareTo(DistanceToCamera);
    }
}

public class FixHoop(Mesh baseMesh, Vector3 position, Euler rotation) : Mesh(baseMesh, position, rotation)
{
    public bool Rotated;
}

public interface IRenderable
{
    void Render(Camera camera);
}

public class Mesh : Transform, IRenderable
{
    private Color3[] Colors;
    private CarStats Stats;
    private Rad3dWheelDef[] Wheels;
    private Rad3dRimsDef? Rims;
    private Rad3dBoxDef[] Boxes;
    private MeshPoly[] Polys;

    public BoundingBoxExt BoundingBox;

    public void RecalculateBounds()
    {
        BoundingBox = BoundingBoxExt.Empty;
        foreach (var poly in Polys)
        {
            var polyBoundingBox = poly.BoundingBox;
            BoundingBoxExt.Merge(in BoundingBox, in polyBoundingBox, out BoundingBox);
        }
    }

    public Mesh(string code)
    {
        var rad = RadParser.ParseRad(code);
        Colors = rad.Colors;
        Stats = rad.Stats;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = Array.ConvertAll(rad.Polys, poly => new MeshPoly(poly));

        _grMult = MathF.Sqrt(Grounded);
    }
    
    public Mesh(Mesh baseMesh, Vector3 position, Euler rotation)
    {
        Colors = baseMesh.Colors;
        Stats = baseMesh.Stats;
        Wheels = baseMesh.Wheels;
        Rims = baseMesh.Rims;
        Boxes = baseMesh.Boxes;
        Polys = Array.ConvertAll(baseMesh.Polys, poly => new MeshPoly(poly));
        Grounded = baseMesh.Grounded;
        _grMult = baseMesh._grMult;

        Move(position, rotation);
    }

    public void Render(Camera camera)
    {
        Polys.AsSpan().Sort();
        
        // translate and rotate bbox to transform position and rotation
        var bboxWorld = BoundingBox;
        bboxWorld.Transform(WorldMatrix);
        if (!camera.Frustum.Contains(in bboxWorld))
        {
            return;
        }
        
        var worldViewProjection = WorldMatrix * camera.ViewProjectionMatrix;
        
        var transformedPoint = Vector3.TransformCoordinate(new Vector3(), worldViewProjection);

        DistanceToCamera = transformedPoint.Z * _grMult;
        
        foreach (var poly in Polys)
        {
            poly.Render(worldViewProjection, this, camera);
        }
    }
}

public class Camera
{
    public int Width { get; private set; } = 1280;
    public int Height { get; private set; } = 720;
    public float Fov { get; private set; } = 90f;
    
    public Vector3 Position { get; private set; } = Vector3.Zero;
    public Vector3 LookAt { get; private set; } = Vector3.UnitZ;
    public Vector3 Up  { get; private set; } = -Vector3.UnitY;

    public Matrix ViewMatrix { get; private set; }

    public Matrix ProjectionMatrix { get; private set; }

    public Matrix ViewProjectionMatrix { get; private set; }

    public BoundingFrustum Frustum { get; private set; }

    public Camera()
    {
        Reorient(Fov, Width, Height);
        Move(Position, LookAt);
    }

    public void Reorient(float fov, int width, int height)
    {
        Fov = fov;
        Width = width;
        Height = height;
        ProjectionMatrix = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(fov), width / (float)height, 50f, 1_000_000f);
        UpdateViewProjection();
    }

    public void Move(Vector3 to, Vector3 lookAt)
    {
        Position = to;
        LookAt = lookAt;
        ViewMatrix = Matrix.LookAtRH(to, lookAt, Up);
        UpdateViewProjection();
    }

    private void UpdateViewProjection()
    {
        var viewProjectionMatrix = ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        Frustum = new BoundingFrustum(in viewProjectionMatrix);
    }
}

public class FollowCamera
{
    public int FollowYOffset = 0;

    private float _bcxz;
    private Euler _angle;

    public void Follow(Camera camera, Mesh mesh, float cxz, int lookback)
    {
        // x: yaw = xz
        // y: pitch = zy
        // z: roll = xy
        _angle.Pitch = AngleSingle.FromDegrees(10);
        var i28 = 2 + Math.Abs(_bcxz) / 4;
        if (i28 > 20)
        {
            i28 = 20;
        }
        if (lookback != 0)
        {
            if (lookback == 1)
            {
                if (_bcxz < 180)
                {
                    _bcxz += i28;
                }
                if (_bcxz > 180)
                {
                    _bcxz = 180;
                }
            }
            if (lookback == -1)
            {
                if (_bcxz > -180)
                {
                    _bcxz -= i28;
                }
                if (_bcxz < -180)
                {
                    _bcxz = -180;
                }
            }
        }
        else if (Math.Abs(_bcxz) > i28)
        {
            if (_bcxz > 0)
            {
                _bcxz -= i28;
            }
            else
            {
                _bcxz += i28;
            }
        }
        else
        {
            _bcxz = 0;
        }
        cxz += _bcxz;
        _angle.Yaw = AngleSingle.FromDegrees(-cxz);

        var position = camera.Position;
        position.X = mesh.Position.X - (800 * Medium.Sin(cxz));
        position.Z = mesh.Position.Z - (800 * Medium.Cos(cxz));
        position.Y = mesh.Position.Y - 250 - FollowYOffset;
        
        // Calculate the look direction by rotating the forward vector
        var lookDirection = (_angle * Vector3.UnitZ) * 100;
        // LookAt should be a target point, not a direction - add direction to position
        var lookAtPoint = position + lookDirection;
        camera.Move(position, lookAtPoint);
    }
}

public static class World
{
    public static int Ground = 250;
    public static Color3 Snap;
}