---
name: sokol-d3d11-hlsl-semantic-names-must-match-signature
description: "sokol/D3D11 input layouts must name semantics as the compiled VS declares them; spirv-cross renames every vertex input to TEXCOORD<location>, so the abstraction's POSITION/COLOR names can never match a generated bundle"
metadata: 
  node_type: memory
  type: project
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-21T21:17:10.892Z
---

For the `NFMWorld.Graphics.Sokol` backend, D3D11 **requires the input layout's declared
semantic name to equal the compiled vertex shader's own signature name**. It is a name
match, not a position match. Verified with a standalone probe in
`scratch/IlProbe/Program.cs` (control case `TEXCOORD0/TEXCOORD1` against a matching
signature is ACCEPTED; `POSITION0/COLOR0` against that same signature is
`0x80070057 REJECTED`; a superset layout is accepted).

The consequence is a hard conflict with the abstraction's layouts:

- **spirv-cross renames *every* vertex input to `TEXCOORD<location>`** in the HLSL it
  emits - position included, never `POSITION0`. Confirmed against real output in
  `scratch/ShaderPoc/out.Vertex.hlsl` (`float4 v_Position : TEXCOORD0;`).
- The app's layouts declare source semantics: `POSITION`/`COLOR`/`NORMAL`/`TEXCOORD`
  (`nfm-world/Mad/Renderer/PositionColorVertex.cs`, `Mesh.cs`, `InstanceData.cs`).
- Upstream `sokol-shdc` resolves this by **hardcoding** it, not by reading the source:
  `reflection.cc` sets `refl_attr.sem_name = "TEXCOORD"; refl_attr.sem_index = refl_attr.slot;`
  for every stage input, and `generators/sokolc.cc:306-309` emits that as
  `hlsl_sem_name`/`hlsl_sem_index`. It never consults the original semantic name.

sokol's own `sokol_debugtext.h`/`sokol_fontstash.h` do the same (`TEXCOORD` + index).
The backend as written does the *opposite* - it passes the source semantic through
`SokolMapping.SemanticName` - which is why it only works with hand-written HLSL that
happens to declare `POSITION0`/`COLOR0` (see `NFMWorld.Graphics.Sokol.Smoke/Program.cs`).
Two ways out, both real design decisions rather than bug fixes: emit HLSL that preserves
the source semantics (`spvc_compiler_hlsl_add_vertex_attribute_remap`, or
`CompilerOption.HlslUserSemantic`), or adopt upstream's approach and derive
`TEXCOORD<location>` from the shader reflection's inputs, ignoring the layout's semantic
strings. Related: [[sokol-backend-bundle-shaders-need-hlsl]].

Method warning: this was nearly concluded backwards. The first two probe revisions
returned REJECTED for *every* case, including the control, because the probe passed the
COM interface pointer instead of the blob data pointer to `CreateInputLayout`. **A probe
whose control case fails is a broken instrument, not a finding** - fix the harness before
believing any verdict from it.
