---
name: terse-visual-bug-reports-keep-the-named-symptom
description: "When the user reports a visual regression by name, keep that symptom as the target — a terse follow-up naming another visual feature may be imprecise, so confirm before switching subsystems"
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 5ad88fbb-551f-4012-91b8-733df9894e73
  modified: 2026-09-20T09:25:00.570Z
---

In NFM-World the user often verifies rendering work by eye and reports the result in a line or
two. Those reports name the symptom they want fixed, and the named symptom is the target — not
whatever else happens to be visibly wrong at the same time.

During the shadow-cascade work (Sep 2026) the standing bug was "no shadows". Mid-investigation the
user wrote "visually confirmed, still no outlines", which sent me into the outline subsystem
(`Line.fx`/`LineMesh`) instead. The user then corrected the direction: "outlines are fine, please
fix the shadows not rendering." The detour cost real time on a subsystem that was working.

The lesson generalises past this one bug: when a terse report names a feature other than the
standing symptom, or is ambiguous about which effect is missing, ask which they mean (one short
question) before investing in a different subsystem. Several visual features in this engine look
similar enough to be confused in a sentence — outlines (Line.fx) vs shadow darkening (shadow maps)
vs ambient/diffuse shading are the clearest example — and the user is looking at the running game,
so they can answer immediately.
