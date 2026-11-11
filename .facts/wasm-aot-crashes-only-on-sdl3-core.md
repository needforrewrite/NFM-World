---
name: wasm-aot-crashes-only-on-sdl3-core
description: "mono-aot-cross in the .NET 11 RC.3 wasm toolchain crashes on exactly one assembly (SDL3.Core.dll); every other assembly AOTs fine, and the SDK's documented skip hook is dead code."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-27T14:35:23.875Z
---

`RunAOTCompilation=true` for `browser-wasm` on .NET 11 RC.3 fails with
`Assertion at sgen-alloc.c:409, condition '*p == NULL' not met` (exit -1073740791). Measured
2026-10-27 by AOTing each of the 109 bundled assemblies individually with the pack's
`mono-aot-cross.exe`: **108 succeed, only `SDL3.Core.dll` crashes.** It crashes under *every*
LLVM option set (with/without `no-opt`, `static`, `direct-icalls`, `interp`, determinism, dwarf
debug) while the same file's **interpreter-only** path (`--aot=...,interp,asmonly`, no `--llvm`)
succeeds. So it is the LLVM backend on that one assembly, not the flags and not our code.

The older note that it "crashes on aot-instances.dll and reports invalid IL in Lua-CSharp" no
longer holds, and neither workaround is needed: `aot-instances.dll` (the generated dedup assembly)
AOTs fine on its own — so `WasmDedup` can stay at its default `true` — and `Lua.dll` AOTs clean.
Leaving dedup on matters: it cuts the linked module from 91.6 MB to 53.2 MB (14.5 MB → 9.6 MB
brotli), and it only ever appeared to crash because the AOT task failed on SDL3 in the same run.

**The SDK's skip hook does not work.** `_AOT_InternalForceInterpretAssemblies`
(`WasmApp.Common.targets:654`) sets `AOT_InternalForceToInterpret` with a Condition that tests
whether the *list* is non-empty, not whether the current assembly is in it — populating it would
mark every assembly to skip. Worse, `MonoAOTCompiler.dll` never reads that metadata (grep finds
0 hits), so even the correctly-set version is inert.

**Working fix** (in `NFMWorld.Browser/NFMWorld.Browser.csproj`): a target `BeforeTargets=
"_WasmAotCompileApp"` that moves the matching item out of `_WasmAssembliesInternal` into a
holding item, and one `AfterTargets="_WasmAotCompileApp"` that puts it back, so it is bundled as
IL for the interpreter. Do not use `only-llvmonly` or `method=` as `--aot=` options — both are
"Unknown argument" in this build; valid ones include `llvmonly`, `interp`, `asmonly`,
`llvm-path=`, `llvm-outfile=`, `temp-path=`.

Reproduce one assembly directly (fast, ~1 s) instead of a full publish:
`MONO_PATH=<linked-dir>;<runtime-pack native>;<runtime-pack lib/net11.0>` then
`mono-aot-cross.exe --llvm "--aot=no-opt,static,direct-icalls,deterministic,nodebug,llvmonly,interp,asmonly,llvm-path=<emsdk tools/bin/>,llvm-outfile=<x>.bc,temp-path=<dir>" SDL3.Core.dll`.
Harnesses: `scratch/aot-scan.sh`, `scratch/aot-opts.sh`.

See [[net11-browser-wasm-project-setup]], [[wasm-msbuild-emscripten-toolchain]].
