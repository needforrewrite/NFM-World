# HLSL -> sokol-shdc: spike results

**Verdict: the pipeline works.** A single HLSL `.fx` can drive both backends, and
`DXC -> SPIR-V -> spirv-cross -> sokol-shdc` is viable for the shaders here. The
blocker is not the translation, it is that much of the existing HLSL is written
in D3D9 idioms `dxc` refuses outright. That is a source-level migration, not a
tooling problem.

## What was tested

`apos-shapes.fx` (the hardest case, 3239 lines, heavy control flow and 6
textures) end to end, plus every shader in `nfm-world/data/shaders`.

## The pipeline

```
shader.fx
  |  dxc -T vs_6_0 -E <entry> -spirv -fvk-use-dx-layout -Zpc -D<defines>
  v
  vs.spv / fs.spv                       (validated with spirv-val)
  |  spirv-cross -V --version 450 --relax-nan-checks
  v
  Vulkan GLSL  (named uniform block, separate texture2D + sampler uniforms)
  |  bridge.mjs   <- 5 mechanical rewrites, see below
  v
  sokol-shdc annotated .glsl
  |  sokol-shdc -l glsl430:glsl300es:hlsl5:metal_macos:wgsl
  v
  20 MB header with glsl430 / glsl300es / hlsl5 / metal_macos / wgsl
```

`run.sh` drives all of it. `./run.sh <shader.fx> <vsEntry> <psEntry>`.

## The five rewrites `bridge.mjs` applies

Every one is lossless; none changes what the shader computes.

1. **Drop `#version 450` and the `GL_EXT_spirv_intrinsics` line.** sokol-shdc
   emits its own per-backend `#version`, and errors on a second one.
2. **Drop the `out gl_PerVertex { ... };` block.** spirv-cross emits it to declare
   `gl_Position`'s block; glslang declares it implicitly and rejects the
   redeclaration.
3. **`layout(set = N, binding = M)` -> `layout(binding = M)`.** sokol_gfx has one
   descriptor set.
4. **Rename varyings to match.** sokol-shdc links stages by varying *name*, not by
   location. spirv-cross calls the two sides `out_var_X` / `in_var_X`, which never
   match; both become `vX`.
5. **Split the uniform block per stage.** sokol-shdc rejects one block name used
   from both `@vs` and `@fs` ("conflicting uniform block definitions"), and then
   rejects the two resulting blocks sharing a binding. The fragment copy is
   renamed and moved to binding 1.

Two `spirv-cross` flags matter and are not optional:

- `-V` (Vulkan GLSL) is what produces the separate `uniform texture2D` +
  `uniform sampler` declarations sokol-shdc wants. Plain GLSL output emits a
  combined `sampler2D` and is rejected.
- `--relax-nan-checks` keeps spirv-cross from modelling `NMin`/`NMax` via
  `GL_EXT_spirv_intrinsics`. sokol-shdc's glslang does not accept
  `spirv_instruction()`, and without this flag the vertex shader fails to parse.

## Results

| shader | result |
| --- | --- |
| `apos-shapes.fx` | **OK** — all 5 backends generated |
| `Sky.fx` | OK |
| `Particle.fx` | OK |
| `Ground.fx`, `Mountains.fx` | OK **after** the `Mad.fxh` migration below |
| `Poly.fx`, `Line.fx` | blocked by `Mad.fxh` |
| `Nvg.fx` | 2 of 4 techniques OK; the other 2 are blocked by `tex2D` |
| `ImGui.fx` | blocked by `tex2D` |

## The one real blocker: D3D9 idioms `dxc` rejects

`dxc` is a shader-model-6 compiler and refuses three things used throughout the
existing shaders. None is a translation-pipeline problem; each is a source edit.

**1. `tex2D(sampler, uv)` is a hard error**, not a warning:
> `error: deprecated tex2D intrinsic function will not be supported`

The modern form needs a `Texture2D`/`SamplerState` pair. `apos-shapes.fx` already
does exactly this behind its `#if SM6` branch, with a comment saying why — that
branch is the template for the rest.

**2. Samplers cannot be passed as function parameters.** `Mad.fxh`'s
`applyShadowingSingle` takes `in sampler shadowMapSampler`, which SM6 has no
equivalent for; the texture and sampler must be passed separately (or the call
inlined). This is the only place in the test set where an actual signature change
is needed.

**3. `texture`/`sampler_state` declarations** are ignored as deprecated effect
syntax, so the associated sampler state never reaches SPIR-V. Harmless here — the
sampler state is set from C# anyway — but it means those declarations are dead
weight under this path.

