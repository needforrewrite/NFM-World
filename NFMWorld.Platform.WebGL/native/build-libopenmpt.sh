#!/usr/bin/env bash
# Builds libopenmpt.a for wasm with .NET's own Emscripten, so the archive matches the runtime it
# gets linked into. Run by .github/workflows/web.yml; runnable locally too.
#
# The output is not checked in - NFMWorld.Browser.csproj picks it up from
# NFMWorld.Platform.WebGL/native/libopenmpt.a, which CI fills from this script's artifact.
#
# Two traps that this script exists to avoid, both silent:
#
#   - It MUST be .NET's Emscripten, not a system one. Different Emscripten versions produce
#     objects that fail to link or misbehave in the final module.
#   - It MUST build unmodified. Upstream's config-emscripten.mk uses the legacy JS exception
#     model, which is the model the head links with (WasmEnableExceptionHandling=false);
#     switching the archive to -fwasm-exceptions links cleanly and then aborts at run time.
#
# Requires: git, make, nproc, and the wasm-tools workload (dotnet workload install wasm-tools).
#
# Usage: build-libopenmpt.sh <output-dir>
set -euo pipefail

LIBOPENMPT_TAG="libopenmpt-0.8.6"

out_dir="${1:-$(pwd)/out}"
mkdir -p "$out_dir"
out_dir="$(cd "$out_dir" && pwd)"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# The autotools tarball from lib.openmpt.org has no build/make/, and config-emscripten.mk lives
# there, so the git checkout is the only usable source.
echo "==> cloning openmpt $LIBOPENMPT_TAG"
git clone --depth 1 --branch "$LIBOPENMPT_TAG" https://github.com/OpenMPT/openmpt.git "$work/openmpt"
cd "$work/openmpt"

# A caller may point at a toolchain directly; otherwise use the one the wasm-tools workload
# installed, which is the version the runtime is built against. Kept separately because
# emsdk_env.sh does not leave EMSDK set in every layout.
emdk_root="${EMSDK:-}"
if [ -n "$emdk_root" ]; then
    echo "==> using EMSDK from the environment: $emdk_root"
    # shellcheck disable=SC1091
    source "$emdk_root/emsdk_env.sh" >/dev/null
else
    # Workload packs live under the dotnet root, not the NuGet cache, and the Emscripten ones are
    # per-RID: ...Emscripten.6.0.2.Sdk.<rid>. Derive the root from the running SDK rather than
    # guessing /usr/share or /usr/lib, and take whichever version is installed.
    dotnet_root="$(dirname "$(dirname "$(readlink -f "$(command -v dotnet)")")")"
    packs_dir="$dotnet_root/packs"
    find_emscripten_pack() {
        local kind="$1" dir
        dir="$(ls -d "$packs_dir"/Microsoft.NET.Runtime.Emscripten.*."$kind".* 2>/dev/null | sort -V | tail -1)"
        [ -n "$dir" ] || { echo "no Emscripten $kind pack under $packs_dir" >&2; return 1; }
        # <pack>/<version>/tools
        echo "$dir"/*/tools
    }

    sdk_tools="$(find_emscripten_pack sdk)"
    node_pack="$(find_emscripten_pack node)"
    echo "==> emscripten: $sdk_tools"

    # The SDK pack's own emsdk_env.sh points PATH and the .emscripten config at this toolchain; it
    # reads these four variables and refuses to run without them.
    export EMSDK_PATH="$sdk_tools"
    export DOTNET_EMSCRIPTEN_LLVM_ROOT="$sdk_tools/bin"
    export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$sdk_tools"
    export DOTNET_EMSCRIPTEN_NODE_JS="$(ls "$node_pack"/*/bin/node 2>/dev/null | head -1 || true)"
    [ -n "$DOTNET_EMSCRIPTEN_NODE_JS" ] || { echo "no node in $node_pack" >&2; exit 1; }

    # shellcheck disable=SC1091
    source "$sdk_tools/emsdk_env.sh"
fi

# build/make/config-emscripten.mk shells out to `emcc ... -c` to probe the C++ standard, so the
# compiler has to be callable before make starts.
command -v em++ >/dev/null || { echo "em++ not on PATH after emsdk_env.sh" >&2; exit 1; }
em++ --version | head -1

# A bare `make` builds the examples and the shared library instead of the archive, silently
# producing a different object set, so the target is named explicitly.
echo "==> building bin/libopenmpt.a"
make -j"$(nproc)" bin/libopenmpt.a \
    CONFIG=emscripten EMSCRIPTEN_TARGET=wasm \
    NO_SHARED=1 NO_EXAMPLES=1 NO_TESTS=1 NO_TOOLS=1 NO_DOCS=1 \
    NO_ZLIB=1 NO_MPG123=1 NO_OGG=1 NO_VORBIS=1 NO_FLAC=1 NO_SNDFILE=1 \
    NO_PORTAUDIO=1 NO_SDL=1 NO_PULSEAUDIO=1 NO_JACK=1

archive=bin/libopenmpt.a
[ -f "$archive" ] || { echo "build finished without $archive" >&2; exit 1; }

# The failure this guards against links cleanly and only shows up as a dead page, so check the
# exception model the archive was actually built with.
# em++ sits in <sdk>/upstream/emscripten and llvm-nm in <sdk>/upstream/bin, so the compiler's own
# directory is not where the tool is. Try PATH, then the two layouts emscripten uses.
nm="$(command -v llvm-nm || true)"
if [ -z "$nm" ]; then
    for candidate in \
        "$emdk_root/upstream/bin/llvm-nm" \
        "$(dirname "$(command -v em++)")/../bin/llvm-nm" \
        "$(dirname "$(command -v em++)")/llvm-nm"
    do
        [ -x "$candidate" ] && { nm="$candidate"; break; }
    done
fi
[ -n "$nm" ] || { echo "llvm-nm not found; cannot verify the exception model" >&2; exit 1; }

personality="$("$nm" "$archive" 2>/dev/null | grep -c '__gxx_personality_v0' || true)"
wasm_personality="$("$nm" "$archive" 2>/dev/null | grep -c '__gxx_wasm_personality_v0' || true)"
echo "==> exception model: legacy=$personality wasm=$wasm_personality"
[ "$personality" -gt 0 ] || { echo "archive is not the legacy-exception build" >&2; exit 1; }
[ "$wasm_personality" -eq 0 ] || { echo "archive is a -fwasm-exceptions build" >&2; exit 1; }

cp "$archive" "$out_dir/libopenmpt.a"
echo "==> wrote $out_dir/libopenmpt.a ($(du -h "$out_dir/libopenmpt.a" | cut -f1))"
