#!/bin/sh
# Reformats staged C# files with csharp-format, then re-stages them.
# csharp-format reads the repo's .editorconfig itself.
set -e

files=""
for file in "$@"; do
    [ -f "$file" ] && files="$files $file"
done

[ -n "$files" ] || exit 0

# shellcheck disable=SC2086 # the paths come from git and contain no spaces
dotnet csharp-format format $files

# shellcheck disable=SC2086
git add -- $files
