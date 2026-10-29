#!/usr/bin/env python3
"""Builds cimgui (Dear ImGui's C API) to LLVM bitcode for the browser head.

The translation units the cimgui build needs: cimgui.cpp plus the imgui sources. The demo is
included even though the game never calls it - the generated binding declares every function in
cimgui.h, and the LTO pass keeps the references, so leaving the demo out fails the final link
with "undefined symbol: ImGui::ShowDemoWindow(bool*)" and friends. The sources live in the
`ext/cimgui` submodule (elix22/cimgui at the commit Hexa.NET.ImGui is generated from).

Two things are required by .NET rather than by ImGui:

  - every translation unit is compiled with -emit-llvm. The .NET native build links with LTO,
    which silently drops the symbols of any plain wasm object nothing in the bitcode mentions;
    the symptom is a bare ManagedError("cimgui") at the first P/Invoke.
  - the archive is named cimgui.a, because the generated P/Invoke table is keyed by the native
    file's base name and must match the [DllImport("cimgui")] strings exactly.

docking is on, matching the Hexa.NET.ImGui binding this is linked against: the binding is
generated from the docking branch, and a non-docking build has no ImGuiConfigFlags_DockingEnable
for the game's styles to set.

Usage: build_cimgui.py <archive> <work-dir> <source-dir>
Run by the BuildCImgui target in NFMWorld.Browser.csproj, which supplies the emscripten
toolchain through the same environment that the .NET wasm build uses.
"""

import os
import subprocess
import sys

SOURCES = [
    "cimgui.cpp",
    "imgui/imgui.cpp",
    "imgui/imgui_demo.cpp",
    "imgui/imgui_draw.cpp",
    "imgui/imgui_tables.cpp",
    "imgui/imgui_widgets.cpp",
]

# Not in the submodule, and named by absolute path: fills the three freetype symbols the generated
# binding references but nothing calls, which have no wasm build.
STUB_NAME = "cimgui_freetype_stub.cpp"


def main() -> int:
    if len(sys.argv) != 4:
        print(__doc__)
        return 2

    archive, work, source_root = (os.path.abspath(p) for p in sys.argv[1:4])
    sdk = os.environ["EMSDK"]
    emxx = os.path.join(sdk, "emscripten", "em++.bat")
    llvm_ar = os.path.join(sdk, "bin", "llvm-ar.exe")
    objects = os.path.join(work, "obj")

    os.makedirs(objects, exist_ok=True)
    flags = [
        "-c", "-O2", "-emit-llvm",
        "-std=c++17",
        "-DIMGUI_DISABLE_OBSOLETE_FUNCTIONS",
        # cimgui.h exposes the offline igText0/igTextColored0/... forms only under this define,
        # and the generated binding calls those: it cannot marshal a C variadic.
        "-DCIMGUI_VARGS0",
        "-I" + source_root,
        "-I" + os.path.join(source_root, "imgui"),
    ]

    # A source-only mtime check misses a flags change (a define flipping a whole block of the
    # header in or out), which silently keeps objects that lack the symbols the link then asks
    # for. The flag list is written beside the objects and any change invalidates them.
    stamp = os.path.join(objects, "flags.txt")
    stamp_text = "\n".join(flags)
    stale_flags = not os.path.isfile(stamp) or open(stamp).read() != stamp_text
    if stale_flags:
        for f in os.listdir(objects):
            if f.endswith(".o"):
                os.remove(os.path.join(objects, f))
        open(stamp, "w").write(stamp_text)

    stub = os.path.join(os.path.dirname(os.path.abspath(__file__)), STUB_NAME)
    built = []
    for rel in SOURCES + [stub]:
        source = os.path.join(source_root, rel) if not os.path.isabs(rel) else rel
        obj = os.path.join(objects, os.path.basename(rel) + ".o")
        if stale_flags or not (os.path.isfile(obj) and os.path.getmtime(obj) >= os.path.getmtime(source)):
            result = subprocess.run([emxx] + flags + [source, "-o", obj],
                                    capture_output=True, text=True)
            if result.returncode != 0:
                print(f"FAILED {source}\n{result.stderr[-3000:]}")
                return 1
        built.append(obj)

    if os.path.isfile(archive):
        os.remove(archive)
    subprocess.run([llvm_ar, "crs", archive] + built, check=True)
    print(f"archived {len(built)} objects into {archive}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