Migration for `Mad.fxh` was prototyped and works: three `texture`+`sampler_state`
pairs become `Texture2D`/`SamplerState` on matching registers, `tex2D` becomes
`.Sample()`, and `applyShadowingSingle` takes the texture and sampler separately.
`Ground.fx` then goes through the whole pipeline.

## Caveats before productionising

- **The generated header is ~20 MB** for `apos-shapes.fx`, dominated by the
  fragment shader duplicated across 5 backends (686 KB of GLSL alone — the
  unrolled shape ladder is enormous). Fine on desktop, likely disqualifying for
  web. Trimming `-l` to the backends actually shipped cuts this proportionally.
- **Matrix convention needs a runtime check.** HLSL `mul(v, M)` becomes
  `v * mat4(...)` in the generated GLSL, with the block member declared
  `layout(row_major)`. That *should* round-trip, but it was not verified against
  a rendered frame and is the classic place this kind of pipeline silently
  transposes. Verify before trusting it.
- **Reflection is per-stage here, not shared.** Splitting the uniform block means
  the generated `type_Globals_t` and `fs_Globals_t` are two structs with identical
  fields and two separate `sg_apply_uniforms` calls. sokol-shdc's `@include_block`
  is the intended mechanism and would merge them, but a block pulled into both
  stages then needs the varying-name dance above to be rethought.
- **The `Nvg` false positive is a warning about this whole exercise**: techniques
  whose entry point never reaches the sampling code compile fine and report no
  samplers at all. Checking that a shader *compiles* is not checking that it
  *works*. Whatever driver this grows into needs to check reflected bindings
  against what the C# side expects, per technique — not just exit status.

## Recommendation

The translation path is proven and the bridge is small. The real work is a
one-time migration of the in-house HLSL off `tex2D` and the legacy sampler
syntax, in the same shape `apos-shapes.fx` already uses. `Mad.fxh` is the only
file needing a signature change. After that, `run.sh` generalises to a build step.

---

# Addendum: the glslang HLSL front-end (no DXC)

Follow-up spike, prompted by "can we port sokol-shdc to C#". The short version:
**DXC can be removed from the pipeline entirely.** glslang — the same compiler
sokol-shdc already links for its GLSL front-end, and which shaderc
(`Silk.NET.Shaderc`, full RID coverage incl. macOS) exposes to .NET — has its own
HLSL front-end, and it accepts the shaders in this repo.

    shader.fx
      |  glslang -V -D -S <stage> -e <entry> -DSM6=1     <- HLSL front-end, in-process
      v
      vs.spv / fs.spv
      |  spirv-cross -V --version 450 --relax-nan-checks
      v
      Vulkan GLSL  ->  bridge.mjs  ->  sokol-shdc

`run_hlsl.sh` drives it. Same 5 rewrites in `bridge.mjs`, no DXC, no separate
SPIR-V optimizer (glslang links SPIRV-Tools itself).

## Why this matters more than a flag change

The previous pipeline needed DXC, which **has no macOS binaries** — Microsoft
publishes Windows and Linux only, `Vortice.Dxc.Native` ships win-x64/win-arm64/
linux-x64, and the only macOS `.dylib` in existence is from an unofficial ~2-year
old Zig fork on a package with ~5k downloads. Since this repo targets
`osx-x64;osx-arm64;win-x64;win-arm64;linux-x64;linux-arm64`, in-process DXC was a
dead end. glslang/shaderc ships for **all** of those.

It also collapses the port: sokol-shdc's front-end wiring (`spirv.cc`, glslang
init + `GlslangToSpv`) is replaced by a call into shaderc, and the annotated-GLSL
parser in `input.cc` is replaced by an HLSL front-end. SPIRV-Cross is used through
the C API, which covers everything `reflection.cc` and `spirvcross.cc` need —
including `spvc_compiler_variable_is_depth_or_compare`, the C-API equivalent of the
`UnprotectedCompiler` protected-member trick, and
`spvc_compiler_get_binary_offset_for_decoration` for the WGSL bind-slot patching.

## Results with the HLSL front-end

| shader | result |
| --- | --- |
| `Sky.fx` | OK |
| `Particle.fx` | OK |
| `Ground.fx`, `Mountains.fx` | OK after the `Mad.fxh` migration |
| `ImGui.fx` | OK after migration |
| `apos-shapes.fx` | **OK** — all 13 attributes, 2 blocks, 6 textures, 6 samplers reflected |
| `Nvg.fx` | 3 of 4 techniques OK; `PSMainSimple` needs a dead varying removed |
| `Poly.fx`, `Line.fx` | OK after migration + the two fixes below |

