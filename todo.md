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

### Security (from security-review.md)
- **M-5: Lock down checkride_results bulk listing** — public SELECT policy lets anyone enumerate every run (tracks, timestamps, user UUIDs) via the anon REST API, not just view runs they have links to. Fix: make table SELECT owner-only + add a `get-report` edge function (service role, fetch single run by id) so report.html share links keep working. ~1 hour incl. report.html fetch update. Do before user count grows. Interim: mention in client UI that uploaded runs are publicly viewable.
- **M-2: Rate-limit pull-route** — unauthenticated proxy spending FPD API quota.
- **M-4: Meta CSP + index.html escaping audit** — 49 innerHTML uses, no escapeHtml helper there.
- **L-6: Code signing** — Azure Trusted Signing (~$10/mo) before public/paid launch; SHA-256 hashes in release notes until then (see CheckRide/PUBLISH.md).

### Polish
- **Multi-language** — international reach, big undertaking.

---

> Top picks for next: **Logbook CSV export**, **My Stats**
