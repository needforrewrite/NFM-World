---
name: libopenmpt-wasm-legacy-exceptions-and-v1-api
description: "In the browser build, libopenmpt must use the legacy JS exception model and its v1 create API; v2 and -fwasm-exceptions each fail silently."
metadata:
  node_type: memory
  type: project
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-27T12:34:40.482Z
---

`libopenmpt` in the NFM-World browser build (`NFMWorld.Platform.WebGL/native/libopenmpt.a`,
linked into the Mono wasm module) only works under two specific conditions. Both failures are
silent — the page just exits, with no message naming libopenmpt.

The archive is **not checked in**: `NFMWorld.Platform.WebGL/native/build-libopenmpt.sh` builds it
(CI runs it in an Ubuntu job and passes the artifact to the Windows build). It takes its compiler
from the `wasm-tools` workload's Emscripten pack, not a system emsdk, and it fails loudly if the
result is not the legacy-exception build.

1. **Exception model must be legacy.** The head sets `WasmEnableExceptionHandling=false`, which
   links with `-fexceptions` / `-sDISABLE_EXCEPTION_CATCHING=0`. Do not switch it on:
   Emscripten 6.0.2 emits a module the browser refuses to instantiate (`invalid value type 'rtt'`,
   a GC-proposal type), and linking a `-fwasm-exceptions` archive crashes `wasm-ld` (SIGTRAP).
   The archive must be built with upstream's unmodified config so both sides agree.

2. **`openmpt_module_create_from_memory2` aborts** (the module dies with `[EXCEPTION] ExitStatus`)
   while `openmpt_module_create_from_memory` — the deprecated v1 entry point — loads the same
   files normally. `TrackerDecoder` uses v1 for that reason; do not "upgrade" it back without
   re-testing tracker playback in a real browser.

**Why:** the abort surfaces only as `ExitStatus` from Emscripten's `quit_`, with no native message,
so the natural suspects (symbol missing, stack overflow, signature mismatch, bad data) are all
wrong and each costs a full publish cycle to rule out.

**How to apply:** if tracker music kills the browser page, check these two first before
investigating the audio path. A standalone `emcc` build of the same archive works fine — it is
only the .NET module that fails, so a working standalone test does not clear the archive.
Related: [[wasm-pinvoke-table-and-gl-proc-addresses]].
