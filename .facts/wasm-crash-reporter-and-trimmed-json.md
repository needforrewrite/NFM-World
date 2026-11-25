# On wasm, a crash *reporter* is a crash

The browser head is trimmed (`PublishTrimmed`), and two of trimming's defaults turn a recoverable
failure into a dead runtime. Both were live at `play.nfmw.net` on 2026-11-25.

## `System.Text.Json` reflection is off by default when trimmed

The published `runtimeconfig.json` carried
`"System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": false`. `AccountManager` uses
`PostAsJsonAsync`/`ReadFromJsonAsync` over plain DTOs with no `JsonSerializerContext`, so the very
first sign-in threw:

```
System.InvalidOperationException: Reflection-based serialization has been disabled for this application.
```

`NFMWorld.Browser.csproj` now sets `<JsonSerializerIsReflectionEnabledByDefault>true</...>`, the same
switch `NFMWorld/NFMWorld.csproj` already carried. Verify with
`dotnet msbuild … -getProperty:JsonSerializerIsReflectionEnabledByDefault` **and** by grepping the
emitted `bin/Release/net12.0/NFMWorld.Browser.runtimeconfig.json` — the property and the file are
what CI ships.

## A crash handler may not start a process

`CrashReportLibrary.StartReporter` built a `ProcessStartInfo` unconditionally. Its **constructor**
throws `PlatformNotSupportedException` on wasm, before `Process.Start` is ever reached, so the
`TaskScheduler.UnobservedTaskException` handler took the whole runtime down. Every handler now goes
through one `Report(...)` that returns `null` from `StartReporter` on `OperatingSystem.IsBrowser()`
and wraps the rest in `try`/`catch`.

## Anything a fire-and-forget UI action awaits must not throw

`MainMenuPhase.OnSignIn` does `_ = SignInAsync(...)`, as do the content controllers and the publish
dialog. A catch filtered to `HttpRequestException or TaskCanceledException` lets an
`InvalidOperationException`/`JsonException` escape, which is an unobserved task fault — the exact
input the handler above was fed. Those call sites now catch `Exception` and report it in the UI.
Widening a catch is not sufficient on its own: it is the reporter fix that makes the class of bug
non-fatal.
