---
name: git-status-before-work-discarding-ops
description: "In NFM-World (Auto Mode), always run git status and stash/commit first before any command that could discard uncommitted work, and review staged files for secrets"
metadata: 
  node_type: memory
  pinned: true
  originSessionId: 383298f5-e98e-4ad5-be4f-d1c1d17cb88d
  modified: 2026-09-17T02:58:02.890Z
---

The user requires that, before running any command that could discard uncommitted work in
NFM-World — `git checkout`, `git restore`, `git reset`, `git clean`, `rm -rf` inside the repo,
or restoring a tree from a snapshot — `git status` is run first and anything found there is
stashed (with `-u` for untracked files) or committed. Auto Mode means such commands are not
gated by a permission prompt, so nothing else will catch a destructive command that would
throw away work in progress.

Two related expectations for staging and committing: review what is actually included before
committing (the tree routinely holds several days of unrelated in-progress work across
submodules) and double-check any suspicious file for secrets before it goes in; and never
commit unless the user asks for it.
