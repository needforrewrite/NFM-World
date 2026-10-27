# Native libraries for the browser build

`libopenmpt.a` is **not checked in**. It takes `make` and a Linux toolchain to build, so
`web.yml` builds it in an Ubuntu job and passes the archive to the Windows job as an artifact.
Locally, run the same script:

```sh
NFMWorld.Platform.WebGL/native/build-libopenmpt.sh NFMWorld.Platform.WebGL/native
```

It must be built with **.NET 11's own Emscripten (6.0.2)**, not a system one: the archive is
linked into the same module as the .NET runtime, and objects from a different Emscripten version
can fail at link time or misbehave afterwards. The script takes the toolchain from the
`wasm-tools` workload's pack, so `dotnet workload install wasm-tools` has to have run. Set
`EMSDK` to point it at a different checkout (that is how it was validated locally).

## What the script does, and why

Source: <https://github.com/OpenMPT/openmpt>, tag `libopenmpt-0.8.6`. The autotools tarball from
lib.openmpt.org does not carry `build/make/`, which is where the Emscripten config lives, so the
git checkout is the only usable source.

It builds the archive target directly. A bare `make` builds the examples and the shared library
instead, silently producing a different object set:

```sh
make -j$(nproc) bin/libopenmpt.a CONFIG=emscripten EMSCRIPTEN_TARGET=wasm \
  NO_SHARED=1 NO_EXAMPLES=1 NO_TESTS=1 NO_TOOLS=1 NO_DOCS=1 \
  NO_ZLIB=1 NO_MPG123=1 NO_OGG=1 NO_VORBIS=1 NO_FLAC=1 NO_SNDFILE=1 \
  NO_PORTAUDIO=1 NO_SDL=1 NO_PULSEAUDIO=1 NO_JACK=1
```

The `NO_*` switches drop the optional format codecs and backends — the game only needs the
tracker formats, and each of the dropped ones pulls in a library the browser module does not
link.

`NO_ZLIB=1` **enables** libopenmpt's bundled miniz fallback rather than removing zlib support, so
`miniz.o` ends up in the archive and supplies `mz_*`. That is what makes it self-contained. It
also pulls in `minimp3`/`stb_vorbis`, which is harmless.

## Two traps, both silent

**The module's exception model.** `.NET` links the final module itself. Native wasm exception
handling (`-fwasm-exceptions`) is not usable here: Emscripten 6.0.2 emits a module the browser
refuses to instantiate (`invalid value type 'rtt'`, which is gated behind the garbage-collection
proposal), and linking a wasm-exception archive crashes `wasm-ld` outright. So the head sets
`WasmEnableExceptionHandling=false`, which links with the legacy model, and the archive must be
built the same way. Mixing the two is **not** a link error — the module loads and then aborts the
first time the C++ throws, with no message.

**`openmpt_module_create_from_memory2` aborts; `openmpt_module_create_from_memory` does not.**
Under the legacy exception model, the v2 entry point throws internally and never reaches its
error return; the v1 entry point loads the same files through the same parser and returns
normally. `TrackerDecoder` therefore calls v1. Do not "upgrade" it back to v2 without re-testing
tracker playback in a browser.
