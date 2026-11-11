---
name: hexa-imgui-on-wasm
description: "Why Hexa.NET.ImGui cannot run on wasm as shipped, and the two non-obvious requirements of the DllImport rebuild that fixes it."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-26T23:39:07.119Z
---

Hexa.NET.ImGui's generated binding calls every native function through a **function table**
(`((delegate* unmanaged[Cdecl]<...>)funcTable[i])(...)`), populated by
`NativeLibrary.TryGetExport` on a handle from `HexaGen.Runtime.LibraryLoader`. On Mono wasm that
whole route is dead: `NativeLibrary.GetMainProgramHandle()` throws `Win32Exception: Success`, and
`TryGetExport` returns false for every handle (loaded or fabricated) — wasm has no callable
addresses outside managed trampolines. Neither `InterceptLibraryLoad` nor
`InterceptNativeContext` helps, because the binding's static ctor calls `LoadLibrary`, never
`LoadLibraryEx`.

A plain `[DllImport("cimgui")]` **does** work, resolving through the generated P/Invoke table, as
long as the resolver returns any non-zero handle (returning `IntPtr.Zero` fails the load; the
table is keyed by the archive's base name, which must be `cimgui.a`). The working fix is
`NFMWorld.ImGui.Browser`: HexaGen's generator at tag `2.2.9` with
`"ImportType": "DllImport"` in `Generator/config.base.json`, producing the same assembly name
and API.

Two traps when building that:

1. **`CIMGUI_VARGS0`.** The binding calls the offline `igText0`/`igSetTooltip0`/... forms, which
   `cimgui.h` only emits under `-DCIMGUI_VARGS0`. Without it the link fails on those symbols —
   and the objects are not rebuilt when only the flag changes unless the build stamps its flags.
2. **No `[assembly: DisableRuntimeMarshalling]`.** The generated binding declares a few
   parameters as managed delegates (`ImGuiSizeCallback`, the Combo/ListBox getters); with runtime
   marshalling disabled those P/Invokes throw "Cannot marshal managed types" at the call.

Related: [[wasm-pinvoke-table-and-gl-proc-addresses]], [[wasm-runtime-async-and-rid-propagation]].
