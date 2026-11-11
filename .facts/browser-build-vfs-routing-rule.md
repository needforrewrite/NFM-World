---
name: browser-build-vfs-routing-rule
description: In the browser build every game file read/write must go through TheVFS.VFS; direct System.IO hits an empty MEMFS.
metadata:
  node_type: memory
  type: project
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-27T03:31:46.204Z
---

The browser head (`NFMWorld.Browser`) has no install directory: the game's data is a zip mounted
into `TheVFS.VFS`, and the only writable location is MEMFS at `/tmp/nfmw`, mounted by
`BrowserBoot.Mount`. Any code that uses `System.IO.File`/`Directory` directly against a game path
therefore breaks there — reads hit an empty filesystem and writes land somewhere the VFS cannot
see.

Routed so far: `StageSelectPhase` (listing), `SettingsMenu.SaveConfig`/`TrySelectRenderer`/
`LoadConfigValues`, and `SavedTimeTrial` (load and save). `VirtualFS` supplies the equivalents —
`FileExists`, `ReadAllLines`, `ReadAllText`, `OpenRead`, `OpenWrite` (creates parent directories),
`EnumerateFiles`, `EnumerateDirectories`. New game code should use those, not `System.IO`.

The head also has to call `SettingsMenu.LoadConfigValues()` itself: the desktop `Main` does it
before the game exists, and `CreateForBrowser` skips that path, so without it persisted settings
are silently ignored on the web. It has to run after the assets are mounted, which
`Program.Main` guarantees.

**Why:** the failure is silent — no exception, just defaults, or a file that saves and then
cannot be read back.

**How to apply:** when adding a file access to game code, grep for an existing `TheVFS.VFS` call
in the same file first. Related: [[libopenmpt-wasm-legacy-exceptions-and-v1-api]].
