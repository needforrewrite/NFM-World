#!/usr/bin/env bash
# LLM maintained.
# Compiles the generated OpenGL ES 3.0 GLSL under a real ANGLE ES 3.0 context.
#
# The ES output is the one target whose correctness cannot be checked by reading it. Two things
# about it are invisible to inspection and only a driver reports:
#
#   - Whether the source is valid ES 3.0 at all. The pipeline reaches ES through HLSL -> SPIR-V ->
#     GLSL, so an ES 3.10 builtin (fma), a missing precision qualifier, or a construct ES never
#     had will produce source that reads fine and fails to compile.
#   - Whether the two stages link. ES 3.0 matches varyings by name where HLSL matches them by
#     semantic and desktop GLSL by layout(location), so a naming divergence between the stages
#     compiles cleanly in both and fails only at link.
#
# Requires build-all.sh to have run with --dump-sources, which it now always does.
#
# Usage: ./check-es.sh [dump-dir]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dump="${1:-$here/generated}"
probe="$here/../AngleProbe"

if [ ! -x "$probe/bin/Debug/net10.0/AngleProbe.dll" ] && [ ! -f "$probe/bin/Debug/net10.0/AngleProbe.dll" ]; then
    echo "building AngleProbe..." >&2
    dotnet build "$probe/AngleProbe.csproj" -v q --nologo >&2
fi

ls "$dump"/*.Vertex.es.glsl >/dev/null 2>&1 || {
    echo "no *.Vertex.es.glsl in $dump - run ./build-all.sh first" >&2
    exit 1
}

SHADERPOC_GENERATED="$here/generated" dotnet "$probe/bin/Debug/net10.0/AngleProbe.dll" --check-es "$dump"