## What the HLSL front-end changes about the migration

Two things are *easier* than the DXC path, two are *new*.

**Easier:**
- **`tex2D(SamplerState, uv)` works.** dxc rejects `tex2D` outright; glslang's HLSL
  front-end accepts it against a modern `Texture2D`/`SamplerState` pair. So the
  `tex2D` → `.Sample()` change is cosmetic, not required.
- **Legacy `sampler_state` is rejected outright**, which is arguably better: you
  find out at compile time instead of silently getting no sampler state (the
  "dead weight" note above). ImGui went through after a mechanical rewrite.

**New, and they are one-liners in the source:**
- **`SV_Position0` (trailing digit) is silently not recognised.** It is compiled as
  an ordinary varying, the vertex shader gets no `gl_Position`, and sokol-shdc
  fails with "a vertex shader must include the 'position' builtin". This is the
  single hardest thing to debug here because nothing errors at the compile step.
  `apos-shapes.fx:95` has it; so does the `#define SV_POSITION POSITION` idiom in
  `Poly.fx`/`Line.fx` (fix: `POSITION` → `SV_Position`).
- **`bool` in a uniform block becomes `uint`**, and sokol-shdc rejects uniform
  blocks containing anything but float/int: "uniform blocks can only contain float
  or int base types". `Poly.fx`/`Line.fx` declare `bool IsFullbright` etc.; making
  them `float` is enough.

Also worth knowing: **`-DSM6` (no value) is wrong for glslang.** `-D` is glslang's
HLSL flag, so a bare `-DSM6` defines `SM6` to the empty string and every
`#elif SM6` fails with "bad expression" / "missing #endif". Use `-DSM6=1`. This
cost real debugging time and looks like a broken preprocessor.

## The one remaining gap: WGSL needs Tint

SPIRV-Cross has no WGSL backend. sokol-shdc gets WGSL from **Tint** (Google's Dawn
compiler, `spirvcross.cc:14 #include "tint/tint.h"`), which is a large C++
dependency with no .NET binding. If WGSL is in scope, a C# port would need its own
Tint binding or a naga-based one.

**It probably isn't in scope**: the only `browser-wasm` targets in this tree are
`Sokol.NET/examples/*`, not the game. Dropping `wgsl` from the `-l` list removes
Tint entirely and was verified to work — it cuts the generated header for
apos-shapes from 14.6 MB to 12.2 MB.

## Matrix convention: verified this time

The earlier caveat is resolved. HLSL `mul(v, M)` round-trips as
`layout(row_major) mat4 M` in the block and `M * v` in the body — row-major is
carried through as a layout qualifier, so the multiply order is correct without a
transpose. (Still worth one rendered-frame check per backend, but the qualifier is
present and correct rather than silently dropped.)

---

# Addendum 2: "compile for all platforms on all platforms"

This was the actual goal, and it is achievable **without any platform-specific
compiler** — including without DXC. The reason is that sokol_gfx accepts *source*
for every backend it has:

| backend | what sokol_gfx takes | who compiles it | build-time compiler needed |
| --- | --- | --- | --- |
| `GLCORE` / `GLES3` | glsl430 / glsl300es source | the GL driver, at runtime | none |
| `D3D11` | hlsl5 **source** | `d3dcompiler_47.dll`, loaded on demand | **none** |
| `METAL_MACOS` / `METAL_IOS` | MSL source | the Metal runtime | none |
| `WEBGPU` | WGSL source | the browser | none |

sokol_gfx.h is explicit about this: *"for the D3D11 backend, shaders can be
provided as source or binary blobs ... when shader source code is provided for the
D3D11 backend, sokol-gfx will dynamically load 'd3dcompiler_47.dll'"*, and *"for
D3D11 and Metal, either shader source-code or byte-code can be provided."*

sokol-shdc's `-b` flag, which is what invokes `fxc`/`dxc` on Windows and
`xcrun metal` on macOS, is **opt-in** and only pre-compiles that source to
bytecode. Nothing in the default path needs it.

The generated header proves this: `desc.vertex_func.source = vs_source_hlsl5` plus
`desc.vertex_func.d3d11_target = "vs_5_0"` — source plus a target string, handed to
the runtime compiler. Verified that this HLSL5 is valid: `fxc /T vs_5_0 /E main`
and `/T ps_5_0` both compiled it (the fragment shader emits only X3570
gradient-in-loop warnings).

