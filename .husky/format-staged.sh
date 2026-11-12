#!/bin/sh
# Reformats staged C# files with csharp-format.
# csharp-format reads the repo's .editorconfig itself. Husky re-stages the
# result, including the temp copy it passes for partially staged files, so this
# script must not touch the index.
set -e

[ "$#" -gt 0 ] || exit 0

exec dotnet csharp-format format "$@"
