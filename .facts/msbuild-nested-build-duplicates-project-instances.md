# A nested MSBuild call with its own properties compiles the outer graph's projects a second time

`NFMWorld.MsdfCache.targets` builds `NFMWorld.MsdfCache.Tool` from inside `nfm-world`'s build via the
`MSBuild` task, passing `RuntimeIdentifier=;PublishReadyToRun=false` (the release workflows publish with a
global `-r`, which would otherwise move the tool's output and break the hardcoded rid-less assembly path).

Those are **global properties**, so the nested request is a *different project instance* of everything the
tool references — `Apos.Shapes.NFMW`, `NFMWorld.Graphics.Abstraction`, `NFMWorld.Shaders.Abstraction`,
`Maxine.Extensions.Mathematics` — from the instance the surrounding build is already compiling.
MSBuild only de-duplicates identical instances, so both compile the same project into the same
`obj/<cfg>/net11.0/` at the same time and one of them loses the file lock.

That is the whole wasm (and occasionally desktop) publish failure: a single

```
CSC : error CS2012: Cannot open '...\NFMWorld.Shaders.Abstraction\obj\Release\net11.0\NFMWorld.Shaders.Abstraction.dll'
for writing -- The process cannot access the file ... because it is being used by another process.
```

with no other error in the log, and the CI job dying at exit code 1. It reproduces non-deterministically
(it is a race), and it also showed up as a locked `deps.json`.

Fix: the nested tool build passes `BuildProjectReferences=false` and only compiles the tool itself. RAR
still copies the referenced assemblies and their transitive dependencies from their existing build output
(`GetTargetPath`/`GetCopyToOutputDirectoryItems` are metadata queries, no compile). The bake therefore has
to run after the outer graph has built those references: `BakeMsdfCaches` hooks `BeforeTargets="CoreCompile"`
(`ResolveReferences` has run by then), not `PrepareForBuild`.

Same rule for anything else that nest-builds from inside a build: `NFMWorld.ShaderCompiler.targets` gets
away with it only because `NFMWorld.ShaderCompiler{,.Task}` are in no outer graph. `Plugin.Plugins.targets`
passes `PluginOutputRoot` down but keeps it out of the plugin's references via
`_GlobalPropertiesToRemoveFromProjectReferences`, so only the plugin project itself is built twice.

Diagnose with `dotnet build <proj> -v:diag` and count `is building "...csproj" (N) on node M (default targets)`:
two distinct project ids for one project means two instances. A nested build that should only read its
references logs `GetTargetPath`/`GetTargetFrameworks`/`GetCopyToOutputDirectoryItems` and no `Build`.
