---
name: d3dreflect-mangles-cbuffer-member-names
description: "D3DReflect reports cbuffer members with a per-shader-assembly prefix (_360_Projection), so compiled packoffsets cannot be matched to the bundle's reflection by name without stripping it."
metadata: 
  node_type: memory
  type: reference
  originSessionId: fb6d0e00-bea5-442b-9d16-4010da4061f4
  modified: 2026-09-26T12:08:16.315Z
---

`ID3D11ShaderReflection` reports every member of a declared constant buffer with a mangling prefix
that is **not** in the source and **not** stable across compilations: the HLSL declares
`cbuffer _Global : register(b0) { float4x4 Projection : packoffset(c18); }` and D3DReflect returns
the name `_360_Projection`. The block's own name is reported verbatim (`_Global`), only members are
mangled. The number varies per build, so it cannot be predicted or matched by construction.

The rule that recovers the source name: leading underscores, then decimal digits, then one more
underscore. Implemented as `D3D11Reflector.MemberName` in `NFMWorld.Graphics.D3D11/D3D11PipelineState.cs`.
It rejects `_1` (no trailing underscore) and `__Restore` (no digits), and no uniform in this shader
set has a name of that shape, so anything matching is always mangled.

This is the D3D11 counterpart of `GlShaderProgram.MemberName`, which strips ANGLE's
`<block>.<member>` qualifier. Both exist because a backend reading a *driver's* reflection has to
reconcile that driver's spelling with the reflection the generated bundle carries — the offsets agree,
the names do not, and the names are the only key the two share besides the offsets.

Symptom when it is missing: the smoke test reports the compiled cbuffer *size* matching while claiming
every single uniform is "not in the compiled block at all". That signature — size right, all names
absent — points at a naming mismatch, not at a layout mismatch.
