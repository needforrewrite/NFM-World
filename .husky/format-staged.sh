#!/bin/sh
# Reformats the staged C# files with ReSharper's CLI, then re-stages them.
# The settings layers are disabled so only the repo's .editorconfig applies.
set -e

files=""
for file in "$@"; do
    [ -f "$file" ] && files="$files $file"
done

[ -n "$files" ] || exit 0

# shellcheck disable=SC2086 # the paths come from git and contain no spaces
dotnet jb cleanupcode $files \
    --profile="Built-in: Reformat Code" \
    --disable-settings-layers="GlobalAll;GlobalPerProduct;SolutionShared;SolutionPersonal" \
    --no-updates

# shellcheck disable=SC2086
git add -- $files
