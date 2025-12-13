using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using HoleyDiver;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Stride.Core.Mathematics.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Stride.Core.Mathematics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace NFMWorld.Mad;

public class Mesh : Transform
{
    public Color3[] Colors;
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;
    public Rad3dBoxDef[] Boxes;
    public Rad3dPoly[] Polys;
    
    // visually wasted
    public bool Wasted;

    public int GroundAt;
    private readonly GraphicsDevice _graphicsDevice;

    private VertexBuffer _vertexBuffer;
    private PolyEffect _material;
    private int _triangleCount;

    private LineEffect _lineMaterial;    
    private VertexBuffer? _lineVertexBuffer;
    private IndexBuffer? _lineIndexBuffer;
    private int? _lineTriangleCount;

    public Euler TurningWheelAngle { get; set; }

    public Mesh(GraphicsDevice graphicsDevice, string code)
    {
        var rad = RadParser.ParseRad(code);
        Colors = rad.Colors;
        Stats = rad.Stats;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = rad.Polys;
        _material = new PolyEffect(Program._polyShader);
        _lineMaterial = new LineEffect(Program._lineShader);

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        _graphicsDevice = graphicsDevice;
        BuildMesh(graphicsDevice);
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
        _graphicsDevice = baseMesh._graphicsDevice;
        _material = baseMesh._material;
        _lineMaterial = baseMesh._lineMaterial;

        BuildMesh(_graphicsDevice);
        Position = position;
        Rotation = rotation;
    }

