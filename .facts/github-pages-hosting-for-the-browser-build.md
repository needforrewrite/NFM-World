# The browser build is GitHub Pages at play.nfmw.net

`.github/workflows/web.yml` publishes `NFMWorld.Browser` to Pages on every push to `master`. The
custom domain is `play.nfmw.net` — **not** `game.nfmw.net`, which is the game master's host, and not
`www`, which does not resolve.

Measured 2026-11-25 against the live site:

|Check|Result|
|---|---|
|`https://play.nfmw.net/`|200, real page|
|TLS|`CN=play.nfmw.net`, Let's Encrypt, `https_enforced: true`|
|`application/wasm`|**gzip**, not raw: composite `NFMWorld.Browser.r2r.*.wasm` 17.67 MB, `dotnet.native.*.wasm` 13.77 MB|
|`data.boot.zip`|15.19 MB, **uncompressed** (GitHub Pages does not compress `application/x-zip-compressed`)|
|`data.music.zip`|126.79 MB, uncompressed, `206` + `Content-Range` + `Access-Control-Allow-Origin: *` on a Range request|

So Pages is not "everything raw": wasm is compressed, zips are not. Zips are already compressed
(`ZIP_STORED` in `pack_data.py`), so nothing is lost there.

## Gotchas

- **A newly pointed custom domain shows "Site not found" from GitHub for a while.** Three separate
  causes, all transient: DNS still on the old record, GitHub's certificate not yet issued
  (`https_certificate: null` — re-`PUT /repos/{owner}/{repo}/pages` with the cname to re-run the
  check, then it flips to `approved`), and a stale 404 cached on GitHub's own Fastly (`X-Cache: HIT`,
  `Cache-Control: max-age=600`). `curl --resolve play.nfmw.net:80:185.199.108.153 http://play.nfmw.net/`
  answers whether GitHub itself is serving before blaming DNS.
- **`https_enforced` and the certificate are independent.** The site serves fine over HTTP with a
  `*.github.io` cert, which fails every browser's hostname check; enforcing HTTPS before the cert is
  issued just breaks the site.
- **The proxied path hid the truth.** While `play.nfmw.net` was Cloudflare-proxied, the edge returned
  a cached GitHub 404 for `/` while `/index.html` was 200 — so testing only the root suggested the
  deployment was absent.
- **Pages has no 25 MiB per-file cap.** `data.music.zip` at 126.79 MB serves fine. (Cloudflare Pages
  does cap static assets at 25 MiB; that host was considered and rejected.)
- **`AssetPreview.Url` emits `{root}/index.html?hash=…`**, so the root path is not what the archive
  links to. Both work now, but `/index.html` is the one that must be verified.

## Deploying

Pushing to `master` is the only trigger; `workflow_dispatch` also works. A run is ~20 min. The job
needs Windows (the shader task P/Invokes win-x64 shaderc/spirv-cross) and the .NET 12 nightly in both
jobs.