**The whole build-time pipeline is therefore portable**, because every stage of it
is SPIRV-Cross, which has a complete C API and ships for every RID:

    HLSL .fx
      |  shaderc/glslang  (HLSL front-end)         all RIDs
      v  SPIR-V
      |  SPIRV-Cross       (reflection + GLSL/HLSL/MSL)   all RIDs
      v  source for each backend  ->  one header, works everywhere

`glslang` and `SPIRV-Cross` both ship for win-x64/arm64, linux-x64/arm64, and
osx-x64/arm64. A build tool using them can run on any of those and emit shaders for
all of them. DXC is only needed if you insist on pre-compiling D3D11 bytecode at
build time, which is an optimisation, not a requirement.

## The one genuine exception: WGSL

WGSL is not produced by SPIRV-Cross; sokol-shdc gets it from **Tint** (Google's
Dawn), which has no .NET binding. If web is in scope, that is the single piece that
is not portable through this route. Otherwise drop `wgsl` from the backend list and
everything above holds.

## On DXC and macOS

For completeness, since it came up: `Silk.NET.Direct3D.Compilers` **2.23.0 ships no
native binaries at all** — the package is `lib/` only (four managed DLLs, ~8.9 MB),
no `runtimes/` directory, and no `.targets`/`.props` to fetch one. Its library-name
container does define `libdxcompiler.dylib` for macOS, so the *bindings* would load
one, but nothing in the package supplies it and no `Silk.NET.Direct3D.Compilers.Native`
exists on NuGet (404).

The only package that actually ships macOS DXC is **`DirectXShaderCompiler.NET`**
1.3.3 (`runtimes/osx-x64/native/libdxcompiler.dylib` and `osx-arm64`, ~34 MB each) —
the unofficial Zig fork, last published 2024-10-14, ~5k downloads. It also ships
linux-x64/arm64 and win-x64/arm64. Viable if the bytecode path is ever wanted, but
it is stale and unofficial, and per the table above it is not needed.

---

# Addendum 3: the Vulkan SDK does ship DXC everywhere

Correcting Addendum 2's conclusion that macOS DXC "does not exist". It does — it
ships inside the Vulkan SDK, which is why no NuGet package needed to supply it.

Verified on the SDK installed here (1.4.341.1, Windows):

    Bin/dxc.exe            1.0 MB
    Bin/dxcompiler.dll    21.3 MB
    Lib/dxcompiler.lib      import library
    Bin/dxc.exe --version -> dxcompiler.dll 1.10(5180-e3554182) / 1.9.0.5180

and it works for SPIR-V: `dxc -T vs_6_0 -E SpriteVertexShader -spirv
-fvk-use-dx-layout -Zpc -DSM6` on apos-shapes produced a valid `.spv`.

## Why macOS is covered too

LunarG publishes **one build recipe for all three platforms** — the
`config.json` at `sdk.lunarg.com/sdk/download/latest/{windows,linux,mac}/`
is byte-identical for all three (same md5), and its `repos` list is what the SDK
builder consumes:

    DXC    "platforms": "(all)"        <- not platform-restricted
    ...
    MoltenVK        ["Darwin"]         <- only these two are restricted
    KosmicKrisp     ["Darwin"]
    mimalloc        ["Windows"]

DXC is `"(all)"`, pulled from `github.com/microsoft/DirectXShaderCompiler.git` at a
pinned commit and built locally. Only MoltenVK, KosmicKrisp and mimalloc carry a
platform restriction. So the macOS SDK builds DXC from source, and the LunarG docs
state it directly: *"The DXC Shader Compiler is also available as a shared library
on 64-bit desktop operating systems (macOS, Windows, and Ubuntu). The headers and
an import library for the DLL are included in the SDK."*

That messaging is what "supposedly the Vulkan SDK comes with it" was remembering,
and it is correct.

## What this changes (and what it doesn't)

It removes the last argument for DXC being a blocker — but it does **not** make DXC
the right choice, for two reasons that still hold:

1. **It ties the build to a Vulkan SDK install.** Depending on `dxcompiler.dll`
   from the SDK means the build machine must have the SDK, at a version whose ABI
   matches. The `DirectXShaderCompiler.NET` package exists precisely to avoid that
   by vendoring the `.dylib`/`.so`, and `Vortice.Dxc.Native` does it for
   win/linux. Shipping your own copy per-RID is the normal answer.
2. **It is still not needed.** Per Addendum 2, sokol_gfx compiles source at
   runtime for D3D11 (`d3dcompiler_47.dll`, loaded on demand) and for Metal, and
   glslang's HLSL front-end already produces the SPIR-V the pipeline wants.
   DXC only enters if you opt into pre-compiled D3D11 bytecode via `-b`.

