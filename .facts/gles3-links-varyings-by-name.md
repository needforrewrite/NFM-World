---
name: gles3-links-varyings-by-name
description: "OpenGL ES 3.0 links varyings by name (no layout(location) allowed), so spirv-cross's _entryPointOutput_X vs input_X naming breaks every HLSL-sourced program at link, not compile."
metadata: 
  node_type: memory
  type: project
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-22T19:56:24.379Z
---

A HLSL -> SPIR-V -> GLSL ES 3.0 pipeline produces two stages whose varying names never
agree: spirv-cross names vertex outputs `_entryPointOutput_Color` (after the entry point's
return value) and fragment inputs `input_Color` (after the original HLSL parameter).

Both stages compile cleanly. Only `glLinkProgram` fails, with "FRAGMENT varying input_Color
does not match any VERTEX varying".

The reason is that the three targets match varyings differently:

- HLSL matches by **semantic**
- desktop GLSL (410+) matches by **layout(location)** on the varying
- core ES 3.0 matches by **name** alone - ANGLE rejects varying locations outright
  ("invalid layout qualifier: only valid on program inputs and outputs"), and
  `GL_EXT_separate_shader_objects` is not in its extension list
- `CompilerOption.GlslSeparateShaderObjects` does **not** fix it on ES 3.0

**Why:** this is invisible to reading the source and to `glCompileShader`; it only appears at
link, and it affects every shader rather than an edge case.

**How to apply:** rename the varying in the spirv-cross IR (`CompilerSetName`, keyed by
`Decoration.Location`, skipping `Decoration.BuiltIn`) on **both** stages so they independently
derive the same name. Do it in the IR, not the emitted text - the text substitution cannot tell
a varying apart from a same-named vertex attribute (Poly's vertex stage has both an
`input_Color` attribute and an `input_Color` varying, so a blind rename produces a redefinition).
Because `ContextCreateCompiler` uses `CaptureMode.Copy`, the rename stays scoped to the ES
compiler and does not leak into the HLSL/MSL/desktop-GLSL passes. Implemented as
`HarmonizeVaryingNames` in `scratch/ShaderPoc/SpirvCrossReflector.cs`; verified by
`scratch/ShaderPoc/check-es.sh` (all 8 bundles compile and link under real ANGLE).

Related: [[sokol-d3d11-hlsl-semantic-names-must-match-signature]],
[[angle-deferred-shader-compilation]]
