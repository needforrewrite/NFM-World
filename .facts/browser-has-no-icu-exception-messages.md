# The browser build has no ICU, so framework exception messages are resource keys

`InvariantGlobalization` is on for the wasm head, so every framework string that would come from a
resource table arrives as its key instead of a sentence. A user-visible `ex.Message` therefore reads

```
net_http_message_not_success_statuscode_reason, 404, Not Found
```

Confirmed on a 404 from `ArchiveContentClient.DownloadAsync` (its `EnsureSuccessStatusCode`) in the
radpack preview page, which shows the message in a dialog.

Only framework-thrown exceptions are affected. Messages this tree writes itself (the `InvalidDataException`s
in `RadpackReader`, `ContentCache`, the preview phase) are plain strings and safe to show.

Format by exception kind, not by `Message`:

- `HttpRequestException { StatusCode: { } status }` → `$"The archive answered {(int)status} {status}."`
- `HttpRequestException` (no status) → "Could not reach the archive."
- `TaskCanceledException` → "The archive did not answer in time."
- anything else → `ex.Message`, which is ours in practice.

`ArchivePreviewPhase.DescribeFailure` is the reference implementation. Logging the raw `Message` is
fine; only the string a player reads needs this.
