# SimLetsFly Project Review — July 5, 2026

Covers the website (index.html, flights.html, report.html, sw.js, Supabase) and the
CheckRide Windows client (C#/.NET). Organized as **Bugs**, **Improvements**, and
**Feature Gaps** per component, with a prioritized shortlist at the end.

> **Status (same day):** items **1–6, 9, 10, 14–18, 21, 23** were fixed and committed.
> Scoring version bumped to `xp12-1.4` (landing-lights gate + pause handling change
> event detection). Remaining open items are the larger features and refactors:
> 7, 8, 11, 12, 13, 19, 20, 22 (website hint part), 24–30.

---

## Website

### Bugs

1. **CheckRide count includes other users' runs** — `flights.html:709` queries
   `checkride_results.select('flight_id')` with **no `user_id` filter**. Since the
   public-read RLS policy (`FOR SELECT USING (true)`) was added for shareable reports,
   this returns *every* user's rows. The new "CheckRides" stat chip and the
   `_flightsWithCheckRide` badge set will silently count strangers' runs as soon as a
   second user uploads. Fix: add `.eq('user_id', currentUser.id)`.

2. **Inconsistent HTML escaping in CheckRide rendering** — `report.html` escapes
   `e.Description`, `row.Label`, `aircraft`, and `sim`, but `flights.html` does not
   (lines ~1684, 1716, 1827, 1870). The aircraft name comes from the sim's
   `acf_ui_name` and event descriptions are built from sim data; a crafted aircraft
   livery name could inject markup into your own page. Low severity (self-XSS), but
   the two pages should match — copy report.html's escaping into flights.html.

3. **Auto-complete flight update swallows errors** — the `saved_flights` update fired
   when a CheckRide exists (`_renderCheckRides`) ends in `.then(() => {})` with no
   error branch. If it fails, the UI shows Completed but the DB still says Pending,
   and it will "un-complete" on next reload with no hint why.

4. **Score breakdown can disagree with final score** — C# clamps the score to 0–100
   but the breakdown line items are raw. A flight with e.g. 100 base + Greaser +10 +
   Hand Flown +10 shows breakdown rows summing to 120 while "Final Score" says 100.
   Same for very bad flights clamped at 0. Consider a "clamped to 100" line item or a
   footnote row when `sum ≠ score`.

### Improvements

5. **Service worker caches map tiles forever** — `sw.js` pass-through list doesn't
   include `cartocdn.com`, so every CartoDB tile fetched by Leaflet falls into the
   cache-first branch and is stored in the versioned cache. Storage grows unboundedly
   with map panning, and every cache-version bump throws it away and starts over.
   Add `basemaps.cartocdn.com` to `passThrough` (tiles are already HTTP-cached by the
   browser).

6. **airports.json (18.8 MB) re-downloads on every SW bump** — it's in the `STATIC`
   precache list, and the cache version has gone v62 → v82 in about a week. Every
   deploy makes every user re-download 18.8 MB. Options: move it out of `STATIC` into
   a separate long-lived cache keyed by content hash, or split rarely-changing data
   from the versioned app-shell cache.

7. **index.html is a 282 KB / 6,000-line monolith** — auth, planning, map, weather,
   export, vertical profile, and winds-aloft all in one file. Duplicated helpers
   (`escapeHtml`, auth header, theme, Supabase init) exist in all four pages and have
   already drifted (see bug #2). Even without a build step, a shared `common.js`
   would eliminate the drift.

8. **No pagination / limits on Supabase queries** — `saved_flights.select('*')` and
   the checkride queries fetch everything. `track` blobs are ~KBs per run; a user
   with 50 runs pays that on every card expand. Consider excluding `track`/`events`
   from the list query and fetching them only when the modal opens.

9. **Old saved-HTML reports fail confusingly** — files saved before the
   self-contained export fix show "Report Unavailable." Detect `file:` protocol
   without embedded data and show a "Re-save this report from the live page" message
   instead.

### Feature Gaps

10. **No social preview for shared reports** — report.html has no Open Graph /
    Twitter card tags. Shared links to Discord/Twitter render bare. Static OG tags
    (title + logo) are trivial; per-report scores would need an edge function.

11. **Conditions data uploaded but never shown** — the client records weather samples
    every 5 minutes (`Conditions[]`: wind, visibility, cloud base/coverage, OAT,
    day/night) and uploads them, but neither flights.html nor report.html displays
    them. A weather strip on the report would be nearly free — the data is already
    there. Same for `LandingLateralG`, `CrosswindAtLandingKt`, `AvgFinalDescentRateFpm`,
    and the V-speed set (`VsoKts`…`VleKts`), all uploaded, never rendered.

12. **No way to delete a CheckRide run** — you asked about this during development;
    there's still no delete button on a run row. Needs a `DELETE` RLS policy scoped
    to `user_id` plus a small confirm UI.

13. **From todo.md, still open** — My Stats page, Logbook CSV export, fuel tracking,
    password strength hint on sign-up, aircraft profiles, SimBrief OFP integration.

---

## CheckRide Client (C#)

### Bugs

14. **"Upload success" sound plays even when the upload failed** —
    `FlightListForm.OnFlightCompleted` awaits `UploadPendingAsync()` and then
    unconditionally plays `upload_success.wav` and the debrief. But
    `UploadPendingAsync` catches its own exceptions (showing the Retry button) and
    returns normally. On a failed upload the pilot hears "upload success" while the
    status bar says "Upload failed." Have `UploadPendingAsync` return a bool and gate
    the sound on it.

15. **Sim pause corrupts stats** — `IsSimPaused` is captured in every snapshot
    (`XP12Connector.cs:478`) but `FlightMonitor.OnSnapshot` never checks it. While
    paused: flight time keeps accruing (wall-clock based), autopilot seconds keep
    counting, and night-flight percentage skews. A 30-minute dinner break mid-flight
    inflates flight time by 30 minutes. Fix: early-return from `OnSnapshot` when
    `snap.IsSimPaused` (keep the track sampler suppressed too).

16. **Landing-lights check false-flags the initial climb** — in `DetectSystemChecks`,
    `onShortFinal` is `(_phase == Airborne && AGL < 500) || Approach || Landed`. That
    Airborne clause is also true right after takeoff below 500 ft. Anyone who takes
    off with landing lights off gets "Landing lights off on approach" seconds into
    the flight. Gate it on descending (`VerticalSpeedFpm < 0`) or on `_peakAglFt`
    having exceeded some threshold.

17. **Concurrent token refresh can race** — `LoadDataAsync` runs `GetFlightsAsync`
    and `GetLastScoresAsync` in parallel via `Task.WhenAll`; both call
    `EnsureTokenAsync`. With an expired token, two refresh-grant requests fire with
    the same refresh token; Supabase rotates refresh tokens, so the loser can invalidate
    the session ("Session expired — please sign in again" right after launch). Guard
    `EnsureTokenAsync` with a `SemaphoreSlim(1,1)`.

18. **`VleKts` actually contains Vs** — `XP12Connector` maps
    `sim/aircraft/view/acf_Vs` (clean stall) into `VleKts` as an acknowledged
    fallback, then uploads it labeled `VleKts`. Anyone reading the stats blob will
    misinterpret it. Rename the field to what it holds, or drop it until a real Vle
    source exists.

### Improvements

19. **~70 HTTP requests per second to XP12** — `BuildSnapshotAsync` fetches every
    dataref as an individual GET each second. It works, but it's noisy and adds
    latency scatter between values in one "snapshot" (values are up to ~1s apart,
    which matters for touchdown VS). XP12 12.1+ offers a WebSocket subscription API
    that pushes batched updates; that's the natural upgrade path and would also make
    the touchdown reading crisper.

20. **Touchdown VS is a 1 Hz sample, not the actual touchdown rate** —
    `RecordLanding` takes `snap.VerticalSpeedFpm` from the first on-ground sample. By
    then the gear has often arrested the descent; greaser/hard classification is
    partly luck of the polling phase. Options: track the min VS over the last ~2s
    before ground contact, or read XP12's `fnrml_gear`-style impact datarefs.

21. **Auth tokens stored in plaintext** — `session.json` in LocalApplicationData
    contains the access and refresh token unencrypted. Windows DPAPI
    (`ProtectedData.Protect`) is a two-line change.

22. **`ScoringVersionConst = "xp12-1.3"` is manually maintained** — scoring changed
    several times recently (breakdown, new bonuses/penalties) without a version bump
    being obviously enforced. Old runs display fine (website renders whatever
    breakdown was uploaded — good design), but comparing scores across versions is
    meaningless. Consider bumping the string every time `ScoringConfig` changes, and
    showing a "different scoring version" hint next to the trend arrow on the website
    when comparing runs recorded under different versions.

23. **Wrong-departure detection hard-codes 3.0 nm** — `FlightMonitor.cs` uses a
    literal `3.0` in two places while every other threshold lives in
    `ScoringConfig`. Move it there for consistency.

### Feature Gaps

24. **Fuel tracking** — already in todo.md: capture fuel at takeoff and landing for
    burn / avg-consumption stats. The dataref list needs
    `sim/flightmodel/weight/m_fuel_total` (kg).

25. **Pattern-work blind spots** — the Approach phase is only entered from Cruise,
    and Cruise requires 30 stable seconds above 1,000 ft AGL. Traffic-pattern flights
    (stay below 1,000 ft) never reach Approach, so gear-down-on-final and several
    approach checks silently don't apply. Worth either a lightweight "pattern mode"
    or extending the Airborne-descent fallback that already exists for
    `DetectApproachEvents` to the gear check.

26. **ILS data captured but unscored** — glideslope and localizer deviation
    (`nav1_vdef_dot` / `nav1_hdef_dot`) are polled and logged every second but never
    scored or uploaded. A "flew the ILS within ½ dot" bonus would be a nice
    differentiator and the data is already flowing.

27. **No auto-update / version check** — the client has no way to know a newer build
    exists. A version row in Supabase checked at launch, with a "new version
    available" link, is cheap insurance now that scoring evolves quickly.

28. **Single sim** — XP12 only. MSFS (SimConnect) is the obvious market expansion;
    the `Sim` field and `sim` column already anticipate it.

---

## Cross-cutting

29. **Scoring "source of truth" doc drift** — checkride.md / client-app.md predate
    the breakdown refactor and the four new bonuses. Since the website intentionally
    has no scoring logic, the C# `ScoringConfig` is the de-facto spec — worth
    regenerating the doc from it (or noting in the docs that ScoringConfig is
    authoritative).

30. **No automated tests anywhere** — `FlightMonitor` is pure logic driven by
    snapshots and is highly testable (feed a synthetic snapshot sequence, assert
    events/score). Even 10 golden-file tests (takeoff, greaser, hard landing, stall,
    pause, touch-and-go) would protect the scoring engine from regressions as you
    keep tuning it. On the web side, the report renderer could be tested with a
    stored sample JSON.

---

## Prioritized shortlist

| # | Item | Effort | Why first |
|---|------|--------|-----------|
| 1 | Add `user_id` filter to checkride count query (bug 1) | 1 line | Wrong data as soon as user #2 uploads |
| 2 | Gate upload-success sound on actual success (bug 14) | small | Actively misleads the pilot |
| 3 | Skip snapshots while sim paused (bug 15) | small | Corrupts every stat on paused flights |
| 4 | Stop SW-caching map tiles; un-precache airports.json (5, 6) | small | Storage + 18 MB per user per deploy |
| 5 | Landing-lights climb false-flag (bug 16) | small | Unfair penalty, user-visible |
| 6 | Escape CheckRide fields in flights.html (bug 2) | small | Consistency with report.html |
| 7 | Serialize token refresh (bug 17) | small | Intermittent forced re-login |
| 8 | Show Conditions/weather on report (gap 11) | medium | Data already uploaded, high perceived value |
| 9 | Delete-run UI (gap 12) | medium | Users will accumulate junk runs |
| 10 | FlightMonitor golden tests (30) | medium | Protects the thing you tweak most |
