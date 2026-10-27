#!/usr/bin/env python3
"""Builds SDL3 to LLVM bitcode for the browser head.

Not a port: SDL3 has an official emscripten configuration, and this follows the recipe in
emscripten's own tools/ports/sdl3.py - same glob list, same disable flags. The two differences
are both required by .NET rather than by SDL:

  - every translation unit is compiled with -emit-llvm. The .NET native build links with LTO,
    which silently drops the symbols of any plain wasm object nothing in the bitcode mentions;
    the symptom is a bare ManagedError("SDL3") at the first P/Invoke.
  - the archive is named SDL3.a, because the generated P/Invoke table is keyed by the native
    file's base name and must match the [DllImport("SDL3")] strings exactly.

The port's SDL_build_config.h is copied in on purpose: it is what switches off the backends
that do not exist here (GPU, render drivers, async io, tray), and without it the link fails on
symbols from subsystems nothing on this platform can provide.

Usage: build_sdl3.py <output.a> <work-dir>
Run by the BuildSDL3 target in this project, which supplies the
emscripten toolchain through the same environment that the .NET wasm build uses.
"""

import glob
import os
import shutil
import subprocess
import sys
import tarfile

SOURCE_URL = "https://github.com/libsdl-org/SDL/archive/refs/tags/release-3.4.2.tar.gz"
SOURCE_DIR = "SDL-release-3.4.2"

# From emscripten's tools/ports/sdl3.py. Kept in the same order and grouping so a future SDL
# bump can be diffed against the port rather than re-derived.
GLOB_PATTERNS = [
    "*.c",
    "atomic/*.c",
    "audio/*.c",
    "camera/*.c",
    "core/*.c",
    "core/unix/*.c",
    "cpuinfo/*.c",
    "dynapi/*.c",
    "events/*.c",
    "io/*.c",
    "io/generic/*.c",
    "filesystem/*.c",
    "gpu/*.c",
    "joystick/*.c",
    "haptic/*.c",
    "hidapi/*.c",
    "locale/*.c",
    "main/*.c",
    "misc/*.c",
    "power/*.c",
    "render/*.c",
    "render/*/*.c",
    "sensor/*.c",
    "stdlib/*.c",
    "storage/*.c",
    "thread/*.c",
    "time/*.c",
    "timer/*.c",
    "video/*.c",
    "video/yuv2rgb/*.c",
    "tray/*.c",
    "storage/generic/*.c",
    "tray/unix/*.c",
    "time/unix/*.c",
    "timer/unix/*.c",
    "main/emscripten/*.c",
    "filesystem/posix/*.c",
    "filesystem/emscripten/*.c",
    "locale/emscripten/*.c",
    "camera/emscripten/*.c",
    "joystick/emscripten/*.c",
    "joystick/virtual/*.c",
    "power/emscripten/*.c",
    "misc/emscripten/*.c",
    "audio/emscripten/*.c",
    "video/emscripten/*.c",
    "video/offscreen/*.c",
    "audio/disk/*.c",
    "loadso/dlopen/*.c",
    "camera/dummy/*.c",
    "audio/dummy/*.c",
    "video/dummy/*.c",
    "haptic/dummy/*.c",
    "sensor/dummy/*.c",
    # No pthreads in this build, so the generic thread backend rather than thread/pthread.
    "thread/generic/*.c",
    # The remaining backends. SDL_build_config.h declares these subsystems available, so SDL
    # exports their entry points even though the port's glob list leaves the backends out - the
    # link then fails on whole subsystems rather than on individual files.
    "dialog/*.c",
    "dialog/dummy/*.c",
    "dialog/unix/*.c",
    "process/*.c",
    "process/dummy/*.c",
]


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2

    archive, work = (os.path.abspath(p) for p in sys.argv[1:3])
    sdk = os.environ["EMSDK"]
    emcc = os.path.join(sdk, "emscripten", "emcc.bat")
    llvm_ar = os.path.join(sdk, "bin", "llvm-ar.exe")
    root = os.path.join(work, SOURCE_DIR)
    objects = os.path.join(work, "obj")

    os.makedirs(work, exist_ok=True)
    if not os.path.isdir(root):
        tarball = os.path.join(work, "sdl3.tar.gz")
        if not os.path.isfile(tarball):
            print(f"fetching {SOURCE_URL}")
            subprocess.run(["curl", "-sSL", "-o", tarball, SOURCE_URL], check=True)
        with tarfile.open(tarball) as tar:
            tar.extractall(work)

    # The port's config, not SDL's default: it is what disables the backends that cannot exist
    # on this platform, and SDL would otherwise compile them in and fail to link.
    config = os.path.join(sdk, "emscripten", "tools", "ports", "sdl3", "SDL_build_config.h")
    shutil.copyfile(config, os.path.join(root, "include", "SDL3", "SDL_build_config.h"))

    os.makedirs(objects, exist_ok=True)
    flags = [
        "-c", "-O2", "-emit-llvm", "-sUSE_SDL=0",
        "-I" + os.path.join(root, "include"),
        # SDL_internal.h includes "SDL_build_config.h" by bare name, and the port puts that file
        # inside include/SDL3 rather than at the top of the include tree.
        "-I" + os.path.join(root, "include", "SDL3"),
        "-I" + os.path.join(root, "src"),
    ]

    built = []
    for pattern in GLOB_PATTERNS:
        for source in sorted(glob.glob(os.path.join(root, "src", pattern))):
            # Path-qualified names: SDL has same-named sources in different backends.
            name = os.path.relpath(source, os.path.join(root, "src")).replace(os.sep, "_")
            obj = os.path.join(objects, name + ".o")
            if not (os.path.isfile(obj) and os.path.getmtime(obj) >= os.path.getmtime(source)):
                result = subprocess.run([emcc] + flags + [source, "-o", obj],
                                        capture_output=True, text=True)
                if result.returncode != 0:
                    print(f"FAILED {source}\n{result.stderr[-2000:]}")
                    return 1
            built.append(obj)

    if os.path.isfile(archive):
        os.remove(archive)
    subprocess.run([llvm_ar, "crs", archive] + built, check=True)
    print(f"archived {len(built)} objects into {archive}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
