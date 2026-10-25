#!/usr/bin/env bash
# LLM maintained.
# Regenerates every bundle from the migrated shader sources.
#
# The entry-point names differ per shader (the D3D9-era ones use VertexShaderFunction/
# PixelShaderFunction, the newer ones MainVS/MainPS), so they are listed explicitly rather
# than guessed. A real port would read them from the build item's metadata, which is what
# the FX `technique` block's `compile VS_SHADERMODEL Foo()` lines record.
#
# Usage: ./build-all.sh [output-dir]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
migrated="${FXMIG:-/tmp/fxmig}"
out="${1:-$here/generated}"

if [ ! -d "$migrated" ]; then
    echo "no migrated sources at $migrated - run: node migrate-fx.js $migrated" >&2
    exit 1
fi

mkdir -p "$out"

# shader:vs_entry:fs_entry
shaders=(
    "Ground:VertexShaderFunction:PixelShaderFunction"
    "ImGui:VertexShaderFunction:PixelShaderFunction"
    "Line:MainVS:MainPS"
    "Mountains:VertexShaderFunction:PixelShaderFunction"
    # Nvg has four pixel entry points, one per technique; the reflected bindings are the same
    # for all of them, so the simplest is compiled here and the others by the real build.
    "Nvg:VSMain:PSMainSimple"
    "Particle:VertexShaderFunction:PixelShaderFunction"
    "Poly:MainVS:MainPS"
    "Sky:VertexShaderFunction:PixelShaderFunction"
)

failed=0
for entry in "${shaders[@]}"; do
    IFS=: read -r name vs fs <<< "$entry"
    src="$migrated/$name.fx"
    if [ ! -f "$src" ]; then
        echo "SKIP  $name (no $src)"
        continue
    fi
    # --dump-sources as well as the bundle: the ES GLSL is also written out per stage, because
    # compiling it is the only way to know the pass produced something a GLES3 driver accepts.
    # check-es.sh picks those up.
    if (cd "$out" && dotnet "$here/bin/Debug/net11.0/ShaderPoc.dll" "$src" "$vs" "$fs" --dump-sources >/dev/null); then
        echo "ok    $name"
    else
        echo "FAIL  $name"
        failed=1
    fi
done

exit $failed
