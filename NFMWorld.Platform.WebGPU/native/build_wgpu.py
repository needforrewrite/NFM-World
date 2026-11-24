#!/usr/bin/env python3
"""Builds emdawnwebgpu's webgpu.cpp and the JS-backed forwarders to LLVM bitcode.

emdawnwebgpu was chosen over wgpu-native for the browser because wgpu-native has no wasm build.
Everything below is a consequence of one fact: the package is a *source* package, so the portable
C API has to be compiled here, with the same Emscripten that will link the module.

  - -emit-llvm, not a plain wasm object. The .NET native build links with LTO and silently drops
    the symbols of non-bitcode objects nothing in the bitcode mentions; the symptom is a bare
    ManagedError at the first P/Invoke.
  - -fno-exceptions, which is upstream's own flag for this package. It emits no landing pads, so
    the object carries no exception model at all and links into the module's wasm-EH one. An
    archive built with the *legacy* model is the crash: wasm-ld dies in instruction selection on
    whichever function happens to have a landing pad.
  - the archive is named wgpu_native.a because the generated P/Invoke table is keyed by the
    native file's base name, and WgpuNativeContext's [DllImport("wgpu_native")] has to match.
  - webgpu.h's C++ half is not needed: the header is included as C, and only the four JS
    libraries are handed to the link separately (they cannot be compiled - see the csproj).

Usage: build_wgpu.py <archive> <work-dir> <package-dir> <shim-source>
Run by the BuildWebGpuShim target in NFMWorld.Platform.WebGPU.csproj.
"""

import os
import subprocess
import sys

SOURCES = ["webgpu/src/webgpu.cpp"]


def main() -> int:
    if len(sys.argv) != 5:
        print(__doc__)
        return 2

    archive, work, pkg, shim = (os.path.abspath(p) for p in sys.argv[1:5])
    sdk = os.environ["EMSDK"]
    emcc = os.path.join(sdk, "emscripten", "emcc.bat")
    llvm_ar = os.path.join(sdk, "bin", "llvm-ar.exe")
    objects = os.path.join(work, "obj")
    include = os.path.join(pkg, "webgpu", "include")

    os.makedirs(objects, exist_ok=True)

    # The shim is C, the rest C++; one flag list would pass -std=c++20 to the C compiler, which
    # emcc rejects outright.
    c_flags = ["-c", "-O2", "-emit-llvm", "-I" + include]
    cxx_flags = c_flags + ["-std=c++20", "-fno-exceptions"]

    # A flags change has to invalidate the objects, or an object built without a define that
    # gates a whole block of the header is kept and the link asks for symbols it never had.
    stamp = os.path.join(objects, "flags.txt")
    stamp_text = "\n".join(cxx_flags + [pkg])
    stale = not os.path.isfile(stamp) or open(stamp).read() != stamp_text
    if stale:
        for name in os.listdir(objects):
            if name.endswith(".o"):
                os.remove(os.path.join(objects, name))
        open(stamp, "w").write(stamp_text)

    steps = [(os.path.join(pkg, rel), cxx_flags, os.path.join(objects, os.path.basename(rel) + ".o")) for rel in SOURCES]
    steps.append((shim, c_flags, os.path.join(objects, "wgpu_shim.o")))

    built = []
    for source, flags, obj in steps:
        if stale or not (os.path.isfile(obj) and os.path.getmtime(obj) >= os.path.getmtime(source)):
            result = subprocess.run([emcc] + flags + [source, "-o", obj], capture_output=True, text=True)
            if result.returncode != 0:
                print(f"FAILED {source}\n{result.stderr[-4000:]}")
                return 1
        built.append(obj)

    # Delete first: `llvm-ar crs` updates a member in place, and one whose function list shrank
    # leaves the old prototype in the index, which links as the wrong wrapper arity.
    if os.path.isfile(archive):
        os.remove(archive)
    subprocess.run([llvm_ar, "crs", archive] + built, check=True)
    print(f"archived {len(built)} objects into {archive}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