The real remaining constraint is unchanged and has nothing to do with DXC: the
in-house HLSL still uses D3D9 idioms that must be migrated, and WGSL still needs
Tint if web is in scope.

---

# Addendum 4: is runtime D3D11 compilation viable?

Short answer: **yes, and the earlier "vs_5_0 cuts it" premise is the wrong lever.**

## SM5 is not droppable — but it is also not a problem

`hlsl4` vs `hlsl5` only selects the *target string* and the `hlsl_target()`
profile handed to the compiler (`generator.cc:369`):

    HLSL4 -> d3d11_target = "vs_4_0" / "ps_4_0"
    HLSL5 -> d3d11_target = "vs_5_0" / "ps_5_0"

The HLSL **source** sokol-shdc emits is the same either way, and it is valid SM4 —
verified by scanning the generated `vs_source_hlsl5` / `fs_source_hlsl5` for
SM6-only constructs (`StructuredBuffer`, `WaveActive*`, `groupshared`, `[[vk::]]`,
`numthreads`): **zero found**. It uses only `cbuffer`/`packoffset`, `register(bN)`,
`Texture2D`/`SamplerState` on `register(tN)`/`register(sN)`, `column_major`, and
`SV_Position`/`SV_Target`. The same text compiles under `vs_4_0`, so the target
string is the only difference and choosing one does not constrain anything.

The reason SM5 is not droppable is that the SDK does not document the actual
compatibility matrix. sokol_gfx's own comments say *"HLSL4.0 (for compatibility
with old low-end GPUs) or preferably HLSL5.0"*, so `hlsl5` is the recommended
default and there is no documented reason to prefer `hlsl4`.

## The cost is fxc's optimizer, and it is concentrated in one shader

Measured by extracting the generated HLSL5 from an `-l hlsl5` header and running
`fxc` at the same flags sokol_gfx uses in release
(`D3DCOMPILE_OPTIMIZATION_LEVEL3` → `/O3`), one run per stage:

| shader | VS size | VS | FS size | FS |
| --- | --- | --- | --- | --- |
| Sky | 821 B | 166 ms | 475 B | 167 ms |
| Particle | 1057 B | 163 ms | 475 B | 174 ms |
| Ground | 1936 B | 183 ms | 8559 B | 183 ms |
| Mountains | 2065 B | 162 ms | 8259 B | 186 ms |
| ImGui | 1144 B | 178 ms | 704 B | 184 ms |
| Nvg | 1705 B | 176 ms | 2720 B | 172 ms |
| Poly | 7511 B | 174 ms | 10613 B | 192 ms |
| Line | 11448 B | 194 ms | 12208 B | 187 ms |
| **apos-shapes** | 8230 B | 177 ms | **625597 B** | **7656 ms** |

Everything is ~170–190 ms — dominated by fxc's fixed startup, not the shader.
**apos-shapes' fragment shader is the sole outlier at 7.7 s**, and it is 625 KB
because the shape ladder is enormous, not because it is unrolled (the emitted HLSL
still has its 18 loops; the source has 3, the rest is spirv-cross's full inlining).

`/Od` — which is what `desc.d3d11_shader_debugging` selects — takes that 7.7 s down
to **1.8 s**. `/O1` does not help (7.7 s); it is specifically the aggressive
optimizer that is slow.

## What this means

A D3D11-only startup cost, paid synchronously inside `sg_make_shader`
(confirmed: `_sg_init_shader` runs in `sg_make_shader`, so it blocks the calling
thread). Roughly **8 s of the total is apos-shapes' fragment shader**; everything
else is under 400 ms.

Three ways to avoid it, in order of preference:

1. **Precompile just the D3D11 bytecode**, which is what `-b` is for. This needs
   `fxc` — but `fxc.exe` is a thin wrapper over `d3dcompiler_47.dll`, the *same*
   library sokol_gfx loads at runtime, so anywhere the game runs it can compile.
   On Windows the SDK ships both. (Note `dxc` cannot substitute: given `-T vs_5_0`
   it warns *"Promoting older shader model profile to 6.0 version"* and emits SM6,
   so it cannot produce SM5 bytecode.)
2. **Ship apos-shapes as precompiled bytecode and the rest as source** — the
   descriptor is per-shader, so this mixes freely. That gets startup to ~2 s with
   no toolchain work beyond one shader.
3. **Accept ~8 s on D3D11 startup** on the machine that first creates the pipeline.

None of these involve DXC at build time, and none are platform-blocking: the
build-time path stays glslang + SPIRV-Cross, which covers every RID.