    [MemberNotNull(nameof(_vertexBuffer))]
    private void BuildMesh(GraphicsDevice graphicsDevice)
    {
        var data = new List<VertexPositionNormalColorCentroid>();
        
        var lines = new OrderedDictionary<(Vector3 point0, Vector3 point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>(LineEqualityComparer.Instance);
        
        _triangleCount = 0;
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
                
                data.Add(new VertexPositionNormalColorCentroid(p0.ToXna(), result.PlaneNormal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
                data.Add(new VertexPositionNormalColorCentroid(p1.ToXna(), result.PlaneNormal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
                data.Add(new VertexPositionNormalColorCentroid(p2.ToXna(), result.PlaneNormal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            }

            _triangleCount += result.Triangles.Count;
            if (poly.LineType != null)
            {
                for (var i = 0; i < poly.Points.Length; ++i)
                {
                    var p0 = poly.Points[i];
                    var p1 = poly.Points[(i + 1) % poly.Points.Length];
                    lines.TryAdd((p0, p1), (poly, centroid, result.PlaneNormal));
                }
            }
        }
        _triangleCount /= 3;

        var vertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);

        vertexBuffer.SetData(data.ToArray());

        _vertexBuffer = vertexBuffer;

        if (lines.Count > 0)
        {
            CreateMeshFromLines(lines);
        }
    }

    private void CreateMeshFromLines(IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines)
    {
	    var data = new List<VertexPositionNormalColorCentroid>(8 * lines.Count);
	    var indices = new List<int>(12 * 3 * lines.Count);

        const float halfThickness = 2f;
	    foreach (var line in lines)
	    {
            // Create two quads for each line segment to give it some thickness
            var p0 = line.Key.Point0;
            var p1 = line.Key.Point1;
            var poly = line.Value.Poly;
            var centroid = line.Value.Centroid;
            var normal = line.Value.Normal;
            var lineDir = Vector3.Normalize(p1 - p0);
            var up = Vector3.UnitY;
            if (Vector3.Dot(lineDir, up) > 0.99f)
            {
                up = Vector3.UnitX; // Avoid degenerate case
            }

            var right = Vector3.Normalize(Vector3.Cross(lineDir, up)) * halfThickness; // Line half-thickness
            up = Vector3.Normalize(Vector3.Cross(right, lineDir)) * halfThickness; // Recalculate up to ensure orthogonality
            var v0 = p0 - right - up;
            var v1 = p0 + right - up;
            var v2 = p0 + right + up;
            var v3 = p0 - right + up;
            var v4 = p1 - right - up;
            var v5 = p1 + right - up;
            var v6 = p1 + right + up;
            var v7 = p1 - right + up;
            var baseIndex = data.Count;
            data.Add(new VertexPositionNormalColorCentroid(v0.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v1.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v2.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v3.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v4.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v5.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v6.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            data.Add(new VertexPositionNormalColorCentroid(v7.ToXna(), normal.ToXna(), centroid.ToXna(), poly.Color.ToXna()));
            // Two triangles per quad face
            indices.AddRange(
                baseIndex, baseIndex + 1, baseIndex + 2,
                baseIndex, baseIndex + 2, baseIndex + 3,
                baseIndex + 4, baseIndex + 6, baseIndex + 5,
                baseIndex + 4, baseIndex + 7, baseIndex + 6,
                baseIndex + 3, baseIndex + 2, baseIndex + 6,
                baseIndex + 3, baseIndex + 6, baseIndex + 7,
                baseIndex, baseIndex + 5, baseIndex + 1,
                baseIndex, baseIndex + 4, baseIndex + 5,
                baseIndex + 1, baseIndex + 5, baseIndex + 6,
                baseIndex + 1, baseIndex + 6, baseIndex + 2,
                baseIndex + 4, baseIndex + 0, baseIndex + 3,
                baseIndex + 4, baseIndex + 3, baseIndex + 7
            );
        }
	    
	    _lineVertexBuffer = new VertexBuffer(_graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);
	    _lineVertexBuffer.SetData(data.ToArray());
	    
	    _lineIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
	    _lineIndexBuffer.SetData(indices.ToArray());
	    
	    _lineTriangleCount = indices.Count / 3;
    }

    /// <summary>
    /// Equality comparer that considers two lines equal if they have the same endpoints, regardless of order.
    /// </summary>
    private struct LineEqualityComparer : IEqualityComparer<(Vector3 Point0, Vector3 Point1)>
    {
        public static LineEqualityComparer Instance { get; } = new();

        public bool Equals((Vector3 Point0, Vector3 Point1) x, (Vector3 Point0, Vector3 Point1) y)
        {
            return (x.Point0 == y.Point0 && x.Point1 == y.Point1) ||
                   (x.Point0 == y.Point1 && x.Point1 == y.Point0);
        }

        public int GetHashCode((Vector3 Point0, Vector3 Point1) obj)
        {
            return obj.Point0.GetHashCode() ^ obj.Point1.GetHashCode();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct VertexPositionNormalColorCentroid(
        Microsoft.Xna.Framework.Vector3 Position,
        Microsoft.Xna.Framework.Vector3 Normal,
        Microsoft.Xna.Framework.Vector3 Centroid,
        Microsoft.Xna.Framework.Color Color)
    {
        /// <inheritdoc cref="P:Microsoft.Xna.Framework.Graphics.IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = new(
	        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
	        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
	        new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
	        new VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0)
	    );
    }

    public virtual void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        var matrixWorld = Matrix.CreateFromEuler(Rotation) * Matrix.CreateTranslation(Position.ToXna());

        RenderPolygons(camera, lightCamera, isCreateShadowMap, matrixWorld);
        RenderLines(camera, lightCamera, isCreateShadowMap, matrixWorld);
    }

    private void RenderPolygons(Camera camera, Camera? lightCamera, bool isCreateShadowMap, Matrix matrixWorld)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 0, 0));
        _material.LightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 1, 0));
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(0.857f);
        _material.EnvironmentLight?.SetValue(new Microsoft.Xna.Framework.Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);

        if (isCreateShadowMap)
        {
            _material.View?.SetValue(lightCamera!.ViewMatrix);
            _material.Projection?.SetValue(lightCamera!.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * lightCamera!.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * lightCamera!.ViewMatrix * lightCamera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(lightCamera!.Position.ToXna());
        }
        else
        {
            _material.View?.SetValue(camera.ViewMatrix);
            _material.Projection?.SetValue(camera.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(camera.Position.ToXna());
        }

        if (lightCamera != null)
        {
            _material.LightViewProj?.SetValue(lightCamera.ViewProjectionMatrix);
        }

        _material.CurrentTechnique = isCreateShadowMap ? _material.Techniques["CreateShadowMap"] : _material.Techniques["Basic"];
        if (!isCreateShadowMap)
        {
            _material.ShadowMap?.SetValue(Program.shadowRenderTarget);
        }
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }
        
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
    }

    private void RenderLines(Camera camera, Camera? lightCamera, bool isCreateShadowMap, Matrix matrixWorld)
    {
        if (isCreateShadowMap) return; // no lines in shadow map
        if (_lineVertexBuffer == null) return; // no lines to render

        _graphicsDevice.SetVertexBuffer(_lineVertexBuffer);
        _graphicsDevice.Indices = _lineIndexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(true);
        _material.UseBaseColor?.SetValue(true);
        _material.BaseColor?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 0, 0));
        _material.LightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 1, 0));
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(0.857f);
        _material.EnvironmentLight?.SetValue(new Microsoft.Xna.Framework.Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00002f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
        _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position.ToXna());

        _material.CurrentTechnique = _material.Techniques["Basic"];
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lineTriangleCount!.Value);
        }
    }

    // private Object3D BuildMesh()
    // {
    //     var geometry = new BufferGeometry();
    //     
    //     var positions = new List<float>();
    //     var normals = new List<float>();
    //     var colors = new List<float>();
    //     var centroids = new List<float>();
    //     
    //     foreach (var poly in Polys)
    //     {
    //         // TODO: the result of triangulation can be cached.
    //         var result = PolygonTriangulator.Triangulate(Array.ConvertAll(poly.Points, input => (System.Numerics.Vector3)input));
    //         var centroid = Vector3.Zero;
    //         foreach (var point in poly.Points)
    //         {
    //             centroid += point;
    //         }
    //         centroid /= poly.Points.Length;
    //
    //         for (var index = 0; index < result.Triangles.Count; index += 3)
    //         {
    //             var i0 = result.Triangles[index];
    //             var i1 = result.Triangles[index + 1];
    //             var i2 = result.Triangles[index + 2];
    //             
    //             var p0 = poly.Points[i0];
    //             var p1 = poly.Points[i1];
    //             var p2 = poly.Points[i2];
    //             
    //             positions.Add(p0.X);
    //             positions.Add(p0.Y);
    //             positions.Add(p0.Z);
    //             positions.Add(p1.X);
    //             positions.Add(p1.Y);
    //             positions.Add(p1.Z);
    //             positions.Add(p2.X);
    //             positions.Add(p2.Y);
    //             positions.Add(p2.Z);
    //
    //             normals.Add(result.PlaneNormal.X);
    //             normals.Add(result.PlaneNormal.Y);
    //             normals.Add(result.PlaneNormal.Z);
    //             normals.Add(result.PlaneNormal.X);
    //             normals.Add(result.PlaneNormal.Y);
    //             normals.Add(result.PlaneNormal.Z);
    //             normals.Add(result.PlaneNormal.X);
    //             normals.Add(result.PlaneNormal.Y);
    //             normals.Add(result.PlaneNormal.Z);
    //
    //             colors.Add(poly.Color.R / 255f);
    //             colors.Add(poly.Color.G / 255f);
    //             colors.Add(poly.Color.B / 255f);
    //             colors.Add(poly.Color.R / 255f);
    //             colors.Add(poly.Color.G / 255f);
    //             colors.Add(poly.Color.B / 255f);
    //             colors.Add(poly.Color.R / 255f);
    //             colors.Add(poly.Color.G / 255f);
    //             colors.Add(poly.Color.B / 255f);
    //
    //             centroids.Add(centroid.X);
    //             centroids.Add(centroid.Y);
    //             centroids.Add(centroid.Z);
    //             centroids.Add(centroid.X);
    //             centroids.Add(centroid.Y);
    //             centroids.Add(centroid.Z);
    //             centroids.Add(centroid.X);
    //             centroids.Add(centroid.Y);
    //             centroids.Add(centroid.Z);
    //         }
    //     }
    //     
    //     geometry.SetAttribute("position", new BufferAttribute<float>(positions.ToArray(), 3));
    //     geometry.SetAttribute("normal", new BufferAttribute<float>(normals.ToArray(), 3));
    //     geometry.SetAttribute("tcentroid", new BufferAttribute<float>(centroids.ToArray(), 3));
    //     geometry.SetAttribute("color", new BufferAttribute<float>(colors.ToArray(), 3));
    //
    //     geometry.ComputeBoundingSphere();
    //
    //     var material = new ShaderMaterial()
    //     {
    //         Side = Constants.DoubleSide,
    //         VertexColors = true,
    //         VertexShader =
    //             """
    //             // attribute vec3 position;
    //             // attribute vec3 normal;
    //             attribute vec3 tcentroid;
    //             // attribute vec3 color;
    //             
    //             varying vec3 v_view_pos;
    //             varying vec3 v_color;
    //             
    //             uniform vec3 u_snap;
    //             uniform bool u_light;
    //             uniform bool u_color;
    //             uniform vec3 u_base_color;
    //             uniform vec3 u_light_dir;
    //             uniform vec3 u_fog;
    //             uniform float fade;
    //             uniform float density;
    //             uniform vec2 env_light;
    //             
    //             #include <common>
    //             #include <logdepthbuf_pars_vertex>
    //             #include <shadowmap_pars_vertex>
    //             
    //             void main() {
    //                 #include <beginnormal_vertex>
    //                 #include <defaultnormal_vertex>
    //                 #include <begin_vertex>
    //                 #include <skinning_vertex>
    //                 #include <project_vertex>
    //                 #include <logdepthbuf_vertex>
    //                 #include <worldpos_vertex>
    //                 #include <shadowmap_vertex>
    //                 
    //                 vec4 view_pos4 = modelViewMatrix * vec4(position, 1.0);
    //                 gl_Position = projectionMatrix * view_pos4;
    //                 
    //                 v_view_pos = view_pos4.xyz;
    //             
    //                 vec3 base_color = mix(color, u_base_color, vec3(u_color ? 1.0 : 0.0));
    //                 
    //                 if (!u_light) {
    //                     vec3 c = vec3(modelMatrix * vec4(tcentroid, 1.0));
    //                     vec3 n = normalize(normalMatrix * normal);
    //                     float diff = 0.0;
    //                     // TODO phys is different here!!!
    //                     diff = abs(dot(n, u_light_dir));
    //                     v_color = (env_light.x + env_light.y * diff) * base_color;
    //                 } else {
    //                     v_color = base_color;
    //                 }
    //                 
    //                 v_color += (v_color * u_snap);
    //             
    //                 float d = length(v_view_pos);
    //                 float f = pow(density, max((d - fade / 2.0) / fade, 0.0));
    //                 v_color = v_color * vec3(f) + u_fog * vec3(1.0 - f);
    //             }
    //             """,
    //         FragmentShader =
    //             """
    //             varying vec3 v_view_pos;
    //             varying vec3 v_color;
    //             
    //             #include <common>
    //             #include <packing>
    //             #include <fog_pars_fragment>
    //             #include <bsdfs>
    //             #include <lights_pars_begin>
    //             #include <logdepthbuf_pars_fragment>
    //             #include <shadowmap_pars_fragment>
    //             #include <shadowmask_pars_fragment>
    //             
    //             void main() {
    //                 #include <logdepthbuf_fragment>
    //                 
    //                 gl_FragColor = mix(vec4(v_color, 1.0), vec4(0.0, 0.0, 0.0, 1.0), (1.0 - getShadowMask()));
    //             }
    //             """,
    //         Uniforms =
    //         {
    //             ["u_snap"] = new GLUniform(),
    //             ["u_fog"] = new GLUniform(),
    //             ["u_light"] = new GLUniform(),
    //             ["u_color"] = new GLUniform(),
    //             ["fade"] = new GLUniform(),
    //             ["density"] = new GLUniform(),
    //             ["u_light_dir"] = new GLUniform() { ["value"] = new THREE.Vector3(0, 1, 0) },
    //             ["env_light"] = new GLUniform() { ["value"] = new THREE.Vector2(0.37f, 0.63f) },
    //         },
    //     };
    //
    //     // var material1 = new MeshLambertMaterial()
    //     // {
    //     //     Color = THREE.Color.Hex(0xaaaaaa),
    //     //     Specular = THREE.Color.Hex(0xffffff),
    //     //     Shininess = 250,
    //     //     Side = Constants.DoubleSide,
    //     //     VertexColors = true
    //     // };
    //
    //     var mesh = new THREE.Mesh(geometry, material);
    //
    //     mesh.OnBeforeRender += (renderer, object3D, camera, geometry, material, drawRange, glRenderTarget) =>
    //     {
    //         object3D.UpdateWorldMatrix(true, false);
    //         
    //         var shaderMaterial = (material as ShaderMaterial)!;
    //         (shaderMaterial.Uniforms["u_snap"] as GLUniform)!["value"] = new THREE.Vector3(World.Snap[0] / 100f, World.Snap[1] / 100f, World.Snap[2] / 100f);
    //         (shaderMaterial.Uniforms["u_fog"] as GLUniform)!["value"] = World.Fog.Snap(World.Snap).ToTHREEVector3();
    //         (shaderMaterial.Uniforms["u_light"] as GLUniform)!["value"] = false;
    //         (shaderMaterial.Uniforms["u_color"] as GLUniform)!["value"] = false;
    //         (shaderMaterial.Uniforms["fade"] as GLUniform)!["value"] = World.FadeFrom;
    //         (shaderMaterial.Uniforms["density"] as GLUniform)!["value"] = World.Density;
    //         (shaderMaterial.Uniforms["env_light"] as GLUniform)!["value"] = new THREE.Vector2(World.BlackPoint, World.WhitePoint);
    //     
    //         shaderMaterial.UniformsNeedUpdate = true;
    //     
    //         // if (this.polyType === 'glass') {
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = true;
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_base_color.value = new THREE.Vector3(stage.sky[0] / 255, stage.sky[1] / 255, stage.sky[2] / 255);
    //         // } else if (this.polyType === 'light' && (stage.lightson || (this.parent as NFMOGeometry).lightson)) {
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_light.value = true;
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_snap.value = new THREE.Vector3(0, 0, 0);
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = false;
    //         // } else if (this.polyType === 'fullbright') {
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_light.value = true;
    //         //     (this.material as THREE.ShaderMaterial).uniforms.u_color.value = false;
    //         // }
    //     };
    //     mesh.ReceiveShadow = true;
    //
    //     var containerObject = new Object3D();
    //     containerObject.Add(mesh);
    //
    //     return containerObject;
    // }

    public sealed override Vector3 Position { get; set; }

    public sealed override Euler Rotation { get; set; }
}