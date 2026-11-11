---
name: silk-egl-generated-using-trim-breaks-cs1614
description: "Removing \"unused\" usings from Maxine.Silk.EGL's generated .gen.cs files breaks the build with CS1614 on [Extension(...)]."
metadata:
  node_type: memory
  type: project
  originSessionId: ffae20f0-7347-4393-995b-7b2c99dd6fed
  modified: 2026-10-29T22:08:22.291Z
---

Maxine.Silk.EGL's `*.gen.cs` files (produced by Silk.NET.BuildTools via `tools/SilkGenerate` + `generator.json`) must keep their full using block. An IDE "remove unused usings" pass strips `System.Runtime.CompilerServices`, `System.Text`, `Silk.NET.Core` and `Silk.NET.Core.Loader` from ~35 of them, and the build then fails with `CS1614: 'Extension' is ambiguous between 'ExtensionAttribute' and 'ExtensionAttribute'` on every `[Extension("...")]`.

**Why:** the generated files rely on `using Extension = Silk.NET.Core.Attributes.ExtensionAttribute;` plus `using Silk.NET.Core.Attributes;`. With `using System.Runtime.CompilerServices;` present (which also defines an `ExtensionAttribute`), Roslyn resolves the alias; without it, the same alias is reported as ambiguous. Counter-intuitive but reproducible on SDKs 8/10/11 and every `LangVersion`. Confirmed by bisecting the using block: deleting `System.Runtime.CompilerServices;` alone reproduces it, and re-adding it alone fixes it.

**How to apply:** treat these generated files as read-only — never run cleanup/formatting on `Maxine.Silk.EGL/src`. If CS1614 shows up, `git checkout` the `.gen.cs` files rather than hand-editing, then rebuild (the generator's output itself is upstream-shaped and correct). Related: [[git-status-before-work-discarding-ops]].
