---
name: net11-browser-wasm-project-setup
description: "The three non-obvious MSBuild settings a net11.0 browser-wasm app needs in this SDK, and the workload gap that hides _framework."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-26T16:19:58.517Z
---

For a `net11.0` + `browser-wasm` executable in this repo (SDK 11.0.100-rc.3), three
settings are mandatory and none is implied:

1. **`Sdk="Microsoft.NET.Sdk.WebAssembly"`**, not `Microsoft.NET.Sdk`. Plain SDK leaves
   `SelfContained`/`UseMonoRuntime` unset, so no runtime pack resolves and the build dies
   in `WasmApp.Common.targets` with `MicrosoftNetCoreAppRuntimePackDir=''`.
2. **`WasmCachePath`** must be writable and on the **same drive as the SDK**. The default
   is under `Program Files` (emscripten port fetches hit WinError 5), and another drive
   fails with `ValueError: path is on mount 'C:', start on mount 'W:'`. Use
   `$(LOCALAPPDATA)\...`.
3. **`PackageReference Microsoft.NET.Sdk.WebAssembly.Pack`** — this SDK's
   `dotnet workload install wasm-tools` installs the Mono/Emscripten packs but not this
   one, so `_framework/` and the boot manifest are never generated. `dotnet workload
   repair` currently fails on an unrelated VS-installed Android MSI.

Emscripten's `-sUSE_SDL=3` port (SDL 3.4.2) ships **plain wasm objects**, while the .NET
native build is LLVM bitcode; mixing them means LTO silently drops every symbol nothing
references. Build the C library to bitcode too. See
[[wasm-pinvoke-table-and-gl-proc-addresses]].

**How to apply:** start any browser-wasm project in this repo from these four settings,
not from a plain csproj — each failure mode is a bare error with no cause named.
