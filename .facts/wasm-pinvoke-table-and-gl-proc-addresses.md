---
name: wasm-pinvoke-table-and-gl-proc-addresses
description: "On .NET browser-wasm: P/Invoke resolves through a static table keyed by file name; calli needs managed trampolines; emscripten's gl* live in JS glue."
metadata:
  node_type: memory
  type: project
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-26T18:14:07.735Z
---

Four hard-won facts from the milestone-1 GL probes (`scratch/glprobe`, `scratch/glspike`;
findings in their `FINDINGS.md`), all verified in headless Edge with AOT on. Each fails with
no useful error message.

**1. Mono WASM does not `dlopen`.** P/Invokes resolve through a generated static table
(`pinvoke-table.h`) whose module key is the **native archive's base file name**. The
`[DllImport("SDL3")]` in `FNA/lib/SDL3-CS` only resolves if the archive is `SDL3.a` — as
`libSDL3.a` the table gets an empty entry and the first call throws a bare
`ManagedError("SDL3")`. `NativeLibrary.SetDllImportResolver` (including the
`GetMainProgramHandle()` arm `WorldGame.ImportResolver` already has for `IsBrowser()`) is
**inert**; the table wins. The table also only lists symbols it could *see*, so a symbol
missing from the archive fails at first call, not at link.

**2. `calli` only works on `[UnmanagedCallersOnly]` addresses.** Calling a wasm function
through `calli` — even a plain module export obtained via `NativeLibrary.GetExport` — throws
`Win32Exception: Success` and can abort the runtime (`aot-runtime-wasm.c:188`). Enabling
`RunAOTCompilation` does not change this. So Silk.NET's dispatch must get the address of a
managed `[UnmanagedCallersOnly]` wrapper whose signature matches the native one and which
forwards to a `[DllImport]`.

**3. Emscripten's proc addresses are unusable.** `SDL_GL_GetProcAddress("glClearColor")`
returns `0x4E9` — a wasm function-**table index**, not a pointer. `GL.GetApi(SDL_GL_GetProcAddress)`
constructs fine and then crashes on the first call.

**4. `gl*` is JS glue on a non-pthread build.** `system/lib/gl/webgl1.c` defines the GL entry
points only under `__EMSCRIPTEN_PTHREADS__ && __EMSCRIPTEN_OFFSCREEN_FRAMEBUFFER__`. Without
pthreads they are JS imports (`emscripten_glClearColor`), and the sysroot's `libGL.a` has
almost no `gl*` symbols. C# therefore needs a tiny C shim whose wrappers forward to them —
that shim is what makes `[DllImport]` possible, and a full triangle (shaders, VAO/VBO,
`glDrawArrays`, `glReadPixels`) then renders correctly.

**Verified working route:** C shim → `[UnmanagedCallersOnly]` trampoline → browser-only
`INativeContext` → `GL.GetApi`. `GL.GetApi(INativeContext)` exists in Silk.NET.Core 2.23.0,
so `GlGraphicsDevice.Create(Func<string,nint>)` can stay for desktop.

**5. Structs marshal fine — the prior art's warning does not apply to plain P/Invoke.**
`[StructLayout(LayoutKind.Sequential)]` structs pass by value and return exactly, at every
ABI size class: 8-byte int and float pairs, 12- and 16-byte float vectors, and a 64-byte
16-float matrix (which exercises the hidden-pointer return), in both directions. So Far
`ImGui` and descriptor structs are ordinary binding work, not a runtime limitation. Structs
**containing pointers** were not tested. (`calli` itself remains unsupported, so
struct-taking calls must go through `[DllImport]`, not a proc address.)

**Why:** these are silent-at-build, fatal-at-runtime failures with misleading messages; each
cost a full build/deploy/browser cycle to isolate.

**How to apply:** for any native library in the browser build, compile it to **LLVM
bitcode** (`-emit-llvm`, matching the .NET native build) and name the archive exactly as the
`[DllImport]` string. For GL, plan on the C-shim + trampoline `INativeContext` route.
Related: [[net11-browser-wasm-project-setup]], [[sokol-backend-bundle-shaders-need-hlsl]].
