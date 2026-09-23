# ShaderCrossCheck

Spike: can one HLSL `.fx` source drive both backends?

    HLSL (.fx)  ──ShadowDusk / MGCB──►  .fxb / .mgfx   (FNA3D + MojoShader)
                └──dxc ──► SPIR-V ──► spirv-cross ──► GLSL ──► sokol-shdc ──► sokol_gfx

`run.sh` drives the second path end to end and prints the sokol-shdc reflection
summary. See `FINDINGS.md` for results and the list of transformations required
between spirv-cross and sokol-shdc.

## Usage

```sh
./run.sh path/to/shader.fx SpriteVertexShader SpritePixelShader
```

Defaults to `Apos.Shapes/Source/Content/apos-shapes.fx` and its two entry points.

## Dependencies

On PATH, or override via environment:

| var           | default                                   |
| ------------- | ----------------------------------------- |
| `DXC`         | `dxc.exe`                                 |
| `SPIRV_CROSS` | `spirv-cross`                             |
| `SOKOL_SHDC`  | `tools/win32/sokol-shdc.exe` in this repo |

`dxc` + `spirv-cross` ship with a Vulkan SDK. `sokol-shdc` is vendored in this
repository under `tools/<platform>/`.

## Two front-ends

`run.sh` uses DXC (needs a Vulkan SDK; Windows/Linux only). `run_hlsl.sh` uses
glslang's HLSL front-end instead — no DXC, works on every RID the game targets,
and is the one relevant to a C# port. See the addendum in `FINDINGS.md`.

`run_hlsl.sh` also needs the legacy `texture`/`sampler_state` declarations gone and
does not tolerate `SV_Position0`, `bool` in a uniform block, or `#define
SV_POSITION POSITION`. The migration is mechanical; `Mad.fxh` is the worst of it.
