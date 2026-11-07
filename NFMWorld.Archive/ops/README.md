# Running the archive

Single ASP.NET Core process, SQLite in WAL mode, uploaded files on disk. One node is the
intended deployment for the expected user count; everything below assumes that.

## Configuration

Set through the `Archive__*` environment variables (double underscore nests).

| Variable | Purpose |
|---|---|
| `Archive__Database` | SQLite connection string, e.g. `Data Source=/data/archive.db` |
| `Archive__StorageRoot` | Directory for uploaded files |
| `Archive__PublicBaseUrl` | Origin clients use; must be a **separate origin** from the website |
| `Archive__TrustedServerKeys` | `id=base64secret` list the lobby signs results with |
| `Archive__ValidatorPath` | Path to `NFMWorld.Archive.Validator`; time-trial validation is refused without it |
| `Archive__ValidatorWorkingDirectory` | Directory holding the game `data/` tree the validator needs |
| `Archive__DiscordClientId` / `DiscordClientSecret` / `DiscordRedirectUri` | Discord sign-in |
| `Archive__ReadOnly` | Disables registration and uploads |

Generate a server key with `NFMWorld.GenerateHmacKey`, and give the same value to the lobby as
`ARCHIVE_SECRET_KEY`. A game master must **not** receive it: it can only report races to the
lobby, which is what stops one GM fabricating results.

## Why uploads are on another origin

Served from this app's own origin, an uploaded SVG or HTML document would run as script with the
viewer's session cookie attached. `Archive__PublicBaseUrl` should point at a host that serves the
storage directory with `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`;
the in-app `/assets/{hash}` endpoint is a development convenience.

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
with a wall clock (`Archive__ValidationTimeout`). The archive refuses to rank a time trial when
the validator is not configured rather than trusting the client's claimed time.
