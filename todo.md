# SimLetsFly — Feature Ideas

## Useful for Flight Planning

- **TAF (forecast) alongside METAR** — shows what weather will be when you actually fly, not just now. Same aviationweather.gov API
- **Aircraft profiles** — save multiple aircraft (C172 @ 120kt, A320 @ 450kt) instead of one cruise speed. Quick switch before generating a flight
- **Suggested route** — use [Flight Plan Database API](https://flightplandatabase.com/dev/api) to show an auto-generated airway route between dep/arr ICAOs (e.g. `V172 → BRL`). Sim-use only, free tier available. Pairs well with existing SimBrief export
- **Full flight plan via SimBrief API** — [Navigraph/SimBrief API](https://developers.navigraph.com/docs/simbrief/using-the-api) can auto-generate a complete OFP (route, fuel, waypoints, winds aloft) using dep/arr ICAO + aircraft type. User needs a free SimBrief account

## Social / Engagement

- **Community feed** — a page showing recent publicly shared flights from all users. Discover interesting routes other pilots have flown
- **My Stats** — per-user page showing total flights, countries visited, estimated hours flown, top regions, rating trends over time. All data is already in Supabase

## Polish

- **Logbook CSV export** — let users download their My Flights data as a spreadsheet
- **PWA / installable** — add a manifest.json so the site can be pinned to a phone home screen and feels like a native app
- **Multi-language** — international reach, though that's a big undertaking

## Wildcard

- **Challenge airports** — a "hard mode" button that finds airports with short runways, high elevation, or mountainous terrain. Adds a game element

---

> Top picks for user engagement: **TAF**, **My Stats**, **Community Feed**
