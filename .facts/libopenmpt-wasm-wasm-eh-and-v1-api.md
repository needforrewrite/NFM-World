---
name: libopenmpt-wasm-wasm-eh-and-v1-api
description: "The browser build links libopenmpt built with -fwasm-exceptions; a legacy archive crashes wasm-ld. TrackerDecoder uses the v1 create API, which works under either model."
metadata:
  node_type: memory
  type: project
  modified: 2026-11-23T00:00:00.000Z
---

`libopenmpt` in the NFM-World browser build (`NFMWorld.Platform.WebGL/native/libopenmpt.a`,
linked into the CoreCLR wasm module) must be built with **wasm exception handling**, because the
CoreCLR head requires it: `BrowserWasmApp.CoreCLR.targets` errors on
`WasmEnableExceptionHandling != true` and links `-fwasm-exceptions -sWASM_LEGACY_EXCEPTIONS=0`.

- **The archive is not checked in.** `NFMWorld.Platform.WebGL/native/build-libopenmpt.sh` builds
  it; CI runs it in an Ubuntu job and passes the artifact to the Windows build. It takes its
  compiler from the `wasm-tools` workload's Emscripten pack, not a system emsdk, and asserts the
  result is wasm-EH (`__gxx_wasm_personality_v0 > 0`, `__gxx_personality_v0 == 0`).
- **Upstream's config asks for the legacy model** (`-s DISABLE_EXCEPTION_CATCHING=0` in every flag
  group), which cannot be combined with `-fwasm-exceptions`; the script rewrites the setting in
  `build/make/config-emscripten.mk` rather than layering an `EMCC_CFLAGS` override, since the
  config's own flags win.
- **A legacy archive is not a link error — it crashes `wasm-ld`** (Exception Code
  `0x80000003`) in `WebAssembly Instruction Selection` on the first function with a landing pad
  (`openmpt_could_open_probability2`, then `GlobalLogger::SendLogMessage`). Legacy linking of the
  same archive succeeds, so the crash is EH-model specific and easy to misread as a compiler bug.
- **`TrackerDecoder` uses `openmpt_module_create_from_memory` (v1), not v2.** v2 also works here
  (measured against the wasm-EH archive: `error=0`, correct duration), but it only adds an error
  code and message nothing reads, so v1 is used everywhere with no platform branch.

**How to apply:** if a tracker module fails, first confirm the linked archive is the wasm-EH
build (`strings native/libopenmpt.a | grep __gxx_wasm_personality_v0`). A `wasm-ld` crash during
`WebAssembly Instruction Selection` means a stale legacy archive. Related:
[[wasm-pinvoke-table-and-gl-proc-addresses]].
