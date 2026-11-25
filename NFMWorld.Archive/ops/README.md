# Running the archive

Single ASP.NET Core process, SQLite in WAL mode, uploaded files on disk. One node is the
intended deployment for the expected user count; everything below assumes that.

## Website styling

The Razor site uses [Blades CSS](https://github.com/anyblades/blades) 3.1.1, vendored under
`wwwroot/lib/blades/` with its MIT license. It is served locally, not from a CDN; no npm install
or frontend build is required. `wwwroot/css/archive.css` contains the small site-specific layout rules.
To update Blades, replace the stylesheet and license together from a pinned `@anyblades/blades` release.

## Configuration

Set through the `Archive__*` environment variables (double underscore nests).

| Variable                                                                  | Purpose                                                                           |
|---------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| `Archive__Database`                                                       | SQLite connection string, e.g. `Data Source=/data/archive.db`                     |
| `Archive__StorageRoot`                                                    | Directory for uploaded files                                                      |
| `Archive__PublicBaseUrl`                                                  | Origin clients use; must be a **separate origin** from the website                |
| `Archive__GamePreviewUrl`                                                 | Root the browser build is deployed at, e.g. `https://game.nfmw.net`; the asset and browse pages link each upload into its `index.html?hash=…` preview. Empty hides the preview links |
| `Archive__TrustedServerKeys`                                              | `id=base64secret` list the lobby signs results with                               |
| `Archive__ValidatorPath`                                                  | Path to `NFMWorld.Archive.Validator`; uploads and time-trial validation are refused without it |
| `Archive__ValidatorWorkingDirectory`                                      | Directory holding the game `data/` tree the validator needs (defaults to the executable's folder) |
| `Archive__DiscordClientId` / `DiscordClientSecret` / `DiscordRedirectUri` | Discord sign-in                                                                   |
| `Archive__ReadOnly`                                                       | Disables registration and uploads                                                 |
| `Archive__DeletionGracePeriod`                                           | Grace between a deletion request and the sweep, `DD.HH:MM:SS` or days (default 30 days) |
| `Archive__CloudflareZoneId` / `Archive__CloudflareApiToken`              | Optional edge purge when an asset is hidden, replaced or deleted (see [Cloudflare caching](#cloudflare-caching)) |
| `Archive__CorsOrigins`                                                    | Comma-separated origins allowed to read `/assets/{hash}` and `/assets/meta/{hash}`; default `*` (the browser's standalone preview page reads them cross-origin) |

Relative validator paths are resolved against the content root. In **Development** only, leaving
`Archive__ValidatorPath` unset makes the archive use `NFMWorld.Archive.Validator` built beside this
project (`NFMWorld.Archive.Validator/bin/<Configuration>/net11.0/`), so `dotnet build` that project
and restart the archive. A deployed archive must set the variable; otherwise startup logs that
uploads and time trials will be refused.

### Turnstile

In Production and Staging, set `TURNSTILE_SECRET` to the secret for the existing widget with public sitekey
`0x4AAAAAAF8y76GGmumUX1_K`, and `TURNSTILE_HOSTNAMES` to a comma-separated allowlist
of archive website hostnames (no schemes, ports, paths or wildcards). Obtain the secret from
Cloudflare yourself and supply it through deployment secrets; never commit it or expose it to clients.
Configure the same production hostnames on the existing widget. Do not add localhost to the production
widget or allowlist; loopback hostnames are refused outside ASP.NET Core Development.

**A listed hostname covers its subdomains**, matching Cloudflare's own rule — adding `nfmw.net` to the
widget authorizes `dev.nfmw.net` too, and this check accepts it for the same reason. The two lists must
still agree in scope: a hostname the widget authorizes but `TURNSTILE_HOSTNAMES` does not cover produces a
solved challenge that is then refused, which the visitor sees as "human verification failed" even though
the widget passed. Subdomains of a listed name are covered; a sibling domain (`nfmw.dev` next to
`nfmw.net`) is not, and needs its own entry in both places.

Without either variable outside Development, registration and web sign-in fail closed. Every registration path, including
`POST /auth/register` (JSON field `cf-turnstile-response`, action `register`) and Discord account creation,
requires a verified challenge. `/account/login?flow=signin|register|discord` renders only the selected form
and one widget, with the corresponding action. Switching flows loads a fresh page and challenge.
Discord must start through the challenged web form; the old GET link only redirects there.
Native `POST /auth/login` remains password-based and throttled, without a Turnstile requirement.
Siteverify has a 10-second timeout; errors, expired/replayed tokens, unexpected actions and hostnames
all reject the attempt. The login page requires Cloudflare's JavaScript and iframe to load.

Every refusal is logged as a warning naming the reason — the Siteverify `error-codes` entry, the
action the token was solved for, or the hostname it was solved on together with
`TURNSTILE_HOSTNAMES` — and a failure on our side (timeout, non-2xx, unparseable body, missing
setting) is logged as an error and shown to the visitor as "unavailable right now" rather than
"failed, please try again". Startup also warns when the challenge is misconfigured, because that
otherwise looks exactly like a failed challenge from the outside.

For local development, `dotnet run --project NFMWorld.Archive/NFMWorld.Archive.csproj` uses the checked-in
launch profile (`Development`, `http://localhost:5288`). That environment uses Cloudflare's always-pass test
sitekey and secret automatically; no `TURNSTILE_*` settings are needed. Verification still calls Siteverify
and rejects missing tokens and network/errors, but skips hostname/action matching because dummy responses
report `example.com` without an action. A custom IDE run configuration must also select `Development`.
Never run a public deployment with `ASPNETCORE_ENVIRONMENT=Development` or `DOTNET_ENVIRONMENT=Development`;
production must use the real widget and configured hostname/action checks.

### Generating keys

There are two independent keys in the system. Generate each with the same tool:

```sh
dotnet run --project NFMWorld.GenerateHmacKey            # key id defaults to "primary"
dotnet run --project NFMWorld.GenerateHmacKey archive    # a named key
```

It prints a random 256-bit secret, base64-encoded, and the environment lines for every server
that needs it. Run it twice: one key for the **archive** trust level and one for the **game
master** trust level. They are not interchangeable — the archive key lets its holder write
ratings, and only the lobby should have it.

**Archive ↔ lobby** (the key that signs `POST /results/match`):

| Where   | Variable                                                                               |
|---------|----------------------------------------------------------------------------------------|
| Lobby   | `ARCHIVE_URL` (also enables token checks on join), `HMAC_KEY_ID`, `ARCHIVE_SECRET_KEY` |
| Archive | `Archive__TrustedServerKeys=<keyId>=<secret>`                                          |

**Lobby ↔ game master** (the key that signs `/create-race` and `/race-ended`):

| Where       | Variable                         |
|-------------|----------------------------------|
| Lobby       | `HMAC_KEY_ID`, `HMAC_SECRET_KEY` |
| Game master | `GM_HMAC_KEYS=<keyId>=<secret>`  |

The same `HMAC_KEY_ID`/`HMAC_SECRET_KEY` pair may be reused for both trust levels if you keep it
to one deployment, but never hand the archive key to a game master: the game master runs on
machines you do not control, and that key can write ratings directly.

`Archive__TrustedServerKeys` and `GM_HMAC_KEYS` both take a comma-separated list
(`primary=secret,primary-v2=newsecret`), which is what a rotation relies on: add the new key
everywhere, switch the lobby's `HMAC_KEY_ID`/`HMAC_SECRET_KEY` to it, then drop the old one once
no request signed with it can still be in flight.

## Why uploads are on another origin

Served from this app's own origin, an uploaded SVG or HTML document would run as script with the
viewer's session cookie attached. `Archive__PublicBaseUrl` should point at a host that serves the
storage directory with `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`;
the in-app `/assets/{hash}` endpoint is a development convenience.

## Cloudflare caching

`GET /assets/<sha256>` is the hot public download: content-addressed, no cookies, no `Vary`, and it
answers with

```
Cache-Control: public, max-age=31536000, immutable
CDN-Cache-Control: max-age=31536000
```

so an edge may keep it for a year. **The path has no file extension, and Cloudflare's Standard cache
level only caches by extension — without a Cache Rule Cloudflare never caches these downloads.**
The rules below are what turn the origin into a one-time fetch per asset.

### Cache Rules (Caching → Cache Rules), in order

The first matching rule wins, so keep this order. `matches` (regex) needs a Business or Enterprise
plan, so the download rule uses `starts_with` plus a length check; `/assets/` is 8 characters and the
hash is 64, so a real download is exactly 72 characters.

|#|When|Settings|
|---|---|---|
|1|`http.request.method eq "GET" and starts_with(http.request.uri.path, "/assets/") and len(http.request.uri.path) eq 72`|Cache eligibility **Eligible for cache** · Edge TTL **Respect origin TTL** · Browser TTL **Respect origin TTL** · Cache key: query string **Ignore** · Cache Deception Armor **On**|
|2|`starts_with(http.request.uri.path, "/assets/")`|Cache eligibility **Bypass cache** — search, `/meta/<hash>` and `/mine` must stay live; the metadata check is what sees a takedown|
|3|`starts_with(http.request.uri.path, "/auth/") or starts_with(http.request.uri.path, "/account") or starts_with(http.request.uri.path, "/moderation") or starts_with(http.request.uri.path, "/results") or http.request.uri.path eq "/logout" or http.request.uri.path eq "/health"`|Cache eligibility **Bypass cache**; these carry or check a session|

The length test accepts any 64 characters after `/assets/`, not only hex; nothing links or requests
such a path, and the origin answers `400` without a cacheable body. If you add an `/assets/` endpoint
whose path is exactly 72 characters, move it above rule 1 with its own bypass rule.

Do not add a "Cache Everything" page rule: the HTML and JSON are per-viewer (session cookie,
antiforgery token, moderation state).

### Zone settings

- Caching → Configuration → **Cache Level: Standard**; the rules above override it per path.
- **Browser Cache TTL: Respect Existing Headers** — the origin already sends a year.
- **Origin Cache Control: On**; harmless here because the rule pins the edge TTL.
- **Tiered Cache: On** — a cold PoP asks the upper tier instead of the origin, so an asset costs one
  origin fetch for the whole network rather than one per PoP.
- **Cache Reserve** (paid) if cold assets must survive eviction for the full year. The current asset
  sizes keep the hot set resident without it.
- **WAF**: skip bot-fight/challenge rules for `GET /assets/*`. The native client has no JavaScript
  and sends no cookies, so a challenged download fails for every player.

### Purging when an asset changes

A year-long TTL is only safe because the archive purges. Set `Archive__CloudflareZoneId` and
`Archive__CloudflareApiToken` (`ARCHIVE_CLOUDFLARE_ZONE_ID`/`ARCHIVE_CLOUDFLARE_API_TOKEN` in the
compose stack); both empty means no outbound request at all. The token needs only
**Zone → Cache Purge → Purge** on that zone, and `Archive__PublicBaseUrl` must be the `https://`
hostname players download from, because purge URLs are built from it.

Purges are per URL (`<origin>/assets/<hash>`), batched at Cloudflare's 30-URL limit, and run after
the database write for:

- `DELETE /assets/{id}` and the website's delete — the asset's hash;
- `PUT /assets/{id}/content` and the website's replace — the **previous** hash;
- `PATCH /assets/{id}` (name, description, `isPublic`, `isTakedown`) and the edit page's save — the
  hash, because the download's `Content-Disposition` carries the name and a hidden row must stop
  answering;
- moderation takedown/restore and the bulk `unlist`/`relist` — every hash the account owns.

A purge failure is logged (`[Cache] Cloudflare refused …`) and never fails the action.

A purge affects the edge only. It cannot recall bytes a player already has: the game's content cache
is hash-addressed and never revalidates, and a browser that downloaded the file keeps it for a year.

### What the numbers mean

`DownloadCount` is incremented at the origin, so after this it counts **cache misses**, not
downloads. Use Cloudflare's cache analytics for request volume; the archive's number becomes "first
fetch, and re-fetch after a purge". Verify with `curl -sI https://<archive>/assets/<hash>`:
`cf-cache-status: MISS` on the first request, `HIT` afterwards.

`/tt/replay/{id}` is immutable per id but has no purge hook, so cache it with a short edge TTL
(a day) or leave it uncached; replays are small and far less hot than assets.

## Backup and restore

Files are the source of truth and are content-addressed; the database only indexes them.

```sh
sqlite3 /data/archive.db ".backup '/backups/archive-$(date +%F).db'"
rsync -a /data/storage/ /backups/storage/
```

Restore is: stop the service, put the database and `storage/` back, start. Practise this before
it matters — a single node with a single disk is the whole failure domain.

## Migration

The app runs `Database.Migrate()` at startup. That is fine for one instance; with more than one,
migrate out of band so two processes do not race the migration lock.

## Validation isolation

Time-trial simulation is synchronous and cannot be cancelled, so it runs in a separate process
with a wall clock (`Archive__ValidationTimeout`). Radpack and legacy-model uploads use the same
process. The archive refuses both when the validator is not configured rather than trusting the
client's claimed time or file contents. The Docker image publishes the validator to `/validator`
and sets `Archive__ValidatorPath`, so the compose stack needs no extra configuration; deployments
outside the image must point the variable at the built executable. A missing validator (or a
missing file at the configured path) is logged at startup.

## Moderation

Two roles exist, `User`, `Moderator` and `Admin`, stored as an integer in the `Users.Role`
column. A moderator may kick, ban, mute, unlist and relist another account's content; an admin may
also rename an account, set its password, change its role and erase it. A moderator cannot act on
an admin, and nobody can act on their own account.

**Bootstrapping the first admin** — run against the same archive volume, before or while the stack
is up:

```sh
docker compose run --rm archive --set-role alice admin
docker compose run --rm archive --list-roles
```

The command migrates the database first, so it works on a fresh volume before the server has ever
started. `--set-role <user> <role>` accepts `admin`, `Admin` or `2`; `--list-roles` prints every
account. An unparseable role prints the accepted names and exits 2.

### Chat commands

A lobby chat message starting with `/` is a staff-only command; a non-staff sender gets
"Unknown command." and the text is never relayed. Verbs: `kick`, `ban`, `mute` (a duration such as
`30m`, `7d`, `12h` selects the temporary form), `unban`, `unmute`, `unlist`, `relist`, `rename`,
`deleterequest`, `deletecancel`. `setpassword`, `setrole` and `deleteerase` are deliberately not
chat verbs: those changes belong on the website. A kick purges the target's recent chat and
disconnects their live connection.

### Ban and deletion semantics

A ban blocks the lobby, race admission and uploads but not website sign-in. A banned account's
`/auth/verify` reports `banned: true`, so the lobby refuses admission; the website's login and
security pages do not check `BannedUntil`. A `tempkick` (`KickedUntil`) blocks lobby admission for
a cooldown without blocking sign-in or uploads.

Account deletion is a grace-period sweep: `/account/deletion` requests it, `Archive__DeletionGracePeriod`
(default 30 days) elapses, and the hosted sweeper erases the account. Erasure removes the identity —
name, password, Discord link, sessions, ratings and time-trial replays — while published uploads
stay online under an anonymous `deleted-<id>` owner, so a stage or car another player saved keeps
working. The `User` row survives as a tombstone; no password, role, ban, mute or session remains.
