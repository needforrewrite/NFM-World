using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world.compat;
using nfm_world.mesh;

namespace nfm_world;

/// <summary>
/// Holds all GraphicsPipeline objects and Samplers created at startup.
/// Pipelines bake all fixed-function state (blend, depth, rasterizer, shaders, vertex layout).
/// </summary>
public static class Pipelines
{
    // ─── Shaders ────────────────────────────────────────────────────────────

    public static Shader PolyVert { get; private set; }
    public static Shader PolyFrag { get; private set; }
    public static Shader PolyShadowVert { get; private set; }
    public static Shader PolyShadowFrag { get; private set; }
    public static Shader LineVert { get; private set; }
    public static Shader LineFrag { get; private set; }
    public static Shader SkyVert { get; private set; }
    public static Shader SkyFrag { get; private set; }
    public static Shader GroundVert { get; private set; }
    public static Shader GroundFrag { get; private set; }
    public static Shader MountainsVert { get; private set; }
    public static Shader MountainsFrag { get; private set; }
    public static Shader BasicEffectVert { get; private set; }
    public static Shader BasicEffectFrag { get; private set; }

    // ─── Pipelines ──────────────────────────────────────────────────────────

    /// <summary>Poly main pass: instanced, alpha blend, depth R/W, CullNone → swapchain format.</summary>
    public static GraphicsPipeline Poly { get; private set; }

    /// <summary>Poly shadow map: instanced, no blend, depth R/W, CullNone → R32Float target.</summary>
    public static GraphicsPipeline PolyShadow { get; private set; }

    /// <summary>Line main pass: instanced, alpha blend, depth R/W, CullNone → swapchain format.</summary>
    public static GraphicsPipeline Line { get; private set; }

    /// <summary>Sky: non-instanced VertexPositionColor, opaque, no depth → swapchain format.</summary>
    public static GraphicsPipeline Sky { get; private set; }

    /// <summary>Ground: non-instanced VertexPositionColor, opaque, depth read-only → swapchain format.</summary>
    public static GraphicsPipeline Ground { get; private set; }

    /// <summary>Mountains: non-instanced VertexPositionColor, opaque, depth read-only → swapchain format.</summary>
    public static GraphicsPipeline Mountains { get; private set; }

    /// <summary>BasicEffect: non-instanced VertexPositionColor, alpha blend, depth R/W, CullNone → swapchain format.</summary>
    public static GraphicsPipeline BasicEffect { get; private set; }

    // ─── Samplers ───────────────────────────────────────────────────────────

    /// <summary>Point-clamp sampler for shadow map sampling.</summary>
    public static Sampler ShadowSampler { get; private set; }

    // ─── Vertex Input States ────────────────────────────────────────────────

    /// <summary>Single-binding VertexPositionColor (Sky, Ground, Mountains).</summary>
    private static VertexInputState _simpleVIS;

    /// <summary>Dual-binding: VertexPositionNormalColorCentroid (slot 0) + InstanceData (slot 1).</summary>
    private static VertexInputState _polyInstancedVIS;

    /// <summary>Dual-binding: LineMeshVertexAttribute (slot 0) + InstanceData (slot 1).</summary>
    private static VertexInputState _lineInstancedVIS;

    // ─── Initialization ─────────────────────────────────────────────────────

    public static void Initialize(GraphicsDevice device, MoonWorks.Storage.TitleStorage storage, TextureFormat swapchainFormat, TextureFormat depthFormat)
    {
        LoadShaders(device, storage);
        CreateVertexInputStates();
        CreateSamplers(device);
        CreatePipelines(device, swapchainFormat, depthFormat);
    }

