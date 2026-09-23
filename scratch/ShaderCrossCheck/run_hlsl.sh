#!/usr/bin/env bash
# HLSL .fx -> SPIR-V (glslang's HLSL front-end) -> GLSL -> sokol-shdc.
#
# This is the DXC-free variant of run.sh: the front-end is glslang's own HLSL
# parser, which is what sokol-shdc already links for its GLSL front-end, and
# what shaderc (Silk.NET.Shaderc) exposes to .NET. Removing DXC removes the
# macOS blocker, because glslang/shaderc ship for every RID the game targets.
#
# Usage: ./run_hlsl.sh <shader.fx> <vs_entry> <fs_entry> [vs_block] [fs_block]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"

fx="$1"; vs_entry="$2"; fs_entry="$3"
vs_block="${4:-_Global}"
fs_block="${5:-fs_Global}"

GLSLANG="${GLSLANG:-glslangValidator}"
SPIRV_CROSS="${SPIRV_CROSS:-spirv-cross}"
SOKOL_SHDC="${SOKOL_SHDC:-$root/tools/win32/sokol-shdc.exe}"

define="${DEFINE:-SM6=1}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dir="$(dirname "$fx")"
name="$(basename "${fx%.fx}" | tr -c 'a-zA-Z0-9' '_')"

# glslang needs a value in -D<def>=<value>; a bare -DSM6 makes '#elif SM6' an
# empty expression and fails with 'bad expression' at the first #elif.
"$GLSLANG" -V -D -S vert -e "$vs_entry" "-D$define" -I"$dir/" "$fx" -o "$work/vs.spv"
"$GLSLANG" -V -D -S frag -e "$fs_entry" "-D$define" -I"$dir/" "$fx" -o "$work/fs.spv"

# Same flags as the DXC path: -V for the separate texture/sampler declarations
# sokol-shdc requires, --relax-nan-checks to keep GL_EXT_spirv_intrinsics out.
"$SPIRV_CROSS" "$work/vs.spv" -V --version 450 --relax-nan-checks > "$work/vs.vert"
"$SPIRV_CROSS" "$work/fs.spv" -V --version 450 --relax-nan-checks > "$work/fs.frag"

# glslang names the two sides of a varying differently and inconsistently
# (_entryPointOutput_X / p_X / input_X depending on stage and how it's used);
# normalize both to spirv-cross's out_var_X / in_var_X so bridge.mjs's existing
# renameVaryings applies unchanged.
sed -E 's/\b(_entryPointOutput|out_var)_([A-Za-z0-9_]+)\b/out_var_\2/g' \
    "$work/vs.vert" > "$work/vs.norm"
sed -E 's/\b(input_var|in_var|p|input)_([A-Za-z0-9_]+)\b/in_var_\2/g' \
    "$work/fs.frag" > "$work/fs.norm"

node "$here/bridge.mjs" \
    --vs "$work/vs.norm" --fs "$work/fs.norm" \
    --out "$work/shader.glsl" \
    --vs-block "$vs_block" --fs-block "$fs_block" \
    --program "$name"

"$SOKOL_SHDC" -i "$work/shader.glsl" -o "$work/shader.h" \
    -l glsl430:hlsl5:metal_macos:wgsl -f sokol

echo
echo "==> OK -- sokol-shdc reflection:"
tr -d '\r' < "$work/shader.h" \
    | sed -n '/^    Overview:/,/^ *\*\//p' \
    | grep -v '^\s*\*/' \
    | sed 's/^/    /'
