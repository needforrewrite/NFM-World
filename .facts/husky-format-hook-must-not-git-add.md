# Husky format hook must not `git add`

`.husky/format-staged.sh` runs `csharp-format` over `${staged}`. Husky.Net 0.9.1 executes that as a
`StagedTask` (`src/Husky/TaskRunner/StagedTask.cs`): for a file with both staged and unstaged changes
it writes a temp copy (`<5-char-guid>_Name.cs`) holding only the staged blob, inserts it into the
task's arguments, and afterwards reindexes the formatted temp file with `git hash-object -w` +
`git update-index --cacheinfo`. Non-partial staged files are re-staged by Husky itself.

So the script must only format. An extra `git add -- $files` stages the temp copy (it gets committed,
then shows as a deleted file) and stages the unstaged hunks that Husky's `update-index` then reverts,
leaving the just-committed file listed as modified. `husky run --no-partial` is not a workaround: it
only runs `update-index -g`, which does not stage the reformat.

Line endings: `csharp-format` preserves the EOL it finds, and the repo had no `.gitattributes` with
`core.autocrlf=true`, so 269 `.cs` blobs were CRLF in the index and any formatter pass rewrote the
whole file to LF. `.gitattributes` now pins `*.cs`/`*.csproj` to `text eol=lf`; those blobs were
renormalized once (`git add --renormalize`). Keep new text assets LF or add a rule for them.
