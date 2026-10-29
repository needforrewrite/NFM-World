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
    # per-RID: ...Emscripten.6.0.2.Sdk.<rid>. Take the root from DOTNET_ROOT when the host sets it
    # (GitHub's runner does), otherwise from the running dotnet binary - one dirname, since that
    # binary sits directly in the root. Resolving it a second time lands on the parent of the
    # install (/usr/share instead of /usr/share/dotnet) and finds no packs at all.
    if [ -n "${DOTNET_ROOT:-}" ] && [ -d "$DOTNET_ROOT/packs" ]; then
        dotnet_root="$DOTNET_ROOT"
    else
        dotnet_root="$(dirname "$(readlink -f "$(command -v dotnet)")")"
    fi
    packs_dir="$dotnet_root/packs"
    echo "==> dotnet root: $dotnet_root"
    # The SDK, Node and Cache packs have to come from one Emscripten version: an SDK's sysroot and
    # its prebuilt libraries are only valid for the compiler that produced them, and each kind
    # sorts independently. So the Sdk pack's version is chosen first and the other two are asked
    # for that exact version. <pack>/<version>/tools.
    emscripten_version="$(ls -d "$packs_dir"/Microsoft.NET.Runtime.Emscripten.*.Sdk.* 2>/dev/null \
        | sort -V | tail -1 | sed 's|.*/Microsoft\.NET\.Runtime\.Emscripten\.\([^/]*\)\.Sdk\..*|\1|')"
    [ -n "$emscripten_version" ] || { echo "no Emscripten Sdk pack under $packs_dir" >&2; exit 1; }
    echo "==> emscripten version: $emscripten_version"

    # Capitalised: the packs are ...Emscripten.<version>.<Kind>.<rid>, and a glob is case-sensitive
    # on Linux, where this runs in CI. Lowercase matched only because it was tested on Windows'
    # case-insensitive filesystem.
    find_emscripten_pack() {
        local kind="$1" dir
        dir="$(ls -d "$packs_dir"/Microsoft.NET.Runtime.Emscripten."$emscripten_version"."$kind".* 2>/dev/null | head -1)"
        [ -n "$dir" ] || { echo "no Emscripten $kind pack for $emscripten_version" >&2; return 1; }
        echo "$dir"/*/tools
    }

    sdk_tools="$(find_emscripten_pack Sdk)"
    node_pack="$(find_emscripten_pack Node)"
    echo "==> emscripten: $sdk_tools"

    # Emscripten needs a cache it can write to. The SDK pack ships none of its own and its
    # .emscripten sets FROZEN_CACHE, so emcc refuses to build the missing entries and every compile
    # dies with "FROZEN_CACHE is set, but cache file is missing: sysroot_install.stamp".
    #
    # The prebuilt sysroot lives in the separate Cache pack, under the read-only dotnet root - so
    # pointing EM_CACHE straight at it fixes the missing-file error and then fails the first time
    # anything wants to write. What the SDK itself does (BrowserWasmApp.targets) is freeze only when
    # the cache dir *is* the pack's, and otherwise let emcc populate a writable one; this does the
    # same, and seeds the writable cache from the pack once so the sysroot need not be rebuilt.
    cache_pack="$(find_emscripten_pack Cache)"
    export EM_CACHE="${TMPDIR:-/tmp}/nfmw-emscripten-cache"
    if [ ! -d "$EM_CACHE/sysroot" ]; then
        echo "==> seeding emscripten cache from $cache_pack"
        mkdir -p "$EM_CACHE"
        cp -R "$cache_pack"/emscripten/cache/. "$EM_CACHE"/
        chmod -R u+w "$EM_CACHE"

        # The pack's sanity line records the path the toolchain was built at, which is not this
        # one: left as-is, emcc decides the config changed and calls cache.erase(), deleting the
        # sysroot that was just copied. The line is `<version>|<llvm root>`, so rewriting it for
        # this location is what makes the seeded cache count as current.
        #
        # The version comes from emscripten-version.txt with its surrounding quotes stripped, which
        # is how emcc itself reads it (tools/utils.py). Leaving them in writes `"6.0.3"` where the
        # compiler expects `6.0.3`, which compares unequal for the same reason a wrong path does.
        emscripten_version_text="$(tr -d '"' < "$sdk_tools/emscripten/emscripten-version.txt" | tr -d '\r\n')"
        printf '%s|%s\n' "$emscripten_version_text" "$DOTNET_EMSCRIPTEN_LLVM_ROOT" > "$EM_CACHE/sanity.txt"
    fi
    # Not the pack's own directory, so nothing here may be frozen.
    export EM_FROZEN_CACHE=0
    echo "==> emscripten cache: $EM_CACHE"

    # The SDK pack's own emsdk_env.sh points PATH and the .emscripten config at this toolchain; it
    # reads these four variables and refuses to run without them.
    #
    # node_pack already ends at <pack>/<version>/tools, so the binary is directly under its bin -
    # not another level down. A second wildcard here finds nothing and reports "no node" against a
    # pack that has one. Both names are accepted because a Windows pack ships node.exe and a Linux
    # one ships node, so the same script works in CI and in a WSL checkout of this tree.
    export EMSDK_PATH="$sdk_tools"
    export DOTNET_EMSCRIPTEN_LLVM_ROOT="$sdk_tools/bin"
    export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$sdk_tools"
    DOTNET_EMSCRIPTEN_NODE_JS=""
    for candidate in "$node_pack/bin/node" "$node_pack/bin/node.exe"; do
        [ -x "$candidate" ] && { DOTNET_EMSCRIPTEN_NODE_JS="$candidate"; break; }
    done
    export DOTNET_EMSCRIPTEN_NODE_JS
    [ -n "$DOTNET_EMSCRIPTEN_NODE_JS" ] || { echo "no node under $node_pack/bin" >&2; exit 1; }

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
