---
name: dont-forward-user-email
description: "The user's email identifies them to me only — never send it to an unrelated service in a header, URL, or payload unless they explicitly ask"
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 383298f5-e98e-4ad5-be4f-d1c1d17cb88d
  modified: 2026-09-17T02:58:03.942Z
---

The user's email address (hornsdave4@gmail.com) is for identifying the user to me only. It must
not be sent to any unrelated service — not as a header, not in a URL, not in a payload — unless
the user explicitly asks for that specific use. The default, when a tool or API wants a contact
address and the user has not asked for theirs to be used, is to leave it out or ask.
