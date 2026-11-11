---
name: wasm-runtime-async-and-rid-propagation
description: "Why a library can't see the browser RID, and the property that must be passed globally to turn runtime-async off for wasm."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-26T21:33:50.829Z
---

Two linked facts that cost a build cycle each:

1. **Mono's wasm runtime throws `PlatformNotSupportedException: Runtime Async is not supported
   on this platform`** from `AsyncHelpers.Await` — the CoreLib thunk compiled for
   non-CORECLR/NATIVEAOT. It fires the first time any method compiled with
   `<Features>runtime-async=on</Features>` is reached, so it looks like an unrelated subsystem
   failing (it surfaced at `UiRenderer.Reload`). confirmed: searching the failure message in
   dotnet/runtime lands on `AsyncHelpers.cs`'s `#else` block.
2. **`RuntimeIdentifier` never reaches a referenced library.** The SDK marks library projects
   `IsRidAgnostic=true` and puts `RuntimeIdentifier;SelfContained` in `UndefineProperties` for
   every project reference (`Microsoft.Common.CurrentVersion.targets:1982`). So
   `Condition="'$(RuntimeIdentifier)' != 'browser-wasm'"` in a referenced project is *always*
   true, and `AdditionalProperties="RuntimeIdentifier=$(RuntimeIdentifier)"` does not help —
   it just adds a second evaluation with the same stripped value.

**How to apply:** gate wasm-only compiler differences on a plain global property the head
passes with `-p:` (`NfmwBrowser=true`), not on `RuntimeIdentifier`. `NFMWorld.Browser.csproj`
fails the build if it is missing. `IsRidAgnostic=false` in the referenced project also works,
but has to be repeated at every hop. Related: [[net11-browser-wasm-project-setup]],
[[wasm-msbuild-emscripten-toolchain]].
