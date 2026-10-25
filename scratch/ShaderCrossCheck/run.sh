#!/usr/bin/env bash
# HLSL .fx -> SPIR-V -> GLSL -> sokol-shdc, end to end.
#
# Usage: ./run.sh [shader.fx] [vs_entry] [fs_entry]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"

fx="${1:-$root/Apos.Shapes/Source/Content/apos-shapes.fx}"
vs_entry="${2:-SpriteVertexShader}"
fs_entry="${3:-SpritePixelShader}"

DXC="${DXC:-dxc}"
SPIRV_CROSS="${SPIRV_CROSS:-spirv-cross}"
SOKOL_SHDC="${SOKOL_SHDC:-$root/tools/win32/sokol-shdc.exe}"

# The SM6 branch selects the Texture2D/SamplerState declarations. dxc refuses the
# legacy tex2D intrinsic outright, so a shader that only reaches its sampling
# through tex2D has no path to SPIR-V; see FINDINGS.md.
define="${DEFINE:-SM6}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "==> $fx  [$vs_entry / $fs_entry]  -D$define"
echo "==> workdir $work"

"$DXC" -T vs_6_0 -E "$vs_entry" -spirv -fvk-use-dx-layout -Zpc "-D$define" \
    -I "$(dirname "$fx")" "$fx" -Fo "$work/vs.spv"
"$DXC" -T ps_6_0 -E "$fs_entry" -spirv -Zpc "-D$define" \
    -I "$(dirname "$fx")" "$fx" -Fo "$work/fs.spv"

# --relax-nan-checks keeps spirv-cross from reaching for GL_EXT_spirv_intrinsics
# to model NMin/NMax; sokol-shdc's glslang does not accept spirv_instruction().
# -V asks for Vulkan GLSL, which is what carries the separate texture/sampler
# declarations sokol-shdc requires.
"$SPIRV_CROSS" "$work/vs.spv" -V --version 450 --relax-nan-checks \
    --output "$work/vs.vert"
"$SPIRV_CROSS" "$work/fs.spv" -V --version 450 --relax-nan-checks \
    --output "$work/fs.frag"

node "$here/bridge.mjs" \
    --vs "$work/vs.vert" --fs "$work/fs.frag" \
    --out "$work/shader.glsl" \
    --program "$(basename "${fx%.fx}" | tr -c 'a-zA-Z0-9' '_')"

"$SOKOL_SHDC" -i "$work/shader.glsl" -o "$work/shader.h" \
    -l glsl430:glsl300es:hlsl5:metal_macos:wgsl -f sokol

echo
echo "==> OK -- sokol-shdc reflection:"
# The overview lives in the opening comment; stop at the comment's close so the
# range doesn't run into the generated declarations. CRLF is stripped so the
# terminator matches.
tr -d '\r' < "$work/shader.h" \
    | sed -n '/^    Overview:/,/^ *\*\//p' \
    | grep -v '^\s*\*/' \
    | sed 's/^/    /'

echo
echo "==> keeping artifacts at $work"
trap - EXIT
