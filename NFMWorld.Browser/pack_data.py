#!/usr/bin/env python3
"""Packs the game's data tree into the browser's two archives.

Music is ~90% of the data tree (148 MB against ~11 MB for everything else), so it is split out and
fetched per track at run time instead of being part of the boot payload. The split is by directory:
`data/music/**` goes to one archive, everything else to the other.

Both archives are stored, not deflated. The art (models, fonts) is already compressed and the music
is MP3 or tracker modules, so deflate buys almost nothing, and stored entries let the browser's
fetch read a whole entry without re-inflating it.

Excluded: preact-luau (41 MB of upstream JS plus checked-in lua*.exe tooling the runtime never
loads - router.luau only requires library/sx), node_modules, and the old CEF frontend's build
output.

Usage: pack_data.py <output-dir> <data-root> [<data-root> ...]
Run by the PackData target in NFMWorld.Browser.csproj.
"""

import os
import sys
import zipfile

EXCLUDED_PREFIXES = (
    "data/library/preact-luau/",
    "data/html/dist/",
)

EXCLUDED_SEGMENTS = ("/node_modules/", "/bin/", "/obj/")


def iter_files(roots):
    for root in roots:
        for dirpath, dirnames, filenames in os.walk(root):
            rel_dir = os.path.relpath(dirpath, root).replace(os.sep, "/")
            if rel_dir == ".":
                rel_dir = ""
            for name in filenames:
                rel = f"{rel_dir}/{name}" if rel_dir else name
                archive_name = rel if rel.startswith("data/") else f"data/{rel}"
                if any(archive_name.startswith(p) for p in EXCLUDED_PREFIXES):
                    continue
                if any(s in "/" + archive_name for s in EXCLUDED_SEGMENTS):
                    continue
                yield os.path.join(dirpath, name), archive_name


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    out_dir, roots = os.path.abspath(sys.argv[1]), sys.argv[2:]
    os.makedirs(out_dir, exist_ok=True)

    boot_path = os.path.join(out_dir, "data.boot.zip")
    music_path = os.path.join(out_dir, "data.music.zip")

    counts = {"boot": 0, "music": 0}
    sizes = {"boot": 0, "music": 0}

    with zipfile.ZipFile(boot_path, "w", zipfile.ZIP_STORED) as boot, \
         zipfile.ZipFile(music_path, "w", zipfile.ZIP_STORED) as music:
        seen = set()
        for path, name in iter_files(roots):
            if name in seen:
                continue
            seen.add(name)

            # The music archive keeps the full path, so the loader can ask for it by the same name
            # the game already uses.
            target, key = (music, "music") if name.startswith("data/music/") else (boot, "boot")
            target.write(path, name)
            counts[key] += 1
            sizes[key] += os.path.getsize(path)

    for key, path in (("boot", boot_path), ("music", music_path)):
        print(f"{key}: {counts[key]} files, {sizes[key] / 1048576:.1f} MB -> {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
