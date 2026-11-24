#!/usr/bin/env python3
"""Fetches the pinned emdawnwebgpu package: Dawn's implementation of webgpu.h for Emscripten.

The browser head links WebGPU through emdawnwebgpu rather than wgpu-native, so the C entry
points have to be built for wasm - and they are Dawn's, not ours to vendor. The package is
text-only (834 KB: webgpu.cpp, its headers, and four JS libraries) and versioned by a Dawn
release tag, so it is downloaded instead of checked in.

Two things pin it, and both matter:

  - the URL names a release tag rather than a branch, so the bytes are fixed;
  - the archive's SHA-512 is checked against the digest recorded below. An upstream retag or a
    proxy rewriting the body fails here instead of at a wasm trap six months from now.

The extracted tree is also a build input (see the EmdawnWebGpu targets in the csproj), so a
package that did download once is not fetched again.

Usage: fetch_emdawnwebgpu.py <package-dir>
Exits 0 without downloading when the extraction is already current.
"""

import hashlib
import os
import shutil
import sys
import urllib.request
import zipfile

VERSION = "v20260423.175430"
URL = f"https://github.com/google/dawn/releases/download/{VERSION}/emdawnwebgpu_pkg-{VERSION}.zip"
SHA512 = (
    "42784F70B67197C614322F9FABB0F1DC64228A0DE10E88F99941FA9D29BEE9AD"
    "6683F4651D4EEFD5A7A9FBD1F976EB522B190B683219ED1793E9B531C602FFA6"
)

# The files the build reads. Named rather than globbed so a package that extracted only
# partially is refetched instead of failing later in a confusing place.
REQUIRED = [
    "webgpu/include/webgpu/webgpu.h",
    "webgpu/src/webgpu.cpp",
    "webgpu/src/library_webgpu.js",
    "webgpu/src/library_webgpu_enum_tables.js",
    "webgpu/src/library_webgpu_generated_struct_info.js",
    "webgpu/src/library_webgpu_generated_sig_info.js",
    "emdawnwebgpu.port.py",
    "VERSION.txt",
]


def current(package_dir):
    if not all(os.path.isfile(os.path.join(package_dir, name)) for name in REQUIRED):
        return False
    stamp = os.path.join(package_dir, "VERSION.txt")
    try:
        with open(stamp, encoding="utf-8") as handle:
            return VERSION in handle.read()
    except OSError:
        return False


def main():
    if len(sys.argv) != 2:
        print("usage: fetch_emdawnwebgpu.py <package-dir>", file=sys.stderr)
        return 2
    package_dir = os.path.abspath(sys.argv[1])
    if current(package_dir):
        return 0

    os.makedirs(package_dir, exist_ok=True)
    archive = os.path.join(package_dir, "package.zip")
    print(f"  downloading {URL}")
    with urllib.request.urlopen(URL, timeout=120) as response, open(archive, "wb") as handle:
        shutil.copyfileobj(response, handle)

    with open(archive, "rb") as handle:
        digest = hashlib.sha512(handle.read()).hexdigest().upper()
    if digest != SHA512:
        os.remove(archive)
        print(
            f"emdawnwebgpu {VERSION} failed its hash check.\n"
            f"  expected {SHA512}\n  got      {digest}\n"
            "The download is corrupt or upstream republished the tag. If the package is "
            "deliberately being updated, set SHA512 in this script to the new digest.",
            file=sys.stderr,
        )
        return 1

    # The archive roots everything under emdawnwebgpu_pkg/; strip it so REQUIRED's paths are the
    # package's own paths rather than one directory deeper.
    with zipfile.ZipFile(archive) as bundle:
        for member in bundle.namelist():
            relative = member
            _, _, relative = relative.partition("/")
            if not relative or relative.endswith("/"):
                continue
            target = os.path.join(package_dir, relative)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with bundle.open(member) as source, open(target, "wb") as destination:
                shutil.copyfileobj(source, destination)
    os.remove(archive)

    if not current(package_dir):
        missing = [n for n in REQUIRED if not os.path.isfile(os.path.join(package_dir, n))]
        print(f"emdawnwebgpu {VERSION} extracted without {missing}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
