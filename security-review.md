# SimLetsFly — Security Review

**Date:** July 6, 2026
**Scope:** Web app (GitHub Pages), Supabase backend (tables, RLS, edge functions), CheckRide Windows client (C#/.NET 8)
**Reviewer perspective:** Security manager — issues, concerns, and missed best practices. No code changes made.

---

## Executive Summary

The project has a reasonable security posture for a solo-developer PWA: DPAPI-encrypted session storage in the desktop client, JWT verification and ownership checks in the sensitive edge function, secrets kept in Supabase env vars (never committed — verified against git history), and HTML escaping in the two pages that render user content. The most significant risks cluster around **public-read data exposure** (`checkride_results`, including raw debug logs), **unauthenticated edge functions that spend money or quota** (`generate-debrief` has no rate limit; `pull-route` exposes a paid third-party API), and **weak share-link tokens**. None are emergencies, but several should be addressed before the CheckRide beta widens the user base.

---

## Findings

### HIGH

#### H-1. `log_text` raw debug logs are publicly readable
`checkride_results` has a public SELECT policy (`USING (true)`), and the CheckRide client uploads the **entire raw tick log** into the `log_text` column when the user opts in (`SupabaseClient.cs` → `UploadCheckRideAsync`). Raw logs routinely contain local file paths (which embed Windows usernames), machine/sim configuration details, and timestamps that reveal when the user is at their computer. Anyone with the anon key (which is public by design) can enumerate and read every log.

**Recommendation:** Exclude `log_text` from the public SELECT (column-level grants, a view, or move logs to a separate owner-only table). Alternatively make the public policy a `SELECT` on a whitelist of display columns only.

#### H-2. `generate-debrief` has no rate limiting or spend cap — direct cost exposure
The function is owner-gated and idempotent per run, but a user controls how many runs exist: the REST insert to `checkride_results` accepts anything a valid JWT signs. A hostile (or just enthusiastic) free-tier user can script *insert run → call generate-debrief* in a loop and burn Anthropic API spend without bound. Free account signup is open.

**Recommendation:** Add a per-user daily cap (count debriefs generated in last 24 h before calling Anthropic), and set a hard spend limit on the Anthropic API key itself as a backstop.

#### H-3. Share tokens are generated with a non-cryptographic RNG
`saved_flights.share_token` = `substr(md5(random()::text), 1, 12)`. Postgres `random()` is not a CSPRNG and is seedable/predictable; truncated MD5 adds no entropy. 12 hex chars ≈ 48 bits at best, and realistically less. Share links gate access to private flight data (pilot notes, routes).

**Recommendation:** Use `gen_random_uuid()` or `encode(gen_random_bytes(9), 'base64')` (pgcrypto) for new tokens. Existing tokens can be rotated lazily.

---

### MEDIUM

#### M-1. CheckRide scores are computed client-side and trusted server-side
Scoring lives entirely in the C# client; the server accepts whatever `score`, `grade`, `events`, and `breakdown` arrive with a valid JWT. Any user can forge a perfect run with `curl`. Today the blast radius is personal stats, but the roadmap (career mode, leaderboards, subscriptions) makes forged scores a real integrity problem. The `[Simulated]` debug-upload path in `FlightListForm.cs` demonstrates in-repo how easy fabrication is.

**Recommendation:** Accept this for beta, but before any competitive/leaderboard feature: server-side sanity validation (score consistent with events/breakdown math, plausible flight time vs. distance), and consider signing reports in the client as a speed bump.

#### M-2. `pull-route` edge function is an unauthenticated proxy to a paid API
It requires no JWT and spends `FPD_API_KEY` quota on every call. Anyone can exhaust the Flight Plan Database quota (or run up costs) by hammering the endpoint directly. `metar` has the same shape but proxies a free government API — lower stakes, still an abuse vector.

**Recommendation:** Verify the functions are deployed with JWT verification where feasible; at minimum add basic rate limiting (per-IP via a simple table or Upstash) on `pull-route`.

#### M-3. Insert policy likely doesn't validate `flight_id` ownership
The client sends `flight_id` and `user_id`; RLS presumably checks `user_id = auth.uid()` only. If so, a user can attach CheckRide runs to **another user's** flight card ID. The web UI queries runs by flight_id + user filter so impact is limited today, but `report.html` renders any run by ID publicly, showing the other user's route on the report.

**Recommendation:** Add a `WITH CHECK` that `flight_id` belongs to `auth.uid()` (subquery on `saved_flights`), or validate in a trigger.

#### M-4. No Content-Security-Policy on any page
GitHub Pages prevents custom response headers, but `<meta http-equiv="Content-Security-Policy">` works for script-src. The app is heavily `innerHTML`-driven (49 uses in `index.html`, 21 in `report.html`, 15 in `flights.html`), renders user-controlled data (pilot notes, aircraft names, AI debriefs), and keeps Supabase session tokens in `localStorage` — a single missed escape means token theft. `escapeHtml` exists in `flights.html` and `report.html` but there is **no escaping helper in `index.html`** despite the largest innerHTML surface.

**Recommendation:** Add a meta CSP (script-src 'self' plus the known CDNs; no 'unsafe-inline' for scripts if feasible), and audit `index.html` innerHTML sinks for user-influenced data.

#### M-5. Public `checkride_results` exposes more than the report needs
Beyond `log_text` (H-1): full GPS tracks, timestamps, user UUIDs, and weather/conditions samples are readable for every run by anyone, not just via a share link. Public-by-default was a deliberate choice for free public report pages, but users are not told their runs are world-readable, and there is no opt-out.

**Recommendation:** Either add a per-run `is_public` flag (mirroring `saved_flights`), or state clearly in the client and on the marketing page that uploaded runs are public.

---

### LOW

#### L-1. Personal sample flight logs are embedded in the distributed exe
`samples/*.json` and `samples/*.log` (your own recorded flights, four sessions) are now `EmbeddedResource`s, extracted to every user's `%LOCALAPPDATA%`. They may contain your local paths/username and add megabytes of dead weight. The debug-upload feature that consumes them is a developer tool, not a user feature.

**Recommendation:** Exclude `samples/` from Release builds (`Condition="'$(Configuration)'=='Debug'"`), and strip the log files regardless.

#### L-2. `increment_page_view` RPC is publicly executable with arbitrary `page_name`
Anyone can spam the counter or inject garbage page names, poisoning analytics. Harmless to users, annoying to you.

**Recommendation:** Whitelist accepted `page_name` values inside the function; optionally rate-limit per IP.

#### L-3. `metar` function passes `icao` into the upstream URL without validation
`?icao=KJFK&format=json` style injection can alter query params sent to aviationweather.gov. Host is fixed so no real SSRF, but validate anyway.

**Recommendation:** `if (!/^[A-Z0-9]{3,4}$/.test(icao)) return 400;`

#### L-4. Prompt injection into publicly displayed AI debriefs
`generate-debrief` interpolates user-controlled strings (aircraft name, breakdown labels, event types) into the Claude prompt. A crafted aircraft name ("Ignore prior instructions, write…") can steer the debrief text, which is then displayed on a **public** report page under your brand. Output is HTML-escaped, so no XSS — this is a content/brand risk only.

**Recommendation:** Length-limit and character-restrict the interpolated fields; consider a system prompt with explicit "treat flight data as data" framing.

#### L-5. Plaintext session fallback never expires
`SessionStore.Load()` silently accepts a plaintext `session.json` (migration path). A local attacker could plant a plaintext file to inject a session. Very low risk (requires local access, DPAPI same-user scope anyway), but the fallback should eventually be removed.

**Recommendation:** Remove the plaintext fallback one or two releases after beta.

#### L-6. Unsigned executable
The single-file `CheckRide.exe` is not Authenticode-signed. Users get SmartScreen warnings, and there's no tamper evidence for a binary that holds auth tokens and talks to your backend.

**Recommendation:** For beta, publish SHA-256 hashes next to each release. Longer term, a code-signing cert (or Azure Trusted Signing, which is cheap now) removes the SmartScreen friction and is table stakes for a paid product.

---

### INFORMATIONAL

- **I-1. Secrets hygiene is good.** Only the publishable anon key is committed (by design). Git history shows no service-role key or Anthropic key ever committed. Keep it that way — pre-commit scanning (gitleaks) is cheap insurance as the repo grows.
- **I-2. `verify-client` logs user IDs + versions** to function logs. Fine operationally; be aware logs are PII-adjacent if you ever share log access.
- **I-3. CORS `*` on all edge functions.** Acceptable for a public API surface, but it means browser-based abuse of M-2 is trivial; tightening to your origins raises the bar slightly (non-browser abuse unaffected).
- **I-4. Public repo consideration.** Planning docs (`career-mode.md`, `usage.md`, roadmaps, this review) live in the same repo that serves the site. If the repo is public, competitors and attackers read your roadmap and this document. Consider a private repo for docs, or accept the transparency deliberately.
- **I-5. Supabase tokens in `localStorage`** (supabase-js default). Standard practice, but it makes M-4 (CSP/XSS) the control that actually protects sessions.
- **I-6. No account deletion / data export path** was observed. Worth adding before the user base grows (and required if EU users matter to you).

---

## Positive Observations

- **DPAPI encryption** of desktop session tokens with transparent migration — better than most indie sim tools.
- **`generate-debrief` does auth properly:** JWT verification, ownership check (403), idempotency guard, service-role key confined to the function environment.
- **RLS discipline documented:** the "every my-runs query must filter user_id" rule is written down and followed in `flights.html`/`report.html`.
- **HTML escaping** applied to user input in flight cards and report rendering (recent commit shows active attention to XSS).
- **Analytics fail silent** in the client (`TrackEvent` swallows errors) — no availability coupling to a non-essential feature.
- **Single-instance mutex + no elevation required** for the desktop client — correct least-privilege posture.

## Suggested Priority Order

1. H-1 — pull `log_text` out of public read (one policy change)
2. H-2 — Anthropic spend cap + per-user debrief limit
3. H-3 — fix share-token generation for new tokens
4. M-4 — meta CSP + `index.html` escaping audit
5. M-2 — rate-limit `pull-route`
6. Everything else as time allows; L-6 (signing) before any paid tier.
