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
            // CreateMeshFromLines(lines);
        }
    }

    private void CreateMeshFromLines(IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines)
    {
	    // Create a thin capsule for each line segment
	    // then displace it by the center position between the two endpoints
	    // and rotate it to align with the line segment direction
	    // then merge all the capsule meshes into a single mesh
	    // We use the normals of the original poly.
	    var capsulePoints = new List<Vector3>();
	    var capsuleIndices = new List<int>();

	    const float capsuleRadius = 10f;
	    const int radialSegments = 6;
	    const int rings = 4;

	    var data = new List<VertexPositionNormalColorCentroid>(lines.Count * ((rings + 2) * (radialSegments + 1) * 2));
	    var indices = new List<int>(lines.Count * ((rings + 1) * (radialSegments) * 6 * 2));

	    foreach (var line in lines)
	    {
		    capsulePoints.Clear();
		    capsuleIndices.Clear();
		    
		    CreateCapsuleMesh(
			    capsuleRadius,
			    (line.Key.Point1 - line.Key.Point0).Length(),
			    radialSegments,
			    rings,
			    capsulePoints,
			    capsuleIndices
		    );

		    // Transform the capsule to align with the line segment
		    var direction = Vector3.Normalize(line.Key.Point1 - line.Key.Point0);
		    var rotationMatrix = Stride.Core.Mathematics.Matrix.RotationYawPitchRoll(
			    (float)Math.Atan2(direction.X, direction.Z),
			    (float)Math.Asin(-direction.Y),
			    0f
		    );

		    var center = (line.Key.Point0 + line.Key.Point1) / 2;

		    // Add transformed points to the final mesh
		    foreach (var p in capsulePoints)
		    {
			    // Rotate and translate point
			    var transformedPoint = Vector3.TransformCoordinate(p, rotationMatrix) + center;

			    data.Add(new VertexPositionNormalColorCentroid(
				    transformedPoint.ToXna(),
				    line.Value.Normal.ToXna(),
				    line.Value.Centroid.ToXna(),
				    line.Value.Poly.Color.ToXna()
			    ));
		    }

		    var indexOffset = indices.Count;
		    foreach (var idx in capsuleIndices)
		    {
			    indices.Add(idx + indexOffset);
		    }
	    }
	    
	    _lineVertexBuffer = new VertexBuffer(_graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);
	    _lineVertexBuffer.SetData(data.ToArray());
	    
	    _lineIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
	    _lineIndexBuffer.SetData(indices.ToArray());
	    
	    _lineTriangleCount = indices.Count / 3;
    }

    // https://github.com/godotengine/godot/blob/08e6cd181f98f9ca3f58d89af0a54ce3768552d3/scene/resources/3d/primitive_meshes.cpp#L410
	private static void CreateCapsuleMesh(
		float radius,
		float height,
		int radial_segments,
		int rings,
		List<Vector3> points,
		List<int> indices,
		List<Vector3>? normals = null,
		List<float>? tangents = null,
		List<Vector2>? uvs = null,
		List<Vector2>? uv2s = null,
		float p_uv2_padding = 0
	)
	{
		int i, j, prevrow, thisrow, point;
		float x, y, z, u, v, w;
		const float onethird = 1.0f / 3.0f;
		const float twothirds = 2.0f / 3.0f;

		// Only used if we calculate UV2
		float radial_width = 2.0f * radius * MathF.PI;
		float radial_h = radial_width / (radial_width + p_uv2_padding);
		float radial_length = radius * MathF.PI * 0.5f; // circumference of 90 degree bend
		float vertical_length = radial_length * 2 + (height - 2.0f * radius) + p_uv2_padding; // total vertical length
		float radial_v = radial_length / vertical_length; // v size of top and bottom section
		float height_v = (height - 2.0f * radius) / vertical_length; // v size of height section

		// Use LocalVector for operations and copy to Vector at the end to save the cost of CoW semantics which aren't
		// needed here and are very expensive in such a hot loop. Use reserve to avoid repeated memory allocations.
		int num_points = (rings + 2) * (radial_segments + 1) * 2;
		points.EnsureCapacity(num_points);
		normals?.EnsureCapacity(num_points);
		tangents?.EnsureCapacity(num_points * 4);
		uvs?.EnsureCapacity(num_points);
		uv2s?.EnsureCapacity(num_points);
		indices.EnsureCapacity((rings + 1) * (radial_segments) * 6 * 2);
		point = 0;

		void ADD_TANGENT(float m_x, float m_y, float m_z, float m_d)
		{
			if (tangents == null) return;
			tangents.Add(m_x);
			tangents.Add(m_y);
			tangents.Add(m_z);
			tangents.Add(m_d);
		}

		// Note, this has been aligned with our collision shape but I've left the descriptions as top/middle/bottom.

		/* top hemisphere */
		thisrow = 0;
		prevrow = 0;
		for (j = 0; j <= (rings + 1); j++) {
			v = j;

			v /= (rings + 1);
			if (j == (rings + 1)) {
				w = 1.0f;
				y = 0.0f;
			} else {
				w = MathF.Sin(0.5f * MathF.PI * v);
				y = MathF.Cos(0.5f * MathF.PI * v);
			}

			for (i = 0; i <= radial_segments; i++) {
				u = i;
				u /= radial_segments;

				if (i == radial_segments) {
					x = 0.0f;
					z = 1.0f;
				} else {
					x = -MathF.Sin(u * (2*MathF.PI));
					z = MathF.Cos(u * (2*MathF.PI));
				}

				Vector3 p = new Vector3(x * w, y, -z * w);
				points.Add(p * radius + new Vector3(0.0f, 0.5f * height - radius, 0.0f));
				normals?.Add(p);
				ADD_TANGENT(-z, 0.0f, -x, 1.0f);
				uvs?.Add(new Vector2(u, v * onethird));
				uv2s?.Add(new Vector2(u * radial_h, v * radial_v));
				point++;

				if (i > 0 && j > 0) {
					indices.Add(prevrow + i - 1);
					indices.Add(prevrow + i);
					indices.Add(thisrow + i - 1);

					indices.Add(prevrow + i);
					indices.Add(thisrow + i);
					indices.Add(thisrow + i - 1);
				}
			}

			prevrow = thisrow;
			thisrow = point;
		}

		/* cylinder */
		thisrow = point;
		prevrow = 0;
		for (j = 0; j <= (rings + 1); j++) {
			v = j;
			v /= (rings + 1);

			y = (height - 2.0f * radius) * v;
			y = (0.5f * height - radius) - y;

			for (i = 0; i <= radial_segments; i++) {
				u = i;
				u /= radial_segments;

				if (i == radial_segments) {
					x = 0.0f;
					z = 1.0f;
				} else {
					x = -MathF.Sin(u * (2*MathF.PI));
					z = MathF.Cos(u * (2*MathF.PI));
				}

				Vector3 p = new Vector3(x * radius, y, -z * radius);
				points.Add(p);
				normals?.Add(new Vector3(x, 0.0f, -z));
				ADD_TANGENT(-z, 0.0f, -x, 1.0f);
				uvs?.Add(new Vector2(u, onethird + (v * onethird)));
				uv2s?.Add(new Vector2(u * radial_h, radial_v + (v * height_v)));
				point++;

				if (i > 0 && j > 0) {
					indices.Add(prevrow + i - 1);
					indices.Add(prevrow + i);
					indices.Add(thisrow + i - 1);

					indices.Add(prevrow + i);
					indices.Add(thisrow + i);
					indices.Add(thisrow + i - 1);
				}
			}

			prevrow = thisrow;
			thisrow = point;
		}

		/* bottom hemisphere */
		thisrow = point;
		prevrow = 0;
		for (j = 0; j <= (rings + 1); j++) {
			v = j;

			v /= (rings + 1);
			if (j == (rings + 1)) {
				w = 0.0f;
				y = -1.0f;
			} else {
				w = MathF.Cos(0.5f * MathF.PI * v);
				y = -MathF.Sin(0.5f * MathF.PI * v);
			}

			for (i = 0; i <= radial_segments; i++) {
				u = i;
				u /= radial_segments;

				if (i == radial_segments) {
					x = 0.0f;
					z = 1.0f;
				} else {
					x = -MathF.Sin(u * (2*MathF.PI));
					z = MathF.Cos(u * (2*MathF.PI));
				}

				Vector3 p = new Vector3(x * w, y, -z * w);
				points.Add(p * radius + new Vector3(0.0f, -0.5f * height + radius, 0.0f));
				normals?.Add(p);
				ADD_TANGENT(-z, 0.0f, -x, 1.0f);
				uvs?.Add(new Vector2(u, twothirds + v * onethird));
				uv2s?.Add(new Vector2(u * radial_h, radial_v + height_v + v * radial_v));
				point++;

				if (i > 0 && j > 0) {
					indices.Add(prevrow + i - 1);
					indices.Add(prevrow + i);
					indices.Add(thisrow + i - 1);

					indices.Add(prevrow + i);
					indices.Add(thisrow + i);
					indices.Add(thisrow + i - 1);
				}
			}

			prevrow = thisrow;
			thisrow = point;
		}
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
        _material.DepthBias?.SetValue(0.00002f);

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