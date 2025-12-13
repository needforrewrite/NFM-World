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
    
    public virtual void GameTick()
    {
    }
}

public class FixHoop(Mesh baseMesh, Vector3 position, Euler rotation) : Mesh(baseMesh, position, rotation)
{
    public bool Rotated;
    
    public override void GameTick()
    {
        if (!Rotated || Rotation.Xz != AngleSingle.ZeroAngle)
        {
            var xy = Rotation.Xy.Degrees;
            xy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (xy > 360)
            {
                xy -= 360;
            }
            Rotation = Rotation with { Xy = AngleSingle.FromDegrees(xy) };
        }
        else
        {
            var zy = Rotation.Zy.Degrees;
            zy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (zy > 360)
            {
                zy -= 360;
            }
            Rotation = Rotation with { Zy = AngleSingle.FromDegrees(zy) };
        }
    }
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
        var centroids = new List<float>();
        
        foreach (var poly in Polys)
        {
            // TODO: the result of triangulation can be cached.
            var result = PolygonTriangulator.Triangulate(Array.ConvertAll(poly.Points, input => (System.Numerics.Vector3)input));
            var centroid = Vector3.Zero;
            foreach (var point in poly.Points)
            {
                centroid += point;
            }
            centroid /= poly.Points.Length;

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
                positions.Add(p2.X);
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

                centroids.Add(centroid.X);
                centroids.Add(centroid.Y);
                centroids.Add(centroid.Z);
                centroids.Add(centroid.X);
                centroids.Add(centroid.Y);
                centroids.Add(centroid.Z);
                centroids.Add(centroid.X);
                centroids.Add(centroid.Y);
                centroids.Add(centroid.Z);
            }
        }
        
        geometry.SetAttribute("position", new BufferAttribute<float>(positions.ToArray(), 3));
        geometry.SetAttribute("normal", new BufferAttribute<float>(normals.ToArray(), 3));
        geometry.SetAttribute("tcentroid", new BufferAttribute<float>(centroids.ToArray(), 3));
        geometry.SetAttribute("color", new BufferAttribute<float>(colors.ToArray(), 3));

        geometry.ComputeBoundingSphere();

        var material = new ShaderMaterial()
        {
            Side = Constants.DoubleSide,
            VertexColors = true,
            VertexShader =
                """
                // attribute vec3 position;
                // attribute vec3 normal;
                attribute vec3 tcentroid;
                // attribute vec3 color;
                
                varying vec3 v_view_pos;
                varying vec3 v_color;
                
                uniform vec3 u_snap;
                uniform bool u_light;
                uniform bool u_color;
                uniform vec3 u_base_color;
                uniform vec3 u_light_dir;
                uniform vec3 u_fog;
                uniform float fade;
                uniform float density;
                uniform vec2 env_light;
                
                #include <common>
                #include <logdepthbuf_pars_vertex>
                #include <shadowmap_pars_vertex>
                
                void main() {
                    #include <beginnormal_vertex>
                    #include <defaultnormal_vertex>
                    #include <begin_vertex>
                    #include <skinning_vertex>
                    #include <project_vertex>
                    #include <logdepthbuf_vertex>
                    #include <worldpos_vertex>
                    #include <shadowmap_vertex>
                    
                    vec4 view_pos4 = modelViewMatrix * vec4(position, 1.0);
                    gl_Position = projectionMatrix * view_pos4;
                    
                    v_view_pos = view_pos4.xyz;
                
                    vec3 base_color = mix(color, u_base_color, vec3(u_color ? 1.0 : 0.0));
                    
                    if (!u_light) {
                        vec3 c = vec3(modelMatrix * vec4(tcentroid, 1.0));
                        vec3 n = normalize(normalMatrix * normal);
                        float diff = 0.0;
                        // TODO phys is different here!!!
                        diff = abs(dot(n, u_light_dir));
                        v_color = (env_light.x + env_light.y * diff) * base_color;
                    } else {
                        v_color = base_color;
                    }
                    
                    v_color += (v_color * u_snap);
                
                    float d = length(v_view_pos);
                    float f = pow(density, max((d - fade / 2.0) / fade, 0.0));
                    v_color = v_color * vec3(f) + u_fog * vec3(1.0 - f);
                }
                """,
            FragmentShader =
                """
                varying vec3 v_view_pos;
                varying vec3 v_color;
                
                #include <common>
                #include <packing>
                #include <fog_pars_fragment>
                #include <bsdfs>
                #include <lights_pars_begin>
                #include <logdepthbuf_pars_fragment>
                #include <shadowmap_pars_fragment>
                #include <shadowmask_pars_fragment>
                
                void main() {
                    #include <logdepthbuf_fragment>
                    
                    gl_FragColor = mix(vec4(v_color, 1.0), vec4(0.0, 0.0, 0.0, 1.0), (1.0 - getShadowMask()));
                }
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
                """,
            Uniforms =
            {
                ["u_snap"] = new GLUniform(),
                ["u_fog"] = new GLUniform(),
                ["u_light"] = new GLUniform(),
                ["u_color"] = new GLUniform(),
                ["fade"] = new GLUniform(),
                ["density"] = new GLUniform(),
                ["u_light_dir"] = new GLUniform() { ["value"] = new THREE.Vector3(0, 1, 0) },
                ["env_light"] = new GLUniform() { ["value"] = new THREE.Vector2(0.37f, 0.63f) },
            },
        };

        // var material1 = new MeshLambertMaterial()
        // {
        //     Color = THREE.Color.Hex(0xaaaaaa),
        //     Specular = THREE.Color.Hex(0xffffff),
        //     Shininess = 250,
        //     Side = Constants.DoubleSide,
        //     VertexColors = true
        // };

        var mesh = new THREE.Mesh(geometry, material);

        mesh.OnBeforeRender += (renderer, object3D, camera, geometry, material, drawRange, glRenderTarget) =>
        {
            object3D.UpdateWorldMatrix(true, false);
            
            var shaderMaterial = (material as ShaderMaterial)!;
            (shaderMaterial.Uniforms["u_snap"] as GLUniform)!["value"] = new THREE.Vector3(World.Snap[0] / 100f, World.Snap[1] / 100f, World.Snap[2] / 100f);
            (shaderMaterial.Uniforms["u_fog"] as GLUniform)!["value"] = World.Fog.Snap(World.Snap).ToTHREEVector3();
            (shaderMaterial.Uniforms["u_light"] as GLUniform)!["value"] = false;
            (shaderMaterial.Uniforms["u_color"] as GLUniform)!["value"] = false;
            (shaderMaterial.Uniforms["fade"] as GLUniform)!["value"] = World.FadeFrom;
            (shaderMaterial.Uniforms["density"] as GLUniform)!["value"] = World.Density;
            (shaderMaterial.Uniforms["env_light"] as GLUniform)!["value"] = new THREE.Vector2(World.BlackPoint, World.WhitePoint);
        
            shaderMaterial.UniformsNeedUpdate = true;
        
            // if (this.polyType === 'glass') {
            //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = true;
            //     (this.material as THREE.ShaderMaterial).uniforms.u_base_color.value = new THREE.Vector3(stage.sky[0] / 255, stage.sky[1] / 255, stage.sky[2] / 255);
            // } else if (this.polyType === 'light' && (stage.lightson || (this.parent as NFMOGeometry).lightson)) {
            //     (this.material as THREE.ShaderMaterial).uniforms.u_light.value = true;
            //     (this.material as THREE.ShaderMaterial).uniforms.u_snap.value = new THREE.Vector3(0, 0, 0);
            //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = false;
            // } else if (this.polyType === 'fullbright') {
            //     (this.material as THREE.ShaderMaterial).uniforms.u_light.value = true;
            //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = false;
            // }
        };
        mesh.ReceiveShadow = true;

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
            ThreeObject.Rotation.Order = RotationOrder.YXZ; // NFM rotation order: yaw-pitch-roll
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
    public int FollowZOffset = 0;

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
        camera.Position.Z = mesh.Position.Z - ((800 + FollowZOffset) * UMath.Cos(cxz));
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
    public static int FadeFrom;
    public static float Density;
    public static float BlackPoint = 0.37f;
    public static float WhitePoint = 0.63f;
    public static int Ground = 250;
    public static Color3 Snap;
    public static Color3 Fog;
}