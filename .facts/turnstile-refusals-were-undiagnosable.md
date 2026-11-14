# Turnstile: a solved challenge can still be refused, and why that was invisible

## The trap

`TurnstileVerifier.VerifyAsync` used to return a bare `bool` and swallow every reason. So every
distinct cause — a missing `TURNSTILE_SECRET`, a Siteverify timeout, a token solved on a hostname
`TURNSTILE_HOSTNAMES` does not list, a token solved for a different action, a bad secret — reached
the visitor as the one string "Human verification failed. Please try again.", and reached the
operator as **nothing at all**. The widget passing on the client says only that Cloudflare issued a
token; it says nothing about whether we accept it.

## What a solved-token refusal can actually be

Siteverify is the only authority, and its `error-codes` array is the only place it explains itself.
Observed live:

| Response | Meaning |
|---|---|
| `200 {"success":true,"hostname":...,"action":...}` | token good |
| `200 {"success":false,"error-codes":["invalid-input-response"]}` | token rejected (malformed/expired/replayed) |
| `400 {"success":false,"error-codes":["invalid-input-secret"]}` | **our** secret is wrong |

The 400 case matters: Siteverify answers with the same JSON body for 200 and 400, so a check that
judges `IsSuccessStatusCode` before reading the body reports only "HTTP 400" and loses
`invalid-input-secret`. Read and deserialize the body first, then classify.

`remoteip` is documented as optional/advisory. Community reports (iOS Safari + iCloud Private Relay,
IPv6/IPv4 egress pairs, proxies) claim a solved token can be refused when `remoteip` disagrees with
the solving address. This is **unconfirmed** — Cloudflare has not acknowledged it — so `remoteip`
stays in the request and the refusal log prints the address we sent. Do not drop the parameter as a
"fix" without evidence.

## Two failure classes must not share a message

- Token refused → `TurnstileVerifier.Error`, a Warning, visitor should retry the challenge.
- We could not check (timeout, non-2xx, unparseable body, missing setting, `invalid-input-secret`)
  → `TurnstileVerifier.UnavailableError`, an Error, visitor should wait.

A visitor told "try again" on a broken deployment retries until they give up, and the operator sees
the same sentence they would see for a genuine bot refusal.

## Verifying this area

Headless Chromium never receives a Turnstile token (Cloudflare blocks automation), and Cloudflare's
always-pass test credentials return `hostname: example.com` with **no action**, so in Production they
are refused by the action check — which is correct, and also the cheapest way to see a real refusal
end to end: run the archive with `TURNSTILE_SECRET=1x0000000000000000000000000000000AA`,
`TURNSTILE_HOSTNAMES=<host>`, post any token, and read the log. A deliberately wrong secret shows the
`invalid-input-secret` path. `NFMWorld.Archive.Test/TurnstileTests.cs` covers both with a fake
Siteverify handler, including a `RecordingLoggerProvider` so a test asserts the reason reached the log.
