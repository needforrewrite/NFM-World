using System;
using HoleyDiver;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using THREE;
using Color = Stride.Core.Mathematics.Color;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace NFMWorld.Mad;

public class Transform
{
    public virtual Vector3 Position { get; set; } = Vector3.Zero;
    public virtual Euler Rotation { get; set; } = new();
}

public class FixHoop(Mesh baseMesh, Vector3 position, Euler rotation) : Mesh(baseMesh, position, rotation)
{
    public bool Rotated;
}

public interface IContainsThreeObject
{
    public Object3D ThreeObject { get; }
    public event Action<(Object3D OldObject, Object3D NewObject)>? ThreeObjectChanged;
}

public class Mesh : Transform, IContainsThreeObject
{
    public Object3D ThreeObject
    {
        get;
        private set
        {
            var oldValue = field;
            field = value;
            ThreeObjectChanged?.Invoke((oldValue, value));
        }
    }

    public event Action<(Object3D OldObject, Object3D NewObject)>? ThreeObjectChanged;

    public Color3[] Colors;
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;
    public Rad3dBoxDef[] Boxes;
    public Rad3dPoly[] Polys;
    
    // visually wasted
    public bool Wasted;

    public int GroundAt;

    public Euler TurningWheelAngle { get; set; }

    public Mesh(string code)
    {
        var rad = RadParser.ParseRad(code);
        Colors = rad.Colors;
        Stats = rad.Stats;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = rad.Polys;

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        ThreeObject = BuildMesh();
    }
    
    public Mesh(Mesh baseMesh, Vector3 position, Euler rotation)
    {
        Colors = baseMesh.Colors;
        Stats = baseMesh.Stats;
        Wheels = baseMesh.Wheels;
        Rims = baseMesh.Rims;
        Boxes = baseMesh.Boxes;
        Polys = baseMesh.Polys;
        GroundAt = baseMesh.GroundAt;

        ThreeObject = BuildMesh();
        Position = position;
        Rotation = rotation;
    }

    private Object3D BuildMesh()
    {
        var geometry = new BufferGeometry();
        
        var positions = new List<float>();
        var normals = new List<float>();
        var colors = new List<float>();
        
        foreach (var poly in Polys)
        {
            // TODO: the result of triangulation can be cached.
            var result = PolygonTriangulator.Triangulate(Array.ConvertAll(poly.Points, input => (System.Numerics.Vector3)input));

            for (var index = 0; index < result.Triangles.Count; index += 3)
            {
                var i0 = result.Triangles[index];
                var i1 = result.Triangles[index + 1];
                var i2 = result.Triangles[index + 2];
                
                var p0 = poly.Points[i0];
                var p1 = poly.Points[i1];
                var p2 = poly.Points[i2];
                
                positions.Add(p0.X);
                positions.Add(p0.Y);
                positions.Add(p0.Z);
                positions.Add(p1.X);
                positions.Add(p1.Y);
                positions.Add(p1.Z);
                positions.Add(p2.X);;
                positions.Add(p2.Y);
                positions.Add(p2.Z);

                normals.Add(result.PlaneNormal.X);
                normals.Add(result.PlaneNormal.Y);
                normals.Add(result.PlaneNormal.Z);
                normals.Add(result.PlaneNormal.X);
                normals.Add(result.PlaneNormal.Y);
                normals.Add(result.PlaneNormal.Z);
                normals.Add(result.PlaneNormal.X);
                normals.Add(result.PlaneNormal.Y);
                normals.Add(result.PlaneNormal.Z);

                colors.Add(poly.Color.R / 255f);
                colors.Add(poly.Color.G / 255f);
                colors.Add(poly.Color.B / 255f);
                colors.Add(poly.Color.R / 255f);
                colors.Add(poly.Color.G / 255f);
                colors.Add(poly.Color.B / 255f);
                colors.Add(poly.Color.R / 255f);
                colors.Add(poly.Color.G / 255f);
                colors.Add(poly.Color.B / 255f);
            }
        }
        
        geometry.SetAttribute("position", new BufferAttribute<float>(positions.ToArray(), 3));
        geometry.SetAttribute("normal", new BufferAttribute<float>(normals.ToArray(), 3));
        geometry.SetAttribute("color", new BufferAttribute<float>(colors.ToArray(), 3));

        geometry.ComputeBoundingSphere();

        var material = new MeshPhongMaterial()
        {
            Color = THREE.Color.Hex(0xaaaaaa),
            Specular = THREE.Color.Hex(0xffffff),
            Shininess = 250,
            Side = Constants.DoubleSide,
            VertexColors = true
        };

        var mesh = new THREE.Mesh(geometry, material);

        var containerObject = new Object3D();
        containerObject.Add(mesh);

        return containerObject;
    }

    public sealed override Vector3 Position
    {
        get => ThreeObject.Position.ToStride();
        set
        {
            ThreeObject.Position.X = value.X;
            ThreeObject.Position.Y = value.Y;
            ThreeObject.Position.Z = value.Z;
        }
    }

    public sealed override Euler Rotation
    {
        get => ThreeObject.Rotation.ToMaxine();
        set
        {
            ThreeObject.Rotation.Y = -value.Yaw.Radians;
            ThreeObject.Rotation.X = value.Pitch.Radians;
            ThreeObject.Rotation.Z = value.Roll.Radians;
        }
    }
}
//
// public class Camera
// {
//     public int Width { get; private set; } = 1280;
//     public int Height { get; private set; } = 720;
//     public float Fov { get; private set; } = 90f;
//     
//     public Vector3 Position { get; private set; } = Vector3.Zero;
//     public Vector3 LookAt { get; private set; } = Vector3.UnitZ;
//     public Vector3 Up  { get; private set; } = -Vector3.UnitY;
//
//     public Matrix ViewMatrix { get; private set; }
//
//     public Matrix ProjectionMatrix { get; private set; }
//
//     public Matrix ViewProjectionMatrix { get; private set; }
//
//     public BoundingFrustum Frustum { get; private set; }
//
//     public Camera()
//     {
//         Reorient(Fov, Width, Height);
//         Move(Position, LookAt);
//     }
//
//     public void Reorient(float fov, int width, int height)
//     {
//         Fov = fov;
//         Width = width;
//         Height = height;
//         ProjectionMatrix = Matrix.PerspectiveFovRH(MathUtil.DegreesToRadians(fov), width / (float)height, 50f, 1_000_000f);
//         UpdateViewProjection();
//     }
//
//     public void Move(Vector3 to, Vector3 lookAt)
//     {
//         Position = to;
//         LookAt = lookAt;
//         ViewMatrix = Matrix.LookAtRH(to, lookAt, Up);
//         UpdateViewProjection();
//     }
//
//     private void UpdateViewProjection()
//     {
//         var viewProjectionMatrix = ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
//         Frustum = new BoundingFrustum(in viewProjectionMatrix);
//     }
// }

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
        camera.Position.X = mesh.Position.X + (800 * UMath.Sin(cxz));
        camera.Position.Z = mesh.Position.Z - (800 * UMath.Cos(cxz));
        camera.Position.Y = mesh.Position.Y - 250 - FollowYOffset;
        
        // Calculate the look direction by rotating the forward vector
        var lookDirection = (_angle * Vector3.UnitZ) * 100;
        // LookAt should be a target point, not a direction - add direction to position
        var lookAtPoint = position + lookDirection.ToTHREE();
        camera.LookAt(lookAtPoint);
    }
}

public static class World
{
    public static int Ground = 250;
    public static Color3 Snap;
}