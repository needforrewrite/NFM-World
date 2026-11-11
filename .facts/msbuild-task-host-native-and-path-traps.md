---
name: msbuild-task-host-native-and-path-traps
description: "Three non-obvious traps when a build-time component is an MSBuild task built by the same build (natives, task locking, relative paths)."
metadata: 
  node_type: memory
  type: project
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-24T01:39:17.868Z
---

The shader compile task (`NFMWorld.ShaderCompiler.Task`, built by `nfm-world`'s build) hit three traps
that are generic to any build-time task built by the build that uses it. All three are fixed in place;
the fixes are load-bearing and non-obvious, so don't "simplify" them back.

**1. Silk.NET natives cannot be found by any normal mechanism under a task host.** The bindings do
their *own* loading rather than using `[DllImport]` — the name goes to Silk.NET.Core's
`LibraryLoader` inside a static *method* (`GetApi()` → `CreateDefaultContext` →
`new UnmanagedLibrary(names)`), not a type initializer, so merely naming the type is enough to
reach it. Two consequences:
- `NativeLibrary.SetDllImportResolver` registered on *your* assembly never fires — a resolver only
  applies to P/Invokes made from the assembly it is registered on, and none are made from yours.
- The `runtimes/<rid>/native/` layout is searched relative to `AppContext.BaseDirectory`, which under
  `TaskHostFactory` is `C:\Program Files\dotnet\sdk\<ver>\`, not the task's own folder. So the layout
  that works for a normal exe (the POC next door) is never probed for a task.

Fix: `NativeLibraries.EnsureLoaded()` calls `NativeLibrary.TryLoad` with an **absolute path** built
from `typeof(...).Assembly.Location`'s directory, before any Silk.NET type is touched. Windows then
resolves Silk.NET's later bare-name lookup against the already-loaded module. The DLLs are copied
*flat* next to the task DLL (`Link="shaderc_shared.dll"`), not into `runtimes/`. The base directory
under a task host is the SDK folder, so the task cannot rely on it for anything.

**2. An in-process task locks its own DLL, making the build that owns it unrebuildable.**
MSBuild maps a task assembly into the build process, and Windows forbids overwriting a mapped file.
Symptom: `error MSB3027 ... locked by ".NET Host (NNNNN), MSBuild.dll"`, and the longer-lived the
node, the worse — a build reused a *four-day-old Rider controller node*, so the file stayed locked
across sessions. Fix: `TaskFactory="TaskHostFactory"` on the `<UsingTask>`, which loads the task
out-of-process. Note Rider's MSBuild nodes are killed by closing Rider, not by
`dotnet build-server shutdown` (which only handles its own nodes).

**2b. A naive repro of trap #1 succeeds on a dev machine, so don't trust a passing repro.** The
native resolves out of `%USERPROFILE%\.nuget\packages\silk.net.shaderc.native\...` (the location
`NativePackageResolver` hardcodes) via `deps.json`, *before* the `Resolvers` chain is exhausted. And
`PATH` may pick up a machine-wide copy — this machine has
`C:\VulkanSDK\1.4.357.0\Bin\shaderc_shared.dll`. A repro only fails after deleting `runtimes/`,
faking `USERPROFILE`, and sanitizing `PATH`. So "it works here" is not evidence trap #1 is absent.

**2c. Which "could not load" message you get tells you where to look, and the obvious reading is
backwards.** A task host that cannot find shaderc reports `FileNotFoundException: "Could not load
from any of the possible library names! Please make sure that the library is installed and in the
right place!"`. That string is **not in `Silk.NET.Core.dll`** — Silk.NET's codegen emits it into
*every generated binding assembly* (it is in Silk.NET.Shaderc, OpenGL, OpenGLES, DXGI, GLFW, SDL,
Vulkan, SPIRV.Cross, …). In `Silk.NET.Shaderc.Shaderc::CreateDefaultContext` (RVA 0x5538) it is
thrown at `IL_0023`, *after* `DefaultNativeContext.TryCreate` at `IL_000b` has already run
`UnmanagedLibrary.TryCreate` → `LibraryLoader.TryLoadNativeLibrary` over every candidate. So it
means the native genuinely was not discoverable — the loader did run to exhaustion.
The *other* message, `"Could not find or load the native library from any name: [ … ]"` (plus the
single-name variant `"Could not find or load the native library: … Attempted: …"`), comes from
`LibraryLoader.ThrowLibNotFoundAny` / `ThrowLibNotFound` in Core and belongs to the **throwing**
`LoadNativeLibrary` overload, which **no generated binding calls at all** — seeing it means
something used that API directly rather than going through a binding's `GetApi()`.

Reading these assemblies is itself fiddly, and the usual tools all fail:
`strings` finds nothing (.NET metadata strings are UTF-16LE, not ASCII);
`MetadataLoadContext`'s `ResolveString` fails on these assemblies outright (102/102 unresolved);
and `Assembly.LoadFrom` cannot even enumerate types, because the binding's dependency on
`Silk.NET.Core` is unresolvable in a scratch context. What works is a small `PEReader` +
`MetadataReader` walk: enumerate `MethodDefinitions`, get each body via
`pe.GetMethodBody(m.RelativeVirtualAddress)`, scan the IL for the `0x72` (`ldstr`) opcode, and
resolve the 4-byte token with `MetadataTokens.UserStringHandle` → `md.GetUserString`. Pin a method
by RVA and read its `body.ExceptionRegions` to see which exceptions it actually catches. Keep a
throwaway project for this; it is the same technique as the `NativeLibraries.cs` fix itself.

**3. `IntermediateOutputPath` is empty at import time, and relative paths resolve against the
process CWD.** A `PropertyGroup` in an imported `.targets` runs before the SDK's targets define
`IntermediateOutputPath`, so `$(IntermediateOutputPath)ShaderCompiler\` silently evaluates to
`ShaderCompiler\` in the project directory. Separately, a relative path handed to a task resolves
against the *process* working directory (the consumer project dir under a task host). Fix: resolve
the path inside a target (`ResolveShaderPaths`, depended on by the consumers) and prefix
`$(MSBuildProjectDirectory)\`.

**Why:** each of these fails with a message that points at the wrong file or is simply opaque, and
each "obvious" fix (copy natives next to the task; check `!Exists` on the DLL) is wrong in a way that
looks right.

**How to apply:** if a build-time task starts failing to load, check these three in order before
anything else. Full detail in the comments on `NativeLibraries.cs`, the `<UsingTask>` in
`NFMWorld.ShaderCompiler.targets`, and `ResolveShaderPaths`. Related: [[sokol-backend-bundle-shaders-need-hlsl]].
