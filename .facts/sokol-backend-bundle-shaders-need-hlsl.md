---
name: sokol-backend-bundle-shaders-need-hlsl
description: "NFMWorld.Graphics.Sokol's native sokol.dll is a D3D11-only build that cannot consume SPIR-V, so generated shader bundles must also carry spirv-cross's HLSL"
metadata: 
  node_type: memory
  type: project
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-21T21:17:14.648Z
---

The `sokol.dll` vendored by `NFMWorld.Graphics.Sokol` (`NFMWorld.Graphics.Sokol/native/win-x64/`,
from the SharpSokol `v0.1.0` release) is a **D3D11-only** build: no GL entry points, and its
shader path does not translate bytecode. `sg_make_shader` hands `bytecode.ptr` straight to
`CreateVertexShader` as a **DXBC** blob (sokol_gfx.h:14318), or calls `D3DCompile` on `source`
when that field is set instead (sokol_gfx.h:14343).

The shader compiler's emitted bundles carry **SPIR-V** (`scratch/ShaderPoc/Line.g.cs` holds
`private static ReadOnlyMemory<byte> VertexSpirv`), which is neither DXBC nor HLSL, so a bundle
cannot reach `sg_make_shader` on this backend as it stands. The door is already built on the
backend side - `SokolShaderProgram` prefers `source` over `bytecode`, and `LoadProgram`'s
`vertexSource`/`pixelSource` parameters accept HLSL - but nothing *emits* that HLSL yet.
`SpirvCrossReflector.StageResult.Hlsl` already produces it (it is compiled for the GLSL backend
anyway and is what the reflection is derived alongside), so the remaining work is extending
`scratch/ShaderPoc/CSharpBundleEmitter.cs` to emit it into the bundle.

The current smoke test only passes because `NFMWorld.Graphics.Sokol.Smoke/Program.cs`
hand-writes its HLSL instead of loading a generated bundle. Note the two blockers are
independent: fixing the bytecode format does not fix the semantic-name mismatch described in
[[sokol-d3d11-hlsl-semantic-names-must-match-signature]], and vice versa.
