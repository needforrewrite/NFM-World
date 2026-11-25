# The archive must answer the browser build cross-origin

`play.nfmw.net` (GitHub Pages) is a different origin from the archive (`https://nfmw.net`,
`ARCHIVE_PUBLIC_URL` in `.env`). Every `AccountManager` call from the web build is therefore a CORS
request, and until 2026-11-25 nothing under `/auth`, `/assets` or `/tt` carried CORS metadata: an
`OPTIONS /auth/login` preflight answered **405 with no `Access-Control-Allow-Origin`**, so sign-in
could not work at all regardless of the client.

`CorsPolicies.CrossOrigin` (`NFMWorld.Archive/CorsPolicies.cs`, registered in `Program.cs`) is now
applied with `.RequireCors(...)` to three route **groups** — `/auth`, `/assets`, `/tt`. It admits
`GET/HEAD/POST/PUT/PATCH/DELETE` and the headers `Content-Type`, `Authorization`, `Range`,
`If-Range`, and exposes the ranged-download response headers.

Why one policy and not two:

- `AssetEndpoints.CorsPolicy` (the old, GET+Range-only policy for the standalone preview) was a
  strict subset. Two policies would need per-endpoint precedence rules; one is enough.
- It stays group-scoped rather than `app.UseCors(policy)` globally so the cookie-authenticated Razor
  pages are still unreadable from a third-party origin.
- Credentials remain off. The client sends its session as an `Authorization: Bearer` header, so
  `AllowAnyOrigin` cannot expose another visitor's data, and `AllowAnyOrigin` forbids
  `AllowCredentials` anyway.

`CorsOrigins` still narrows it (default `*`, comma-separated origins otherwise).

Regression coverage: `AssetEndpointTests.Cors_Preflight_AllowsSignInFromTheBrowserBuild` and
`Cors_AuthenticatedRequest_IsReadableCrossOrigin`.

The deployed container must be rebuilt for any of this to apply — the archive is a `docker compose`
service, not a Pages deploy.
