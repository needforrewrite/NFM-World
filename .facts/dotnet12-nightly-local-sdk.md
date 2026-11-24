# A .NET 12 nightly, installed without touching the machine's SDK

The browser head is `net12.0` and only builds on a nightly (see AGENTS.md, "Build / run"). Getting
one should not require replacing `C:\Program Files\dotnet`, and it does not:

```bash
curl -sSL https://raw.githubusercontent.com/danardelean/dotnet-nightly-local/main/install.ps1 \
  -o scratch/install.ps1
pwsh -NoLogo -NoProfile -File scratch/install.ps1 -Channel 12.0.1xx -Workloads wasm-tools \
  -Dir W:/projects/NFM-World/scratch/dotnet12 -NoGlobalJson
```

Then build with that SDK explicitly:

```bash
W:/projects/NFM-World/scratch/dotnet12/dotnet.exe publish NFMWorld.Browser/NFMWorld.Browser.csproj \
  -c Release -p:NfmwBrowser=true
```

`-NoGlobalJson` matters: without it the script writes a `global.json` in the working directory that
re-pins every `dotnet` command in the repo, so the desktop build would start resolving the nightly
too. `-Workloads wasm-tools` is what installs the Emscripten packs (`Microsoft.NET.Runtime.Emscripten.6.0.2.*`)
that a `browser-wasm` publish needs.

Measured install: `12.0.100-alpha.1.26571.109`, sdk-manifests in a `12.0.100-alpha.1` band,
Emscripten **6.0.3**, 392 MB Cache pack. `scratch/` is gitignored, so this is per-machine.

## Why not `dotnet-install.sh`

It cannot resolve this one: `--channel 12.0.1xx` exits with "Failed to resolve the exact version
number" (a daily lane has no `latest.version`), and the aka.ms links for `12.0`, `net12.0`, `daily`,
`nightly`, `main` and `12.0.100-alpha` all 404 or redirect somewhere else. What does answer is the
build URL directly:

```
https://ci.dot.net/public/Sdk/12.0.100-alpha.1.26571.109/dotnet-sdk-12.0.100-alpha.1.26571.109-win-x64.zip
```

`install.ps1` gets there the intended way: it follows the aka.ms redirect of
`https://aka.ms/dotnet/<channel>/daily/dotnet-sdk-<rid>.zip`, whose `Location` carries the version,
then downloads that archive and its `.sha512` and extracts with `tar`. Its real value over the zip is
step 2 — a daily SDK ships stale baseline workload manifests (preview-era iOS/MAUI in an older band
folder), so the script downloads the newest manifest of the SDK's own band for every id in
`KnownWorkloadManifests.txt` and installs the workload against those.

## Do not also install `wasm-tools` into the GA SDK

The GA SDK's workload resolver takes the newest manifest across all bands, so a 12.0 wasm-tools
manifest beside 11.0 packs makes the net11.0 desktop builds resolve `Microsoft.NET.Runtime.WebAssembly.Sdk`
from the wrong band. Keep the two installs disjoint: `scratch/dotnet12` builds the browser head,
`C:\Program Files\dotnet` builds everything else. That is the same reason the head sets
`NfmwBrowser=true` only on the command line — see `net11-browser-wasm-project-setup`.

## In CI

`web.yml` pins the version rather than a channel, because the channel moves:
`actions/setup-dotnet` accepts a `12.0.100-alpha.1.26571.109` version string directly, and the job
then needs `dotnet workload install wasm-tools` from that SDK. Bumping the pin is what upgrades the
browser head; nothing else references 12.x.
