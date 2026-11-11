---
name: glslang-assigns-vertex-location-in-declaration-order
description: "glslang assigns HLSL vertex-input Location by declaration order, not semantic index, so a declared-semantics list in source order zips 1:1 onto the sorted reflected locations"
metadata: 
  node_type: memory
  type: reference
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-21T21:39:34.260Z
---

In the NFM-World shader pipeline (`scratch/ShaderPoc/`), glslang's HLSL front-end assigns
`Location` to vertex inputs in **declaration order**, ignoring the semantic index. Verified
with `scratch/SemProbe/LocationProbe.cs` against the real `nfm-world/data/shaders/Line.fx`
input struct, whose semantic indices are deliberately out of declaration order
(`POSITION0, POSITION1, TEXCOORD0, NORMAL0, COLOR0, POSITION2, TEXCOORD1`): locations 0..6
came back `PositionA, PositionB, Side, Normal, Color, Centroid, DecalOffset`, i.e. exactly
source order.

This is what makes the remap table in
[[spirv-cross-vertex-semantics-restore-via-remap-table]] constructible: read the VS input
struct's fields in declaration order, sort the reflected locations ascending, and zip the two
lists. The semantic *index* is not recoverable from the SPIR-V, so it must come from the
source text.

Two gotchas worth remembering:
- The reflected input name glslang reports is the **HLSL field name** (`PositionA`), never the
  semantic (`POSITION0`). Comparing those two strings textually is meaningless.
- The first version of this probe made exactly that mistake and printed
  "Location is NOT declaration order" while the listed data plainly showed it *was*. The
  lesson generalises: when a probe's verdict contradicts the rows it just printed, suspect the
  comparison logic before believing the verdict - same failure mode as the broken control case
  noted in [[sokol-d3d11-hlsl-semantic-names-must-match-signature]].
