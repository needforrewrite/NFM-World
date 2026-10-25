#!/usr/bin/env bash
# Measure the runtime D3D11 compile cost sokol_gfx would pay when handed HLSL
# source instead of precompiled bytecode.
#
# sokol_gfx calls D3DCompile (d3dcompiler_47.dll) synchronously inside
# sg_make_shader, so this is startup time on the calling thread. Flags mirror
# sokol_gfx's release path: D3DCOMPILE_OPTIMIZATION_LEVEL3 -> fxc /O3.
#
# The point of the measurement: if you let sokol_gfx compile at runtime you do
# NOT need dxc/fxc at build time on any platform, but you pay this at startup.
#
# Usage: ./time_runtime_compile.sh [timeout-seconds-per-stage]
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -W)"
G="${GLSLANG:-glslangValidator}"
S="${SPIRV_CROSS:-spirv-cross}"
FXC="${FXC:-fxc.exe}"
BR="$root/scratch/ShaderCrossCheck/bridge.mjs"
SHDC="${SOKOL_SHDC:-$root/tools/win32/sokol-shdc.exe}"
LIMIT="${1:-60}"
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL="*"

work="$(cd "$(mktemp -d)" && pwd -W)"
trap 'rm -rf "$work"' EXIT

# Pull one backend's source out of a generated sokol-shdc header.
extract() {
    node -e '
        const fs = require("fs");
        const t = fs.readFileSync(process.argv[1], "utf8");
        const i = t.indexOf("static const uint8_t " + process.argv[2] + "[");
        if (i < 0) process.exit(1);
        const b = t.indexOf("{", i), e = t.indexOf("};", b);
        const bytes = t.slice(b+1, e).split(",").map(s => s.trim()).filter(s => s).map(Number);
        fs.writeFileSync(process.argv[3], Buffer.from(bytes).toString("utf8"));
    ' "$1" "$2" "$3"
}

# One shader: build the annotated GLSL, generate the header, then time fxc on
# each stage exactly as sokol_gfx would drive it.
one() {
    local label="$1" fx="$2" vs="$3" fs="$4"
    local dir; dir="$(dirname "$fx")"
    local h="$work/$label.h"

    if ! "$G" -V -D -S vert -e "$vs" -DSM6=1 -I"$dir/" "$fx" -o "$work/$label.v.spv" >/dev/null 2>&1; then
        printf "%-12s %s\n" "$label" "VS source does not compile"; return; fi
    if ! "$G" -V -D -S frag -e "$fs" -DSM6=1 -I"$dir/" "$fx" -o "$work/$label.f.spv" >/dev/null 2>&1; then
        printf "%-12s %s\n" "$label" "FS source does not compile"; return; fi

    "$S" "$work/$label.v.spv" -V --version 450 --relax-nan-checks 2>/dev/null \
        | sed -E 's/\b(_entryPointOutput|out_var)_([A-Za-z0-9_]+)\b/out_var_\2/g' > "$work/$label.v.glsl"
    "$S" "$work/$label.f.spv" -V --version 450 --relax-nan-checks 2>/dev/null \
        | sed -E 's/\b(input_var|in_var|p|input)_([A-Za-z0-9_]+)\b/in_var_\2/g' > "$work/$label.f.glsl"
    node "$BR" --vs "$work/$label.v.glsl" --fs "$work/$label.f.glsl" \
        --out "$work/$label.glsl" --program "$label" --vs-block _Global --fs-block fs_Global >/dev/null 2>&1 || { printf "%-12s %s\n" "$label" "bridge failed"; return; }
    "$SHDC" -i "$work/$label.glsl" -o "$h" -l hlsl5 -f sokol >/dev/null 2>&1 || { printf "%-12s %s\n" "$label" "shdc failed"; return; }

    extract "$h" vs_source_hlsl5 "$work/$label.v.hlsl" || { printf "%-12s %s\n" "$label" "no HLSL5 VS"; return; }
    extract "$h" fs_source_hlsl5 "$work/$label.f.hlsl" || { printf "%-12s %s\n" "$label" "no HLSL5 FS"; return; }

    local vsz fsz
    vsz=$(stat -c%s "$work/$label.v.hlsl"); fsz=$(stat -c%s "$work/$label.f.hlsl")

    time_stage() { # profile hlsl
        local t0 t1
        t0=$(date +%s%N)
        if ! timeout "$LIMIT" "$FXC" /T "$1" /E main /O3 /Fo "$work/o.fxo" "$2" >/dev/null 2>&1; then
            echo ">${LIMIT}s"; return; fi
        t1=$(date +%s%N); echo "$(( (t1-t0)/1000000 ))"
    }
    local tv tf
    tv=$(time_stage vs_5_0 "$work/$label.v.hlsl")
    tf=$(time_stage ps_5_0 "$work/$label.f.hlsl")

    printf "%-12s  VS %8s B %9s ms   FS %9s B %10s ms\n" "$label" "$vsz" "$tv" "$fsz" "$tf"
}

echo "                     source size              startup compile (fxc /O3 = sokol_gfx release)"
echo "-----------------------------------------------------------------------------------------"
one sky       C:/Users/Maxine/AppData/Local/Temp/fx/Sky.fx        VertexShaderFunction PixelShaderFunction
one particle  C:/Users/Maxine/AppData/Local/Temp/fx/Particle.fx   VertexShaderFunction PixelShaderFunction
one ground    C:/Users/Maxine/AppData/Local/Temp/fx/Ground.fx     VertexShaderFunction PixelShaderFunction
one mountains C:/Users/Maxine/AppData/Local/Temp/fx/Mountains.fx  VertexShaderFunction PixelShaderFunction
one imgui     C:/Users/Maxine/AppData/Local/Temp/fx/ImGui_m.fx    VertexShaderFunction PixelShaderFunction
one nvg       C:/Users/Maxine/AppData/Local/Temp/fx/Nvg_m.fx      VSMain PSMainFillGradient
one poly      C:/Users/Maxine/AppData/Local/Temp/fx/Poly_f.fx     MainVS MainPS
one line      C:/Users/Maxine/AppData/Local/Temp/fx/Line_f.fx     MainVS MainPS
one apos      C:/Users/Maxine/AppData/Local/Temp/fx/apos-shapes.fx SpriteVertexShader SpritePixelShader
