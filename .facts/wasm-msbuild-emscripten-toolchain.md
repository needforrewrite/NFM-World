---
name: wasm-msbuild-emscripten-toolchain
description: "How to compile C to LLVM bitcode from inside an MSBuild target for a net11 browser-wasm project: which properties hold the toolchain, and the two traps."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-26T18:26:00.132Z
---

A project that must ship its own C to wasm (e.g. `NFMWorld.Platform.WebGL`) compiles it in an
MSBuild `Exec` and links it via `<NativeFileReference>`. Four specifics, all verified:

1. **Toolchain properties** (from the wasm SDK's `EmSdkRepo.Defaults.props`, all derived from
   the workload pack): `EmscriptenSdkToolsPath` is the *pack* dir, `EmscriptenUpstreamEmscriptenPath`
   is `<pack>/emscripten/` (where `emcc.bat` is), `EmscriptenUpstreamBinPath` is `<pack>/bin/`
   (`llvm-ar`), `EmscriptenPythonToolsPath` is `<pack>/python/`. Compiling with anything other
   than `emcc.bat` from `EmscriptenUpstreamEmscriptenPath` risks a bitcode/link mismatch.
2. **Required environment variables.** The workload's `emscripten/.emscripten` reads
   `DOTNET_EMSCRIPTEN_LLVM_ROOT`, `DOTNET_EMSCRIPTEN_NODE_JS` and
   `DOTNET_EMSCRIPTEN_BINARYEN_ROOT`; without them emcc fails with "LLVM_ROOT is set to empty
   value". The wasm SDK sets them only around its own Exec calls, so a new target must repeat
   them, plus `EM_CACHE` (must be writable *and* on the SDK's drive), `EMSDK`, `EMSDK_PYTHON`,
   `PYTHONPATH`, `PYTHONHOME=`.
3. **`IntermediateOutputPath` is empty at evaluation time.** Paths computed in a top-level
   `<PropertyGroup>` from it put outputs in the project directory instead of `obj/`. Use
   `BaseIntermediateOutputPath` (`obj/`), or compute inside the target.
4. **The archive name is the P/Invoke key.** The generated P/Invoke table is keyed by the
   native file's base name, so the `.a` must be named exactly what the `[DllImport]` strings
   say. Always `-emit-llvm`: the .NET native build links with LTO and silently drops symbols
   from plain wasm objects.

**How to apply:** copy the `BuildWebGlShim` target in `NFMWorld.Platform.WebGL.csproj` rather
than writing one from scratch - each of the four points above fails with a message that names
something other than the cause. Related: [[wasm-pinvoke-table-and-gl-proc-addresses]],
[[net11-browser-wasm-project-setup]].