    private static void LoadShaders(GraphicsDevice device, MoonWorks.Storage.TitleStorage storage)
    {
        const string dir = "data/shaders";
        var fmt = ShaderCross.ShaderFormat.HLSL;

        PolyVert        = ShaderCross.Create(device, storage, $"{dir}/Poly.vert.hlsl",        "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        PolyFrag        = ShaderCross.Create(device, storage, $"{dir}/Poly.frag.hlsl",        "main", fmt, ShaderStage.Fragment, includeDir: dir);
        PolyShadowVert  = ShaderCross.Create(device, storage, $"{dir}/PolyShadow.vert.hlsl",  "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        PolyShadowFrag  = ShaderCross.Create(device, storage, $"{dir}/PolyShadow.frag.hlsl",  "main", fmt, ShaderStage.Fragment, includeDir: dir);
        LineVert        = ShaderCross.Create(device, storage, $"{dir}/Line.vert.hlsl",        "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        LineFrag        = ShaderCross.Create(device, storage, $"{dir}/Line.frag.hlsl",        "main", fmt, ShaderStage.Fragment, includeDir: dir);
        SkyVert         = ShaderCross.Create(device, storage, $"{dir}/Sky.vert.hlsl",         "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        SkyFrag         = ShaderCross.Create(device, storage, $"{dir}/Sky.frag.hlsl",         "main", fmt, ShaderStage.Fragment, includeDir: dir);
        GroundVert      = ShaderCross.Create(device, storage, $"{dir}/Ground.vert.hlsl",      "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        GroundFrag      = ShaderCross.Create(device, storage, $"{dir}/Ground.frag.hlsl",      "main", fmt, ShaderStage.Fragment, includeDir: dir);
        MountainsVert   = ShaderCross.Create(device, storage, $"{dir}/Mountains.vert.hlsl",   "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        MountainsFrag   = ShaderCross.Create(device, storage, $"{dir}/Mountains.frag.hlsl",   "main", fmt, ShaderStage.Fragment, includeDir: dir);
        BasicEffectVert = ShaderCross.Create(device, storage, $"{dir}/BasicEffect.vert.hlsl", "main", fmt, ShaderStage.Vertex,   includeDir: dir);
        BasicEffectFrag = ShaderCross.Create(device, storage, $"{dir}/BasicEffect.frag.hlsl", "main", fmt, ShaderStage.Fragment, includeDir: dir);
    }

    private static void CreateVertexInputStates()
    {
        // Simple (Sky, Ground, Mountains): single VertexPositionColor buffer
        _simpleVIS = VertexInputState.CreateSingleBinding<VertexPositionColor>();

        // Poly instanced: mesh vertices (slot 0) + instance data (slot 1)
        var polyMesh = VertexInputState.CreateSingleBinding<Mesh.VertexPositionNormalColorCentroid>(slot: 0, locationOffset: 0);
        var polyInst = VertexInputState.CreateSingleBinding<InstanceData>(slot: 1, inputRate: VertexInputRate.Instance, stepRate: 1, locationOffset: 5);
        _polyInstancedVIS = new VertexInputState
        {
            VertexBufferDescriptions = [..polyMesh.VertexBufferDescriptions, ..polyInst.VertexBufferDescriptions],
            VertexAttributes = [..polyMesh.VertexAttributes, ..polyInst.VertexAttributes]
        };

        // Line instanced: line vertices (slot 0) + instance data (slot 1)
        var lineMesh = VertexInputState.CreateSingleBinding<LineMesh.LineMeshVertexAttribute>(slot: 0, locationOffset: 0);
        var lineInst = VertexInputState.CreateSingleBinding<InstanceData>(slot: 1, inputRate: VertexInputRate.Instance, stepRate: 1, locationOffset: 7);
        _lineInstancedVIS = new VertexInputState
        {
            VertexBufferDescriptions = [..lineMesh.VertexBufferDescriptions, ..lineInst.VertexBufferDescriptions],
            VertexAttributes = [..lineMesh.VertexAttributes, ..lineInst.VertexAttributes]
        };
    }

    private static void CreateSamplers(GraphicsDevice device)
    {
        ShadowSampler = Sampler.Create(device, SamplerCreateInfo.PointClamp);
    }

    private static void CreatePipelines(GraphicsDevice device, TextureFormat swapchainFormat, TextureFormat depthFormat)
    {
        // ── Poly main pass ──────────────────────────────────────────
        Poly = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "Poly",
            VertexShader = PolyVert,
            FragmentShader = PolyFrag,
            VertexInputState = _polyInstancedVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── Poly shadow map ─────────────────────────────────────────
        PolyShadow = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "PolyShadow",
            VertexShader = PolyShadowVert,
            FragmentShader = PolyShadowFrag,
            VertexInputState = _polyInstancedVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = TextureFormat.R32Float,
                        BlendState = ColorTargetBlendState.NoBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── Line main pass ──────────────────────────────────────────
        Line = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "Line",
            VertexShader = LineVert,
            FragmentShader = LineFrag,
            VertexInputState = _lineInstancedVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── Sky ─────────────────────────────────────────────────────
        Sky = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "Sky",
            VertexShader = SkyVert,
            FragmentShader = SkyFrag,
            VertexInputState = _simpleVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = MoonWorks.Graphics.DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NoBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── Ground ──────────────────────────────────────────────────
        Ground = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "Ground",
            VertexShader = GroundVert,
            FragmentShader = GroundFrag,
            VertexInputState = _simpleVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = false,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NoBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── Mountains ───────────────────────────────────────────────
        Mountains = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "Mountains",
            VertexShader = MountainsVert,
            FragmentShader = MountainsFrag,
            VertexInputState = _simpleVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = false,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NoBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });

        // ── BasicEffect ─────────────────────────────────────────────
        BasicEffect = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            Name = "BasicEffect",
            VertexShader = BasicEffectVert,
            FragmentShader = BasicEffectFrag,
            VertexInputState = _simpleVIS,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = new MoonWorks.Graphics.RasterizerState
            {
                CullMode = MoonWorks.Graphics.CullMode.None,
                FillMode = FillMode.Fill,
                FrontFace = FrontFace.CounterClockwise
            },
            MultisampleState = MultisampleState.None,
            DepthStencilState = new MoonWorks.Graphics.DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = swapchainFormat,
                        BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
                    }
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat
            }
        });
    }
}
