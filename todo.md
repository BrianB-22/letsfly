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

### Flight Planning
- **Aircraft profiles** — save multiple aircraft (C172 @ 120kt, A320 @ 450kt) instead of one cruise speed. Quick switch before generating a flight.
- **Full flight plan via SimBrief API** — [Navigraph/SimBrief API](https://developers.navigraph.com/docs/simbrief/using-the-api) auto-generates a complete OFP using dep/arr ICAO + aircraft type. User needs a free SimBrief account.

### Polish
- **Multi-language** — international reach, big undertaking.

---

> Top picks for next: **Logbook CSV export**, **My Stats**
