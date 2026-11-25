#!/usr/bin/env python3
"""Packs the game's data tree into the browser's two archives.

Music is ~90% of the data tree (148 MB against ~11 MB for everything else), so it is split out and
fetched per track at run time instead of being part of the boot payload. The split is by directory:
`data/music/**` goes to one archive, everything else to the other.

The boot archive is deflated. Its bulk is baked font atlases (float MSDF data, which compresses
about as well as text) and it is mounted whole into MEMFS, where the C# ZipArchive inflates each
entry on read - measured 15.3 MB -> 9.9 MB, a third of the boot payload. Deflating it costs a few
milliseconds of inflate against ~200 ms of transfer at the reference connection, and more on a slow
one.

The music archive stays stored. It is read by range request, one track at a time, and a deflated
entry cannot be seeked into: the fetch layer would have to download and inflate the whole 126 MB to
play one track. Its contents are Opus/MP3/tracker modules and already compressed, so deflate would
buy nothing anyway.

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

    # Boot is deflated, music is stored: see the module docstring for why the split is not both.
    with zipfile.ZipFile(boot_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as boot, \
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
