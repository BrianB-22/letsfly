# Code Review — simletsfly

**Date:** 2026-06-24
**Scope:** Full project — `index.html`, `flights.html`, `help.html`, `sw.js`, Supabase edge functions (`metar`, `pull-route`), and SQL migrations.

---

## Security

### 1. XSS — unescaped user data in `flights.html` `buildCard()` (Medium)
`flights.html:674` and `:711` inject `f.dep_name`, `f.arr_name`, and `f.pilot_notes` directly into `innerHTML`/template markup with no escaping. No `escapeHtml` helper exists anywhere in the project.

- `<textarea>${f.pilot_notes || ''}</textarea>` breaks out with `</textarea><img src=x onerror=...>`.
- Currently mostly **self-XSS** — `loadFlights` only pulls `eq('user_id', currentUser.id)` (`flights.html:518`), so a user only renders their own notes. But it is one query change from becoming stored XSS, and the pattern is fragile.
- The *public* share path in `index.html` (`loadSharedFlight`, `:5393`) is safe — it pulls airport objects from the local trusted `AIRPORTS` DB and sets notes via `textarea.value` (`:5384`), not `innerHTML`.

**Fix:** add an `escapeHtml()` helper and apply to all interpolated DB strings in `buildCard`.

### 2. Open CORS + server-side API key on `pull-route` (Medium — abuse/cost)
`pull-route/index.ts` uses your server-side `FPD_API_KEY`, but `Access-Control-Allow-Origin: '*'` (`:2`) means anyone can call the function as a free FlightPlanDatabase proxy and burn your quota.

**Fix:** restrict origin to `https://simletsfly.com` and/or add rate limiting. The open-CORS on `metar` is acceptable (public read-only data).

### 3. Public-flight RLS policy leaks all columns (Low)
`create_saved_flights.sql` — the `"Anyone views public flights" using (is_public = true)` policy combined with `select('*')` exposes `user_id` (and every other column) of any public flight to anonymous clients.

**Fix:** expose a view with only needed columns, or select explicit columns.

### 4. `icao` not validated in `metar` function (Low)
`metar/index.ts:12` uppercases/trims but does not validate format, then interpolates unencoded into the aviationweather URL (`:24`). Passing `icao=KJFK&foo=bar` injects extra query params. Host is fixed, so no SSRF — just sanity-check `^[A-Z0-9]{3,4}$`.

### 5. Supabase publishable key in client (`index.html:2468`) — fine
Publishable key by design; RLS is the real guard. Noted so it is not mistaken for a leak.

---

## Correctness / Schema

### 6. `profiles` table has no creation migration (flag)
Migrations reference and `ALTER`/`upsert` `profiles` (`flights.html:456,501`, `add_aircraft_preset` migration) but there is no `create table profiles` in `supabase/migrations/`. A fresh environment cannot be rebuilt from migrations alone, and no RLS is defined for it in-repo.

**Fix:** add the create migration with its RLS policies.

---

## Quality / Maintainability

- **`airports.json` is 18 MB** and is in the SW `STATIC` precache list (`sw.js:7`, `addAll`) — the whole file downloads on first install. Consider trimming fields or lazy-loading.
- **`index.html` is 5,564 lines / 254 KB**, all JS inline. Acceptable as a deliberate single-file/SW-caching tradeoff, but at the limit of maintainability — extracting the JS into a cached module would help without hurting offline behavior.
- 42 `innerHTML` sites in `index.html`; once `escapeHtml` exists (#1), route other DB/external interpolations (e.g. METAR/TAF decode at `:5334`) through it for consistency.

---

## Highest-value fixes
1. Add `escapeHtml` and patch `buildCard` (#1)
2. Lock down `pull-route` CORS (#2)
3. Commit the missing `profiles` migration (#6)
