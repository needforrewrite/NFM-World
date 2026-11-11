---
name: spirv-cross-vertex-semantics-restore-via-remap-table
description: "glslang drops HLSL vertex semantics, so spirv-cross's HLSL_USER_SEMANTIC option is useless; the only way to restore POSITION0/COLOR0 is an explicit location->semantic remap table fed to add_vertex_attribute_remap"
metadata: 
  node_type: memory
  type: reference
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-21T21:37:57.543Z
---

For the NFM-World shader pipeline (`scratch/ShaderPoc/`), spirv-cross renames every vertex
input to `TEXCOORD<location>`. Restoring the original `POSITION0`/`COLOR0`/etc. requires
`spvc_compiler_hlsl_add_vertex_attribute_remap` with an explicit table. Verified empirically
with `scratch/SemProbe/Program.cs` (fixture: `POSITION0`, `COLOR0`, `NORMAL0`, `POSITION2`):

- **`SPVC_COMPILER_OPTION_HLSL_USER_SEMANTIC` does nothing here.** It makes spirv-cross read
  `SpvDecorationUserSemantic` as literal HLSL semantics, but **glslang's HLSL front-end never
  emits that decoration** - the probe found only `Location` on every stage input. The option
  is a no-op for this pipeline, so do not reach for it.
- **The remap table works exactly**, preserving indices: `input_Position : POSITION0`,
  `input_Color : COLOR0`, `input_Normal : NORMAL0`, `input_Centroid : POSITION2`.

Consequence for the design: since the semantics are not recoverable from the SPIR-V, **they
must be captured from the HLSL source before glslang runs** and carried through to the
emitter. Silk.NET's C# shape is `HlslVertexAttributeRemap { uint Location; byte* Semantic; }`
(null-terminated ASCII) passed to
`Cross.CompilerHlslAddVertexAttributeRemap(Compiler*, HlslVertexAttributeRemap*, UIntPtr count)`,
called after `CompilerInstallCompilerOptions`. The semantic strings must stay pinned for the
duration of the call. Note `CompilerHasDecoration` returns `byte`, not `bool`.

This is the mechanism behind [[sokol-d3d11-hlsl-semantic-names-must-match-signature]]: the
backend and the D3D11 input layout need those restored names to agree.
