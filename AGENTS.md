# FNA → MoonWorks Porting Guide

This document describes the architectural differences between **FNA** (XNA4 reimplementation) and **MoonWorks** (modern SDL3-GPU framework), and how to port code from FNA to MoonWorks.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Game Lifecycle](#game-lifecycle)
3. [Graphics: Fundamental Model Change](#graphics-fundamental-model-change)
4. [Resource Creation](#resource-creation)
5. [Rendering Pipeline](#rendering-pipeline)
6. [Shaders & Effects](#shaders--effects)
7. [Vertex Definitions](#vertex-definitions)
8. [Render State](#render-state)
9. [Textures & Render Targets](#textures--render-targets)
10. [SpriteBatch Replacement](#spritebatch-replacement)
11. [Input System](#input-system)
12. [Audio System](#audio-system)
13. [Content / Asset Loading](#content--asset-loading)
14. [Math Types](#math-types)
15. [Window & Display](#window--display)
16. [Common Porting Patterns](#common-porting-patterns)
17. [Checklist](#checklist)

---

## Architecture Overview

| Aspect | FNA | MoonWorks |
|--------|-----|-----------|
| **API heritage** | XNA 4.0 (DirectX 9 era) | SDL3-GPU (Vulkan/D3D12/Metal) |
| **Rendering model** | Immediate-mode state machine | Deferred command buffer recording |
| **Namespace root** | `Microsoft.Xna.Framework` | `MoonWorks` |
| **GPU backend** | FNA3D (OpenGL/D3D11) | SDL3-GPU (Vulkan, D3D12, Metal) |
| **Shader format** | HLSL → `.fxb` (Effect framework) | SPIR-V, DXBC, DXIL, MSL (per-backend) |
| **Target framework** | `net10.0` | `net9.0` |
| **State management** | Global device state | Per-pass binding |
| **Resource upload** | Implicit (constructor + SetData) | Explicit (Create → TransferBuffer → CopyPass) |
| **Synchronization** | Implicit | Explicit (fences, frames-in-flight) |
| **Content pipeline** | XNB + ContentManager | TitleStorage (raw file reads) |
| **Audio backend** | FAudio (XAudio2 compat) | FAudio (same backend, different API surface) |

### Key Mindset Shift

FNA uses an **immediate-mode state machine**: you set global state on `GraphicsDevice`, then issue draw calls that execute immediately. MoonWorks uses a **command buffer model**: you acquire a command buffer, begin typed passes (render/compute/copy), bind state within those passes, record draw commands, then submit the buffer for asynchronous GPU execution.

---

## Game Lifecycle

### FNA

```csharp
public class MyGame : Microsoft.Xna.Framework.Game
{
    GraphicsDeviceManager graphics;

    public MyGame()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }

    protected override void Initialize() { base.Initialize(); }
    protected override void LoadContent() { /* load assets */ }
    protected override void Update(GameTime gameTime) { /* logic */ }
    protected override void Draw(GameTime gameTime) { /* render */ }
}
```

- `Run()` enters the main loop, calls `Tick()` each frame
- Fixed timestep by default (`IsFixedTimeStep = true`, 60 FPS)
- `Update(GameTime)` receives elapsed and total time
- `Draw(GameTime)` renders; `EndDraw()` calls `GraphicsDevice.Present()`

### MoonWorks

```csharp
public class MyGame : MoonWorks.Game
{
    public MyGame(AppInfo appInfo, WindowCreateInfo windowInfo,
                  FramePacingSettings framePacing, int targetTimestep,
                  bool debugMode)
        : base(appInfo, windowInfo, framePacing, targetTimestep, debugMode) { }

    protected override void Step() { /* once per accumulation iteration */ }
    protected override void Update(TimeSpan delta) { /* fixed-timestep logic */ }
    protected override void Draw(double alpha) { /* render with interpolation */ }
    protected override void Destroy() { /* cleanup */ }
}
```

### Lifecycle Mapping

| FNA | MoonWorks | Notes |
|-----|-----------|-------|
| `Game()` constructor | `Game()` constructor | MoonWorks creates `GraphicsDevice`, `AudioDevice`, `Window` in base ctor |
| `Initialize()` | Constructor body | No separate init phase; device is ready after base ctor |
| `LoadContent()` | Constructor body | Load via `TitleStorage` instead of `ContentManager` |
| `Update(GameTime gt)` | `Update(TimeSpan delta)` | MoonWorks passes delta directly, not `GameTime` |
| `Draw(GameTime gt)` | `Draw(double alpha)` | `alpha` is interpolation factor (0–1) for frame smoothing |
| — | `Step()` | Called once per accumulation loop iteration (before Update) |
| `UnloadContent()` | `Destroy()` | Cleanup override |
| `IsFixedTimeStep` | `FramePacingSettings` | `LatencyOptimized`, `Capped`, `Uncapped` modes |
| `TargetElapsedTime` | `FramePacingSettings.Timestep` | Controls Update frequency |
| `GraphicsDeviceManager` | Built into `Game` | No separate GDM; device managed internally |

### Frame Pacing Modes

FNA has a simple `IsFixedTimeStep` toggle. MoonWorks offers three modes:

- **`LatencyOptimized`** — Waits on swapchain before event processing for minimal input latency. `alpha` is always 0.
- **`Capped`** — Fixed timestep with better GPU utilization. `alpha` provides interpolation value.
- **`Uncapped`** — Maximum framerate, no frame limiting.

---

## Graphics: Fundamental Model Change

This is the **most critical difference**. Every rendering pattern changes.

### FNA: Immediate-Mode State Machine

```csharp
// Set global state on GraphicsDevice
GraphicsDevice.BlendState = BlendState.AlphaBlend;
GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
GraphicsDevice.DepthStencilState = DepthStencilState.Default;
GraphicsDevice.SetRenderTarget(myTarget);
GraphicsDevice.SetVertexBuffer(vertexBuffer);
GraphicsDevice.Indices = indexBuffer;
GraphicsDevice.Textures[0] = myTexture;
GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

// Apply effect, draw immediately
foreach (EffectPass pass in effect.CurrentTechnique.Passes)
{
    pass.Apply();
    GraphicsDevice.DrawIndexedPrimitives(
        PrimitiveType.TriangleList, 0, 0, vertexCount, 0, primitiveCount);
}

// Present
GraphicsDevice.Present();
```

### MoonWorks: Command Buffer Model

```csharp
// Acquire command buffer (from pool)
CommandBuffer cmd = GraphicsDevice.AcquireCommandBuffer();

// Push uniform data
cmd.PushVertexUniformData(mvpMatrix);
cmd.PushFragmentUniformData(materialParams);

// Acquire swapchain texture
Texture backbuffer = cmd.AcquireSwapchainTexture(MainWindow);
if (backbuffer == null) return; // Swapchain unavailable

// Begin a render pass (state is scoped to this pass)
var colorTarget = new ColorTargetInfo
{
    TextureSlice = backbuffer,
    LoadOp = LoadOp.Clear,
    StoreOp = StoreOp.Store,
    ClearColor = Color.Black
};

using (RenderPass renderPass = cmd.BeginRenderPass(colorTarget))
{
    renderPass.BindGraphicsPipeline(myPipeline);   // Pipeline = shader + all fixed state
    renderPass.SetViewport(new Viewport(0, 0, 1920, 1080));
    renderPass.BindVertexBuffers(0, new BufferBinding(vertexBuffer, 0));
    renderPass.BindIndexBuffer(new BufferBinding(indexBuffer, 0), IndexElementSize.ThirtyTwoBits);
    renderPass.BindFragmentSamplers(0, new TextureSamplerBinding(myTexture, mySampler));
    renderPass.DrawIndexedPrimitives(indexCount, 1, 0, 0, 0);
}

// Submit for async execution
GraphicsDevice.Submit(cmd);
```

### Key Differences Summary

1. **No global state machine** — All state is bound within a render pass scope.
2. **Pipeline objects** — Blend, depth-stencil, rasterizer, shaders are baked into a `GraphicsPipeline` at creation time, not set individually.
3. **Explicit passes** — `RenderPass`, `ComputePass`, `CopyPass` are separate contexts.
4. **Command buffers** — Commands are recorded, then submitted as a batch.
5. **Swapchain acquisition** — Must explicitly acquire the backbuffer texture each frame.
6. **No `Present()`** — Submission of the command buffer triggers presentation.

---

## Resource Creation

### FNA: Constructor-Based

```csharp
// Texture
var tex = new Texture2D(GraphicsDevice, 256, 256);
tex.SetData(pixelData);

// Vertex buffer
var vb = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), 100, BufferUsage.WriteOnly);
vb.SetData(vertices);

// Index buffer
var ib = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, 300, BufferUsage.WriteOnly);
ib.SetData(indices);

// Render target
var rt = new RenderTarget2D(GraphicsDevice, 1920, 1080, false,
    SurfaceFormat.Color, DepthFormat.Depth24);
```

### MoonWorks: Factory + Explicit Upload

```csharp
// Texture — create empty, then upload via CopyPass
Texture tex = Texture.Create2D(GraphicsDevice, 256, 256,
    TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);

// Buffer — create empty
Buffer vertexBuffer = Buffer.Create<MyVertex>(GraphicsDevice,
    BufferUsageFlags.Vertex, elementCount: 100);

Buffer indexBuffer = Buffer.Create<uint>(GraphicsDevice,
    BufferUsageFlags.Index, elementCount: 300);

// Upload data via TransferBuffer + CopyPass
var transferBuffer = TransferBuffer.Create<MyVertex>(GraphicsDevice,
    TransferBufferUsage.Upload, 100);
var mapped = transferBuffer.Map<MyVertex>(cycle: false);
vertices.CopyTo(mapped);
transferBuffer.Unmap();

CommandBuffer cmd = GraphicsDevice.AcquireCommandBuffer();
using (CopyPass copyPass = cmd.BeginCopyPass())
{
    copyPass.UploadToBuffer(
        new TransferBufferLocation(transferBuffer),
        new BufferRegion(vertexBuffer, 0, (uint)(100 * sizeof(MyVertex))),
        cycle: false);
}
GraphicsDevice.Submit(cmd);
```

### Convenience: ResourceUploader

```csharp
var uploader = new ResourceUploader(GraphicsDevice);
Texture tex = uploader.CreateTexture2D(pixelSpan,
    TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 256, 256);
uploader.Upload();         // Submits a CopyPass internally
uploader.Dispose();
```

### Resource Creation Mapping

| FNA | MoonWorks |
|-----|-----------|
| `new Texture2D(device, w, h)` | `Texture.Create2D(device, w, h, format, usage)` |
| `new RenderTarget2D(device, w, h, ...)` | `Texture.Create2D(device, w, h, format, TextureUsageFlags.ColorTarget \| ...)` |
| `new VertexBuffer(device, decl, count, usage)` | `Buffer.Create<T>(device, BufferUsageFlags.Vertex, count)` |
| `new IndexBuffer(device, elemSize, count, usage)` | `Buffer.Create<T>(device, BufferUsageFlags.Index, count)` |
| `texture.SetData(data)` | `TransferBuffer` → map → copy → `CopyPass.UploadToTexture()` |
| `buffer.SetData(data)` | `TransferBuffer` → map → copy → `CopyPass.UploadToBuffer()` |
| `texture.GetData(data)` | `CopyPass.DownloadFromTexture()` → fence wait → `TransferBuffer.Map()` |
| `new SamplerState { ... }` | `Sampler.Create(device, SamplerCreateInfo { ... })` |
| Implicit disposal via GC | Explicit `resource.Dispose()` or via `using` |

### Important: The `cycle` Parameter

Many MoonWorks upload/bind operations take a `bool cycle` parameter. When `true`, the GPU allocates a new backing store for the resource, allowing the previous contents to remain valid for in-flight commands. Use `cycle: true` when updating a resource that may still be referenced by a previously submitted (but not yet completed) command buffer. Use `cycle: false` for initial uploads or when you know no in-flight commands reference the resource.

---

## Rendering Pipeline

### FNA: Per-Draw State + Effect Passes

```csharp
effect.Parameters["WorldViewProjection"].SetValue(wvpMatrix);
effect.Parameters["DiffuseTexture"].SetValue(texture);

foreach (EffectPass pass in effect.CurrentTechnique.Passes)
{
    pass.Apply();  // Pushes all state to GPU
    GraphicsDevice.DrawIndexedPrimitives(...);
}
```

### MoonWorks: Pipeline Object + Render Pass

In MoonWorks, **all fixed-function state is baked into a `GraphicsPipeline` object** at creation time. You don't set blend/depth/rasterizer state per-draw; you create pipeline variants upfront.

```csharp
// Create pipeline ONCE (typically at load time)
GraphicsPipeline pipeline = GraphicsPipeline.Create(GraphicsDevice,
    new GraphicsPipelineCreateInfo
    {
        VertexShader = vertShader,
        FragmentShader = fragShader,
        VertexInputState = VertexInputState.CreateSingleBinding<MyVertex>(),
        PrimitiveType = PrimitiveType.TriangleList,
        RasterizerState = RasterizerState.CCW_CullBack,
        MultisampleState = MultisampleState.None,
        DepthStencilState = DepthStencilState.Disable,
        TargetInfo = new GraphicsPipelineTargetInfo
        {
            ColorTargetDescriptions = new[]
            {
                new ColorTargetDescription
                {
                    Format = TextureFormat.R8G8B8A8Unorm,
                    BlendState = ColorTargetBlendState.PremultipliedAlphaBlend
                }
            }
        }
    });

// Use pipeline in render pass
using (RenderPass pass = cmd.BeginRenderPass(colorTarget))
{
    pass.BindGraphicsPipeline(pipeline);
    pass.SetViewport(viewport);
    pass.BindVertexBuffers(0, vertexBinding);
    pass.BindFragmentSamplers(0, textureSamplerBinding);

    cmd.PushVertexUniformData(mvpMatrix);
    pass.DrawPrimitives(vertexCount, 1, 0, 0);
}
```

### Pipeline State Mapping

| FNA state object | MoonWorks pipeline field | Notes |
|-----------------|------------------------|-------|
| `BlendState` | `ColorTargetBlendState` (in `TargetInfo`) | Per-color-target, not global |
| `DepthStencilState` | `DepthStencilState` | Struct, not class |
| `RasterizerState` | `RasterizerState` | Struct with different presets |
| `Effect` + `EffectPass.Apply()` | `VertexShader` + `FragmentShader` | Separate shader objects |
| `PrimitiveType` | `PrimitiveType` | Same concept |
| Vertex declaration | `VertexInputState` | Explicit location-based binding |

### Multiple State Combinations

In FNA, you swap state objects freely between draws. In MoonWorks, you must create separate `GraphicsPipeline` objects for each unique combination of fixed-function state + shaders, then bind the appropriate pipeline before drawing.

```csharp
// FNA: swap state freely
GraphicsDevice.BlendState = BlendState.Opaque;
DrawOpaqueGeometry();
GraphicsDevice.BlendState = BlendState.AlphaBlend;
DrawTransparentGeometry();

// MoonWorks: separate pipelines
pass.BindGraphicsPipeline(opaquePipeline);
DrawOpaqueGeometry(pass);
pass.BindGraphicsPipeline(transparentPipeline);
DrawTransparentGeometry(pass);
```

---

## Shaders & Effects

### FNA: Effect Framework

FNA uses the XNA Effect framework with HLSL shaders compiled to `.fxb` bytecode via `fxc.exe`.

```csharp
Effect effect = Content.Load<Effect>("MyShader");
effect.Parameters["WorldViewProjection"].SetValue(matrix);
effect.Parameters["DiffuseTexture"].SetValue(texture);
effect.CurrentTechnique = effect.Techniques["MainTechnique"];

foreach (EffectPass pass in effect.CurrentTechnique.Passes)
{
    pass.Apply();
    GraphicsDevice.DrawIndexedPrimitives(...);
}
```

- Effects contain **techniques** with **passes**
- Parameters are set by name with reflection: `effect.Parameters["Name"]`
- Single HLSL source compiles to vertex + pixel shader + state

### MoonWorks: Separate Shader Objects

MoonWorks uses standalone vertex and fragment shaders. No effect framework, no parameter-by-name reflection.

```csharp
// Load compiled shader (SPIR-V, DXBC, DXIL, or MSL depending on backend)
Shader vertShader = Shader.Create(GraphicsDevice, TitleStorage, "shaders/vert.spv", "main",
    new ShaderCreateInfo
    {
        ShaderStage = ShaderStage.Vertex,
        ShaderFormat = ShaderFormat.SPIRV,
        NumUniformBuffers = 1,    // Must declare resource counts
        NumSamplers = 0,
        NumStorageBuffers = 0,
        NumStorageTextures = 0
    });

Shader fragShader = Shader.Create(GraphicsDevice, TitleStorage, "shaders/frag.spv", "main",
    new ShaderCreateInfo
    {
        ShaderStage = ShaderStage.Fragment,
        ShaderFormat = ShaderFormat.SPIRV,
        NumUniformBuffers = 1,
        NumSamplers = 1
    });
```

### Passing Data to Shaders

| FNA | MoonWorks | Notes |
|-----|-----------|-------|
| `effect.Parameters["MVP"].SetValue(matrix)` | `cmd.PushVertexUniformData(matrix, slot)` | Push constants, not named params |
| `effect.Parameters["Tex"].SetValue(texture)` | `pass.BindFragmentSamplers(slot, binding)` | Explicit slot binding |
| Effect technique/pass model | Pipeline object binding | No multi-pass effects |
| Named parameter reflection | Slot-based binding (0, 1, 2...) | Must match shader layout |

### Uniform Data

```csharp
// FNA
effect.Parameters["WorldViewProjection"].SetValue(wvp);
effect.Parameters["Tint"].SetValue(new Vector4(1, 0, 0, 1));

// MoonWorks — define a struct matching shader layout
[StructLayout(LayoutKind.Sequential)]
struct VertexUniforms
{
    public Matrix4x4 WorldViewProjection;
}

[StructLayout(LayoutKind.Sequential)]
struct FragmentUniforms
{
    public Vector4 Tint;
}

cmd.PushVertexUniformData(new VertexUniforms { WorldViewProjection = wvp }, slot: 0);
cmd.PushFragmentUniformData(new FragmentUniforms { Tint = new Vector4(1, 0, 0, 1) }, slot: 0);
```

### Shader Compilation

- **FNA:** HLSL → `fxc.exe` → `.fxb` bytecode
- **MoonWorks:** GLSL/HLSL → `spirv-cross`/`dxc`/`metal` → per-backend binaries. The project embeds shaders for multiple backends. SDL3-GPU selects the appropriate format at runtime.

---

## Vertex Definitions

### FNA: VertexDeclaration + VertexElementUsage

```csharp
public struct VertexPositionColorTexture : IVertexType
{
    public Vector3 Position;
    public Color Color;
    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color,   VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
```

- Uses **semantic-based** binding (`Position`, `Color`, `TextureCoordinate`)
- `VertexElementUsage` enum maps to HLSL semantics

### MoonWorks: IVertexType + Location-Based Binding

```csharp
[StructLayout(LayoutKind.Explicit)]
public struct PositionColorVertex : IVertexType
{
    [FieldOffset(0)]
    public Float3 Position;

    [FieldOffset(12)]
    public Ubyte4Norm Color;

    public static VertexElementFormat[] Formats => new[]
    {
        VertexElementFormat.Float3,     // location 0
        VertexElementFormat.Ubyte4Norm  // location 1
    };

    public static uint[] Offsets => new uint[] { 0, 12 };
}
```

- Uses **location-based** binding (matches shader `layout(location = N)`)
- Implements `IVertexType` with static `Formats` and `Offsets`
- Uses `VertexStructs` helper types (`Float3`, `Float2`, `Ubyte4Norm`, etc.)
- Build the input state with: `VertexInputState.CreateSingleBinding<PositionColorVertex>()`

### Built-in Vertex Element Types (MoonWorks)

| MoonWorks type | Equivalent | Shader type |
|----------------|-----------|-------------|
| `Float` | `float` | `float` |
| `Float2` | `Vector2` | `vec2` |
| `Float3` | `Vector3` | `vec3` |
| `Float4` | `Vector4` | `vec4` |
| `Ubyte4Norm` | `Color` (normalized) | `vec4` (0–1) |
| `Half2` | half-precision 2D | `vec2` |
| `Half4` | half-precision 4D | `vec4` |
| `Int` / `Int2` / `Int4` | integer vectors | `int` / `ivec2` / `ivec4` |

---

## Render State

### FNA: Mutable State Objects

```csharp
// Predefined states
GraphicsDevice.BlendState = BlendState.AlphaBlend;
GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
GraphicsDevice.DepthStencilState = DepthStencilState.Default;
GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

// Custom blend state
var customBlend = new BlendState();
customBlend.ColorSourceBlend = Blend.SourceAlpha;
customBlend.ColorDestinationBlend = Blend.InverseSourceAlpha;
GraphicsDevice.BlendState = customBlend;
```

### MoonWorks: Immutable Structs Baked into Pipeline

```csharp
// Blend state preset
ColorTargetBlendState.PremultipliedAlphaBlend
ColorTargetBlendState.NonPremultipliedAlphaBlend
ColorTargetBlendState.Additive
ColorTargetBlendState.NoBlend

// Rasterizer state presets
RasterizerState.CCW_CullBack
RasterizerState.CCW_CullFront
RasterizerState.CCW_CullNone
RasterizerState.CCW_Wireframe

// Depth-stencil presets
DepthStencilState.Disable
// Custom: new DepthStencilState { EnableDepthTest = true, CompareOp = CompareOp.LessOrEqual, ... }

// Sampler — separate object, not pipeline state
Sampler sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);
// Presets: PointClamp, PointWrap, LinearClamp, LinearWrap, AnisotropicClamp, AnisotropicWrap
```

### Blend State Mapping

| FNA `BlendState` | MoonWorks `ColorTargetBlendState` |
|-----------------|----------------------------------|
| `BlendState.AlphaBlend` | `ColorTargetBlendState.PremultipliedAlphaBlend` |
| `BlendState.NonPremultiplied` | `ColorTargetBlendState.NonPremultipliedAlphaBlend` |
| `BlendState.Additive` | `ColorTargetBlendState.Additive` |
| `BlendState.Opaque` | `ColorTargetBlendState.NoBlend` |

### Sampler State Mapping

| FNA `SamplerState` | MoonWorks `SamplerCreateInfo` |
|--------------------|-------------------------------|
| `SamplerState.PointClamp` | `SamplerCreateInfo.PointClamp` |
| `SamplerState.PointWrap` | `SamplerCreateInfo.PointWrap` |
| `SamplerState.LinearClamp` | `SamplerCreateInfo.LinearClamp` |
| `SamplerState.LinearWrap` | `SamplerCreateInfo.LinearWrap` |
| `SamplerState.AnisotropicClamp` | `SamplerCreateInfo.AnisotropicClamp` |
| `SamplerState.AnisotropicWrap` | `SamplerCreateInfo.AnisotropicWrap` |

### Rasterizer Mapping

| FNA `RasterizerState` | MoonWorks `RasterizerState` |
|----------------------|----------------------------|
| `RasterizerState.CullClockwise` | Custom: `{ CullMode = CullMode.Back, FrontFace = FrontFace.CW }` |
| `RasterizerState.CullCounterClockwise` | `RasterizerState.CCW_CullBack` |
| `RasterizerState.CullNone` | `RasterizerState.CCW_CullNone` |

### Compare Function Mapping

| FNA `CompareFunction` | MoonWorks `CompareOp` |
|----------------------|----------------------|
| `CompareFunction.Always` | `CompareOp.Always` |
| `CompareFunction.Never` | `CompareOp.Never` |
| `CompareFunction.Less` | `CompareOp.Less` |
| `CompareFunction.LessEqual` | `CompareOp.LessOrEqual` |
| `CompareFunction.Equal` | `CompareOp.Equal` |
| `CompareFunction.Greater` | `CompareOp.Greater` |
| `CompareFunction.GreaterEqual` | `CompareOp.GreaterOrEqual` |
| `CompareFunction.NotEqual` | `CompareOp.NotEqual` |

---

## Textures & Render Targets

### FNA

```csharp
// Load texture
Texture2D tex = Content.Load<Texture2D>("mytexture");
// Or create manually
var tex = new Texture2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color);
tex.SetData(pixels);

// Render target
var rt = new RenderTarget2D(GraphicsDevice, 1920, 1080, false,
    SurfaceFormat.Color, DepthFormat.Depth24);
GraphicsDevice.SetRenderTarget(rt);
// ... draw ...
GraphicsDevice.SetRenderTarget(null); // Back to backbuffer
```

### MoonWorks

```csharp
// Create texture
Texture tex = Texture.Create2D(GraphicsDevice, 256, 256,
    TextureFormat.R8G8B8A8Unorm,
    TextureUsageFlags.Sampler);  // Usage flags are mandatory

// Render target (just a texture with ColorTarget usage)
Texture rt = Texture.Create2D(GraphicsDevice, 1920, 1080,
    TextureFormat.R8G8B8A8Unorm,
    TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);

// Depth buffer
Texture depth = Texture.Create2D(GraphicsDevice, 1920, 1080,
    GraphicsDevice.SupportedDepthFormat,
    TextureUsageFlags.DepthStencilTarget);

// Render to target — specify in BeginRenderPass
using (RenderPass pass = cmd.BeginRenderPass(
    new ColorTargetInfo { TextureSlice = rt, LoadOp = LoadOp.Clear, StoreOp = StoreOp.Store },
    new DepthStencilTargetInfo { TextureSlice = depth, LoadOp = LoadOp.Clear, StoreOp = StoreOp.DontCare }))
{
    // ... draw ...
}

// Render to backbuffer
Texture backbuffer = cmd.AcquireSwapchainTexture(MainWindow);
using (RenderPass pass = cmd.BeginRenderPass(
    new ColorTargetInfo { TextureSlice = backbuffer, LoadOp = LoadOp.Clear, StoreOp = StoreOp.Store }))
{
    // ... draw ...
}
```

### Texture Format Mapping

| FNA `SurfaceFormat` | MoonWorks `TextureFormat` |
|--------------------|--------------------------|
| `SurfaceFormat.Color` | `TextureFormat.R8G8B8A8Unorm` |
| `SurfaceFormat.Rgba64` | `TextureFormat.R16G16B16A16Unorm` |
| `SurfaceFormat.Single` | `TextureFormat.R32Float` |
| `SurfaceFormat.Vector2` | `TextureFormat.R32G32Float` |
| `SurfaceFormat.Vector4` | `TextureFormat.R32G32B32A32Float` |
| `SurfaceFormat.HalfVector2` | `TextureFormat.R16G16Float` |
| `SurfaceFormat.HalfVector4` | `TextureFormat.R16G16B16A16Float` |
| `SurfaceFormat.HdrBlendable` | `TextureFormat.R16G16B16A16Float` |
| `SurfaceFormat.ColorSrgbEXT` | `TextureFormat.R8G8B8A8UnormSrgb` |

### Texture Usage Flags (MoonWorks-specific)

MoonWorks requires explicit usage flags at creation time:

| Flag | Meaning |
|------|---------|
| `TextureUsageFlags.Sampler` | Can be sampled in shaders |
| `TextureUsageFlags.ColorTarget` | Can be used as a render target color attachment |
| `TextureUsageFlags.DepthStencilTarget` | Can be used as depth/stencil attachment |
| `TextureUsageFlags.GraphicsStorageRead` | Can be read as storage texture in graphics shaders |
| `TextureUsageFlags.ComputeStorageRead` | Can be read as storage texture in compute shaders |
| `TextureUsageFlags.ComputeStorageWrite` | Can be written as storage texture in compute shaders |
| `TextureUsageFlags.ComputeStorageSimultaneousReadWrite` | Simultaneous R/W in compute |

### Key Difference: No Implicit SetRenderTarget(null)

In FNA, `SetRenderTarget(null)` switches back to the backbuffer. In MoonWorks, each render pass explicitly specifies its target. The backbuffer is acquired via `cmd.AcquireSwapchainTexture(window)` and passed as a color target.

---

## SpriteBatch Replacement

FNA's `SpriteBatch` is a convenience class for 2D sprite rendering. **MoonWorks does not include a SpriteBatch.** You must implement your own 2D batching or use a library.

### FNA SpriteBatch Pattern

```csharp
spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
    SamplerState.PointClamp, null, null, null, transformMatrix);
spriteBatch.Draw(texture, position, sourceRect, color, rotation, origin, scale, effects, depth);
spriteBatch.DrawString(font, "Hello", position, Color.White);
spriteBatch.End();
```

### MoonWorks Equivalent (Manual Implementation)

You need to:
1. Create a sprite vertex type with position, texcoord, and color
2. Create a vertex/fragment shader pair for textured quads
3. Create a `GraphicsPipeline` with appropriate blend state
4. Batch sprite quads into a dynamic vertex buffer
5. Submit draw calls within a render pass

This is a significant porting effort. Consider extracting the sprite batching into a reusable helper class early in the port.

---

## Input System

### FNA: Static Polling

```csharp
KeyboardState kb = Keyboard.GetState();
if (kb.IsKeyDown(Keys.Space)) { /* jump */ }

MouseState mouse = Mouse.GetState();
int mouseX = mouse.X;
bool leftClick = mouse.LeftButton == ButtonState.Pressed;

GamePadState pad = GamePad.GetState(PlayerIndex.One);
float leftStickX = pad.ThumbSticks.Left.X;
```

### MoonWorks: Instance-Based with Press/Release Detection

```csharp
// Access via Game.Inputs property
Inputs inputs = Inputs;

// Keyboard — has press/hold/release detection built in
if (inputs.Keyboard.IsPressed(ScanCode.Space))  { /* just pressed this frame */ }
if (inputs.Keyboard.IsHeld(ScanCode.Space))     { /* held down */ }
if (inputs.Keyboard.IsReleased(ScanCode.Space)) { /* just released this frame */ }

// Mouse
int mouseX = inputs.Mouse.X;
int deltaX = inputs.Mouse.DeltaX;
if (inputs.Mouse.LeftButton.IsPressed)  { /* just clicked */ }
if (inputs.Mouse.LeftButton.IsHeld)     { /* held */ }
if (inputs.Mouse.LeftButton.IsReleased) { /* just released */ }
int wheelDelta = inputs.Mouse.Wheel;

// Gamepad
Gamepad pad = inputs.GetGamepad(0);
if (pad != null)
{
    if (pad.South.IsPressed) { /* A button just pressed */ }
    float leftX = pad.LeftX.Value;
}

// Text input events
inputs.TextInput += (char c) => { /* typed character */ };
```

### Input Mapping

| FNA | MoonWorks | Notes |
|-----|-----------|-------|
| `Keyboard.GetState()` | `inputs.Keyboard` | Instance, not static |
| `Keys.Space` | `ScanCode.Space` or `KeyCode.Space` | Scancode = physical, Keycode = logical |
| `kb.IsKeyDown(key)` | `keyboard.IsHeld(scancode)` | `IsPressed` = just this frame |
| `Mouse.GetState()` | `inputs.Mouse` | Instance |
| `mouse.LeftButton == ButtonState.Pressed` | `mouse.LeftButton.IsHeld` | Button is an object with state |
| `mouse.ScrollWheelValue` (cumulative) | `mouse.Wheel` (delta per frame) | **Breaking change**: delta vs cumulative |
| `GamePad.GetState(PlayerIndex.One)` | `inputs.GetGamepad(0)` | Returns `null` if disconnected |
| `Buttons.A` | `pad.South` | Xbox-style → positional naming |
| `Buttons.B` | `pad.East` | |
| `Buttons.X` | `pad.West` | |
| `Buttons.Y` | `pad.North` | |
| `pad.ThumbSticks.Left.X` | `pad.LeftX.Value` | Separate axis objects |

### FNA Button → MoonWorks Button

| FNA | MoonWorks |
|-----|-----------|
| `Buttons.A` | `Gamepad.South` |
| `Buttons.B` | `Gamepad.East` |
| `Buttons.X` | `Gamepad.West` |
| `Buttons.Y` | `Gamepad.North` |
| `Buttons.Start` | `Gamepad.Start` |
| `Buttons.Back` | `Gamepad.Back` |
| `Buttons.BigButton` | `Gamepad.Guide` |
| `Buttons.LeftShoulder` | `Gamepad.LeftShoulder` |
| `Buttons.RightShoulder` | `Gamepad.RightShoulder` |
| `Buttons.LeftStick` | `Gamepad.LeftStick` |
| `Buttons.RightStick` | `Gamepad.RightStick` |
| `Buttons.DPadUp` | `Gamepad.DpadUp` |

---

## Audio System

Both FNA and MoonWorks use **FAudio** as the underlying audio engine, but the API surface differs.

### FNA: XNA-Compatible

```csharp
// Load and play
SoundEffect sfx = Content.Load<SoundEffect>("explosion");
sfx.Play(volume: 0.8f, pitch: 0f, pan: 0f);

// Instance for looping/control
SoundEffectInstance instance = sfx.CreateInstance();
instance.IsLooped = true;
instance.Volume = 0.5f;
instance.Play();
instance.Stop();

// 3D audio
var listener = new AudioListener();
listener.Position = cameraPosition;
var emitter = new AudioEmitter();
emitter.Position = soundPosition;
instance.Apply3D(listener, emitter);
```

### MoonWorks: Voice-Based

```csharp
// Load audio data
AudioBuffer audioBuffer = AudioDataWav.Create(AudioDevice, TitleStorage, "explosion.wav");

// Play via transient voice (fire-and-forget)
TransientVoice voice = AudioDevice.Obtain<TransientVoice>(audioBuffer.Format);
voice.Submit(audioBuffer);
voice.Volume = 0.8f;
voice.Play();

// Persistent voice (for looping/music)
PersistentVoice music = AudioDevice.Obtain<PersistentVoice>(audioBuffer.Format);
music.Submit(audioBuffer);
music.IsLooped = true;
music.Volume = 0.5f;
music.Play();
music.Stop();

// 3D audio
voice.Is3D = true;
// Set listener/emitter positions via AudioListener/AudioEmitter
```

### Audio Mapping

| FNA | MoonWorks | Notes |
|-----|-----------|-------|
| `SoundEffect` | `AudioBuffer` / `AudioDataWav` / `AudioDataOgg` | Data container |
| `SoundEffect.Play()` | `TransientVoice.Submit()` + `.Play()` | Two-step |
| `SoundEffectInstance` | `PersistentVoice` | For controlled playback |
| `SoundEffect.CreateInstance()` | `AudioDevice.Obtain<PersistentVoice>(format)` | Pool-based |
| `SoundEffectInstance.IsLooped` | `SourceVoice.IsLooped` | Same concept, not available on transient |
| `instance.Volume/Pitch/Pan` | `voice.Volume/Pitch/Pan` | Same ranges |
| `AudioListener`/`AudioEmitter` | `AudioListener`/`AudioEmitter` | Similar but in `MoonWorks.Audio` namespace |
| `SoundEffect.MasterVolume` | `AudioDevice.MasteringVoice.Volume` | Via mastering voice |
| `Content.Load<SoundEffect>()` | `AudioDataWav.Create(AudioDevice, TitleStorage, path)` | Direct file load |
| `MediaPlayer` (music) | `StreamingAudioSource` | For long audio streams |

### Voice Hierarchy

MoonWorks has a more explicit audio graph:

```
SourceVoice (TransientVoice / PersistentVoice)
    → SubmixVoice (optional processing: reverb, effects)
        → MasteringVoice (final output)
```

```csharp
// Add reverb
SubmixVoice reverbBus = new SubmixVoice(AudioDevice);
ReverbEffect reverb = new ReverbEffect(AudioDevice);
reverbBus.SetEffect(reverb);

voice.OutputVoice = reverbBus;  // Route through reverb
```

---

## Content / Asset Loading

### FNA: ContentManager

```csharp
Content.RootDirectory = "Content";
Texture2D tex = Content.Load<Texture2D>("sprites/player");
Effect shader = Content.Load<Effect>("shaders/myshader");
SoundEffect sfx = Content.Load<SoundEffect>("audio/boom");
SpriteFont font = Content.Load<SpriteFont>("fonts/arial");

Content.Unload(); // Unload everything
```

- Loads `.xnb` files (XNA binary format) or raw assets
- Caches loaded assets by name
- Type readers handle deserialization

### MoonWorks: TitleStorage + Direct Loading

MoonWorks has **no content pipeline**. Assets are loaded directly from files.

```csharp
// TitleStorage for read-only game assets
TitleStorage storage = TitleStorage; // Available on Game

// Check file existence
if (storage.Exists("textures/player.png"))
{
    ulong size;
    storage.GetFileSize("textures/player.png", out size);
    byte[] data = new byte[size];
    storage.ReadFile("textures/player.png", data);
}

// Shaders
Shader vert = Shader.Create(GraphicsDevice, TitleStorage, "shaders/vert.spv", "main", info);

// Audio
AudioBuffer audio = AudioDataWav.Create(AudioDevice, TitleStorage, "audio/boom.wav");

// UserStorage for save data (read-write)
UserStorage userStorage = UserStorage;
```

### Asset Loading Mapping

| FNA | MoonWorks |
|-----|-----------|
| `Content.Load<Texture2D>("name")` | `Texture.Create2D()` + `ResourceUploader` from raw image bytes |
| `Content.Load<Effect>("name")` | `Shader.Create()` with compiled shader file |
| `Content.Load<SoundEffect>("name")` | `AudioDataWav.Create()` / `AudioDataOgg.Create()` |
| `Content.Load<SpriteFont>("name")` | No built-in SpriteFont; use WellspringCS for font rendering |
| `Content.Unload()` | Dispose resources individually |
| `ContentManager` caching | Implement your own asset cache |

---

## Math Types

### FNA Math Types

FNA provides the full XNA math library in `Microsoft.Xna.Framework`:

- `Vector2`, `Vector3`, `Vector4` — float vectors
- `Matrix` — 4x4 float matrix (row-major)
- `Quaternion` — rotation quaternion
- `Color` — 32-bit RGBA packed color
- `Rectangle`, `Point` — 2D integer types
- `BoundingBox`, `BoundingSphere`, `BoundingFrustum` — collision primitives
- `MathHelper` — Clamp, Lerp, ToDegrees, ToRadians, etc.
- `Curve`, `CurveKey` — animation curves

### MoonWorks Math Types

MoonWorks provides a **minimal** math library in `MoonWorks.Math`:

- `MathHelper` — Barycentric, CatmullRom, Hermite, Clamp, Lerp, etc.
- `Easing` — Easing functions for animations
- `MoonWorks.Math.Fixed` — Fixed-point math (`Fix64`, `Vector2`, `Vector3`) for deterministic simulation

MoonWorks **does not provide** floating-point `Vector2`, `Vector3`, `Matrix`, etc. You should use:
- `System.Numerics.Vector2/Vector3/Vector4/Matrix4x4/Quaternion` from the .NET runtime
- Or keep FNA's math types as a separate dependency

### Math Migration Strategy

| FNA (`Microsoft.Xna.Framework`) | .NET (`System.Numerics`) | Notes |
|--------------------------------|-------------------------|-------|
| `Vector2` | `Vector2` | Field-compatible |
| `Vector3` | `Vector3` | Field-compatible |
| `Vector4` | `Vector4` | Field-compatible |
| `Matrix` | `Matrix4x4` | Different field layout; FNA is row-major `M11..M44`, System.Numerics is also row-major but transposition may differ in shader upload |
| `Quaternion` | `Quaternion` | Compatible |
| `Color` (RGBA struct) | `MoonWorks.Graphics.Color` | MoonWorks has its own Color type |
| `Rectangle` | Custom or `System.Drawing.Rectangle` | No direct equivalent |
| `MathHelper.Clamp()` | `Math.Clamp()` or `MoonWorks.Math.MathHelper.Clamp()` | |
| `MathHelper.Lerp()` | `MoonWorks.Math.MathHelper.Lerp()` | |

---

## Window & Display

### FNA

```csharp
// Via GraphicsDeviceManager
graphics.PreferredBackBufferWidth = 1920;
graphics.PreferredBackBufferHeight = 1080;
graphics.IsFullScreen = true;
graphics.ApplyChanges();

// Window properties
Window.Title = "My Game";
Window.AllowUserResizing = true;
```

### MoonWorks

```csharp
// At construction time
var windowInfo = new WindowCreateInfo
{
    WindowTitle = "My Game",
    WindowWidth = 1920,
    WindowHeight = 1080,
    ScreenMode = ScreenMode.Windowed, // Windowed, Fullscreen, Maximized
    SystemResizable = true,
    HighDPI = true
};

// Runtime changes
MainWindow.SetScreenMode(ScreenMode.Fullscreen);
MainWindow.SetSize(1920, 1080);
MainWindow.Show();
MainWindow.Hide();

// Swapchain parameters
GraphicsDevice.SetSwapchainParameters(MainWindow,
    SwapchainComposition.SDR, PresentMode.VSync);

// Query
uint w = MainWindow.Width;
uint h = MainWindow.Height;
float scale = MainWindow.DisplayScale;
```

### Present Mode Mapping

| FNA | MoonWorks |
|-----|-----------|
| `graphics.SynchronizeWithVerticalRetrace = true` | `PresentMode.VSync` |
| `graphics.SynchronizeWithVerticalRetrace = false` | `PresentMode.Immediate` |
| — | `PresentMode.Mailbox` (triple-buffered, no tearing) |

---

## Common Porting Patterns

### Pattern 1: Converting a Draw Call

**FNA:**
```csharp
GraphicsDevice.SetVertexBuffer(vb);
GraphicsDevice.Indices = ib;
effect.Parameters["WVP"].SetValue(wvp);
effect.CurrentTechnique.Passes[0].Apply();
GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertCount, 0, primCount);
```

**MoonWorks:**
```csharp
cmd.PushVertexUniformData(wvp);
pass.BindGraphicsPipeline(pipeline);
pass.BindVertexBuffers(0, new BufferBinding(vb, 0));
pass.BindIndexBuffer(new BufferBinding(ib, 0), IndexElementSize.ThirtyTwoBits);
pass.DrawIndexedPrimitives(indexCount, 1, 0, 0, 0);
```

### Pattern 2: Converting Render-to-Texture

**FNA:**
```csharp
GraphicsDevice.SetRenderTarget(offscreen);
GraphicsDevice.Clear(Color.Transparent);
// draw scene
GraphicsDevice.SetRenderTarget(null);
// use offscreen as texture
spriteBatch.Draw(offscreen, Vector2.Zero, Color.White);
```

**MoonWorks:**
```csharp
// Pass 1: render to offscreen
using (var pass = cmd.BeginRenderPass(new ColorTargetInfo
{
    TextureSlice = offscreenTexture, LoadOp = LoadOp.Clear,
    StoreOp = StoreOp.Store, ClearColor = new Color(0, 0, 0, 0)
}))
{
    // draw scene
}

// Pass 2: render offscreen to backbuffer
Texture bb = cmd.AcquireSwapchainTexture(MainWindow);
using (var pass = cmd.BeginRenderPass(new ColorTargetInfo
{
    TextureSlice = bb, LoadOp = LoadOp.Clear, StoreOp = StoreOp.Store
}))
{
    pass.BindGraphicsPipeline(fullscreenQuadPipeline);
    pass.BindFragmentSamplers(0, new TextureSamplerBinding(offscreenTexture, linearSampler));
    pass.DrawPrimitives(6, 1, 0, 0); // fullscreen quad
}
```

### Pattern 3: Dynamic Buffer Updates

**FNA:**
```csharp
dynamicVB.SetData(vertices, 0, count, SetDataOptions.Discard);
```

**MoonWorks:**
```csharp
var transfer = TransferBuffer.Create<MyVertex>(device, TransferBufferUsage.Upload, count);
var span = transfer.Map<MyVertex>(cycle: false);
vertices.AsSpan(0, count).CopyTo(span);
transfer.Unmap();

using (var copy = cmd.BeginCopyPass())
{
    copy.UploadToBuffer(
        new TransferBufferLocation(transfer),
        new BufferRegion(dynamicVB, 0, (uint)(count * Unsafe.SizeOf<MyVertex>())),
        cycle: true);  // cycle: true because buffer may be in-flight
}
```

### Pattern 4: Reading Back Pixel Data

**FNA:**
```csharp
Color[] pixels = new Color[tex.Width * tex.Height];
tex.GetData(pixels);
```

**MoonWorks:**
```csharp
var readback = TransferBuffer.Create<Color>(device, TransferBufferUsage.Download,
    (uint)(tex.Width * tex.Height));

var cmd = GraphicsDevice.AcquireCommandBuffer();
using (var copy = cmd.BeginCopyPass())
{
    copy.DownloadFromTexture(
        new TextureRegion(tex),
        new TextureTransferInfo(readback),
        cycle: false);
}
Fence fence = GraphicsDevice.SubmitAndAcquireFence(cmd);
GraphicsDevice.WaitForFence(fence);

var pixels = readback.Map<Color>(cycle: false);
// ... use pixels ...
readback.Unmap();
```

---

## Checklist

When porting a class/system from FNA to MoonWorks, use this checklist:

- [ ] **Namespaces**: Replace `using Microsoft.Xna.Framework` → `using MoonWorks`, `using MoonWorks.Graphics`, `using MoonWorks.Audio`, `using MoonWorks.Input`
- [ ] **Math types**: Replace `Microsoft.Xna.Framework.Vector2/3/4/Matrix/Quaternion` → `System.Numerics` equivalents or keep as shared lib
- [ ] **Game subclass**: Change base from `Game` to `MoonWorks.Game`, update constructor, implement `Step()`/`Update(TimeSpan)`/`Draw(double)`
- [ ] **GraphicsDeviceManager**: Remove; device is created by `MoonWorks.Game` base class
- [ ] **ContentManager**: Remove; load assets via `TitleStorage` + factory methods
- [ ] **SpriteBatch**: Implement custom 2D batcher or port existing one
- [ ] **Effect/shader loading**: Convert HLSL → SPIR-V/backend shaders; replace `Effect` parameters with uniform structs + push constants
- [ ] **Texture creation**: Use `Texture.Create2D()` with explicit format and usage flags
- [ ] **Buffer creation**: Use `Buffer.Create<T>()` with explicit usage flags
- [ ] **Data upload**: Replace `SetData()` with `TransferBuffer` → `CopyPass` pattern
- [ ] **Render state**: Bake blend/depth/rasterizer state into `GraphicsPipeline` objects
- [ ] **Draw calls**: Wrap in `CommandBuffer` → `BeginRenderPass()` → bind pipeline/resources → draw → end pass → submit
- [ ] **Render targets**: Replace `SetRenderTarget()` calls with explicit `ColorTargetInfo` in `BeginRenderPass()`
- [ ] **Sampler state**: Create `Sampler` objects; bind via `BindFragmentSamplers()` in render pass
- [ ] **Input**: Replace static `Keyboard.GetState()`/`Mouse.GetState()` with `Inputs.Keyboard`/`Inputs.Mouse` instance access
- [ ] **Audio**: Replace `SoundEffect`/`SoundEffectInstance` with `AudioBuffer`/`TransientVoice`/`PersistentVoice`
- [ ] **Present/swap**: Remove `GraphicsDevice.Present()`; use `cmd.AcquireSwapchainTexture()` + `GraphicsDevice.Submit(cmd)`
- [ ] **Dispose**: Explicitly dispose all GPU resources; MoonWorks does not auto-dispose via GC
- [ ] **Vertex types**: Convert `IVertexType` (semantic-based) to MoonWorks `IVertexType` (location-based) with `VertexStructs` element types
