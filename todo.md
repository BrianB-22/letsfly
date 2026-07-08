# SimLetsFly — Feature Tracker

## Recently Shipped ✓

- **Site banner** — `banner.json` driven, toggle active/message without touching HTML
- **Last Airport in My Flights** — departure option that uses the arrival of the most recent saved flight; shows airport type, city, runway length
- **Arrival direction filter** — N/S/E/W dropdown next to runway length; ±67.5° arc from departure
- **Load Route** — button on My Flights cards that restores the full flight (dep, arr, speed, notes, route nodes) back into the main app
- **Flight Brief merged into Export** — one dropdown on My Flights cards instead of two
- **Get IFR Route** — renamed from "Pull Route"; added 9s timeout with sleep wake-up toast; route header in pilot notes now stamped with pull date
- **About page** — bernacki.me author link at top, Discord removed, all new features documented
- **Challenge airports** ✓ — already shipped before this session
- **TAF alongside METAR** ✓ — edge function already supported it, wired to UI
- **PWA / installable** ✓ — manifest.json, service worker (sw.js), icons; installs on desktop and mobile
- **Aircraft preset save/restore** — saves selected preset per user in Supabase `profiles.aircraft_preset`; auto-saves on dropdown change; restores on login/reload via `data-key` attribute matching; speed-only fallback for legacy saves
- **Font size increase** — bumped all CSS sizes up (body 14→16px, labels 9→11px+); applied to index.html and flights.html
- **CheckRide aircraft picker fixes** — dock-order bug (bar rendered below bottom bar instead of above), font consistency/sizing pass, search returning only alphabetically-first 50 results, unranked substring search, status bar text clipping
- **FAA aircraft data persistence + enrichment** — Manufacturer/Model/EngineClass/WeightClass/Vref/WTC now saved on upload (previously only bare ICAO code); info line enriched with engine count, approach category, MTOW; bolded/brightened for legibility
- **Client version tracking** — `client_version` now saved on every upload for debugging (previously only sent once at login for version-gate check)
- **Live-flight-tested scoring fixes (piston, C172)** — confirmed N1 low-power-takeoff check (88% cutoff) works correctly for piston aircraft (liftoff N1 reads ~90%, no false positive); fixed 3 bugs found during testing: (1) Fast Taxi/Aggressive Taxi Turn false-firing during full-throttle takeoff roll — now gated on throttle, not just speed; (2) Overspeed check was wired to X-Plane's native Vne (over_vne) flag instead of the aircraft's actual Vno+5kt — was nearly untriggerable in normal flight; (3) "Takeoff" directional-control check had no real takeoff gate (fired on any low-altitude high-lateral-G moment, including mid-flight unusual attitudes/crashes) — now latches closed once climbed past 500ft
- **report.html crash-track fix** — flight map no longer labels the last track point with the destination airport ICAO when the flight crashed, had a runway excursion, or landed at the wrong airport; end-of-track dot now shows red instead of the "arrived safely" white/green in those cases. Also added a "Terrified" Pax Comfort tier for score-0 (crash) runs.

---

## Remaining Ideas

### High Value
- **My Stats** — per-user page: total flights, countries visited, estimated hours flown, top regions, rating trends. All data already in Supabase — just needs aggregation queries + a page.
- ~~**Community feed**~~ — dropped.
- **Logbook CSV export** — let users download their My Flights data as a spreadsheet. Low effort, useful.
- **Fuel tracking (CheckRide client)** — capture fuel quantity (lbs or kg) at takeoff and at landing via XP12 REST API. Upload `FuelAtTakeoffLbs` / `FuelAtLandingLbs` to `stats`. Website can then show fuel burned per flight and average consumption (lbs/hr or gal/hr) in My Stats.

### Flight Planning
- **Aircraft profiles** — save multiple aircraft (C172 @ 120kt, A320 @ 450kt) instead of one cruise speed. Quick switch before generating a flight.
- **Full flight plan via SimBrief API** — [Navigraph/SimBrief API](https://developers.navigraph.com/docs/simbrief/using-the-api) auto-generates a complete OFP using dep/arr ICAO + aircraft type. User needs a free SimBrief account.

### CheckRide Scoring
- **European transition altitude** — altimeter check is hardcoded to FL180 (FAR/US). In Europe, transition altitude is country-specific and much lower (e.g. 3,000 ft Netherlands, FL100 range for most). Fix: make transition altitude configurable, default 18,000 ft, lower for European ops. No false penalties today (29.92 inHg = 1013 hPa numerically), but the FL180 checkpoint is wrong for EASA airspace.
- **TEST NEEDED: flex/derated-thrust jet takeoff vs N1 low-power check** — piston aircraft confirmed fine (see Recently Shipped), but real jets commonly use flex/assumed-temperature reduced-thrust takeoffs legitimately below 90% N1 — could still false-flag a normal airline-style takeoff. Need to fly a jet (and ideally a turboprop) and check the logged liftoff N1% before trusting this check universally. May need to gate using the FAA `Physical_Class_Engine` data already loaded in `AircraftDb`.

### Security (from security-review.md)
- **M-5: Lock down checkride_results bulk listing** — public SELECT policy lets anyone enumerate every run (tracks, timestamps, user UUIDs) via the anon REST API, not just view runs they have links to. Fix: make table SELECT owner-only + add a `get-report` edge function (service role, fetch single run by id) so report.html share links keep working. ~1 hour incl. report.html fetch update. Do before user count grows. Interim: mention in client UI that uploaded runs are publicly viewable.
- **M-2: Rate-limit pull-route** — unauthenticated proxy spending FPD API quota.
- **M-4: Meta CSP + index.html escaping audit** — 49 innerHTML uses, no escapeHtml helper there.
- **L-6: Code signing** — Azure Trusted Signing (~$10/mo) before public/paid launch; SHA-256 hashes in release notes until then (see CheckRide/PUBLISH.md).

### Polish
- **Multi-language** — international reach, big undertaking.

---

> Top picks for next: **Logbook CSV export**, **My Stats**
