---
name: mono-wasm-has-no-collectible-alc
description: "Mono's wasm runtime supports AssemblyLoadContext but not collectible contexts, so plugin assemblies can never unload there."
metadata:
  node_type: memory
  type: project
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-27T18:32:23.290Z
---

On `browser-wasm` (Mono), `AssemblyLoadContext` exists and works, but
`new AssemblyLoadContext(name, isCollectible: true)` does not: the runtime does not
support collectible contexts. Anything that loads a plugin assembly there must treat the
load as one-shot and permanently resident.

**Why:** the desktop path in `GamemodePlugin` deliberately loads a plugin fresh per race
and releases it in `End()`, because a plugin's MemoryPack formatters register into a
process-static table and pin its load context. None of that teardown can take effect on
Mono wasm, and a second `LoadFromStream` into a non-collectible context for the same
assembly identity is rejected outright.

**How to apply:** gate collectible behaviour on a platform check rather than assuming the
desktop semantics. In `NFMWorld.Library/Gamemodes/DotNet/`, `PluginLoadContext.IsCollectibleSupported`
is that gate: `GamemodePlugin.Load` caches the `(context, assembly)` pair per gamemode id
when contexts cannot be collected (and reuses it for every race), `Unload()` returns early
without dropping formatters or reflection handles, and `NativeGamemodeFactory.End` skips
arming `PluginUnloadVerifier`. Waiting on CoreCLR-on-wasm instead is the reason this is a
platform check and not a permanent design. See [[browser-build-vfs-routing-rule]] for the
other browser-only branch of the same shape.
