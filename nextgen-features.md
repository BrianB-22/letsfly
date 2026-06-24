# SimLetsFly — Next-Gen Feature Ideas

> Forward-looking ideas beyond the current `todo.md` roadmap. Each item is tagged with rough **value** and **effort**.
> Nothing here is committed — this is a planning doc.

---

## Highest-Value, Low-Effort Wins

### 1. Social share previews (Open Graph tags) — Value: High · Effort: Low–Medium
A `?flight=token` link shared to Discord/Reddit/Twitter currently renders as a bare URL.
Add OG/Twitter meta tags so it unfurls into a card (route, distance, map thumbnail).
- Static tags are trivial.
- Per-flight dynamic OG images need a small edge function.
- Cheapest growth lever for a tool that lives on Reddit.

### 2. Daily / Weekly Challenge + leaderboard — Value: High · Effort: Medium
One shared "Flight of the Day" everyone flies (deterministic seed by date), with an optional
leaderboard or "I flew it" counter.
- Reuses existing Challenge Mode logic and the `page_view` RPC pattern.
- Turns a one-shot tool into a return-daily habit.

**Challenge pool** (the rotation the daily/weekly picker draws from — all build on existing
filters/data: runway length/surface, elevation, distance, region, aircraft preset, METAR/TAF):

*Difficulty (leans on Challenge Mode)*
- **Short & Sweet** — land at a strip under 2,500 ft
- **Top of the World** — arrival above 8,000 ft elevation
- **Off the Grid** — unpaved runway, no ILS
- **Pin-Drop** — both ends shorter than 4,000 ft
- **One Shot** — no approach system at arrival (visual only)

*Distance / aircraft*
- **Quick Hop** — leg under 150 nm
- **The Long Way** — single leg over 1,500 nm
- **GA Only** — complete it in an aircraft under 200 kts (C172, Cherokee…)
- **Heavy Metal** — fly it in a widebody (A380 / 787)

*Geography*
- **Region of the Week** — random arrival within a featured continent (rotates weekly)
- **Island Hopper** — arrival is an island airport *(data-dependent: needs a tag)*
- **Coastal Run** — dep and arr both coastal *(data-dependent)*
- **Cross-Continent** — dep and arr on different continents

*Weather (uses METAR/TAF already pulled)*
- **IFR Day** — arrival reporting IMC / low visibility
- **Crosswind King** — arrival with significant crosswind to its main runway
- **Severe Clear** — pristine VFR both ends (easy/relaxing option)

*Themed / fun*
- **Historic** — arrival is a notable/heritage field *(data-dependent: needs a tag)*
- **Alphabet** — dep and arr ICAOs start with the same letter
- **Reverse It** — fly a featured route backward

**Design notes**
- **Seed deterministically by date** (e.g. hash of `YYYY-MM-DD`) so everyone sees the same
  challenge, reproducible without storing it server-side.
- **Weekly = harder/themed, Daily = quick** is a natural split.
- Weather-based ones use live data: the *type* is fixed by date, the qualifying airport is
  picked at generation time.
- Items tagged *data-dependent* (Historic, Island, Coastal) need a flag in `airports.json`
  that may not exist yet.

### 3. Achievements / badges — Value: Medium–High · Effort: Low
All derivable from existing `saved_flights` data + airport metadata (distance, bearing,
completed flag, ratings, challenge flag, elevation, runway length/surface, continent, nav aids).
- Pairs naturally with the planned My Stats page. Cheap, sticky, fun.

**Initial badge set** (~12–15 to start so the grid feels achievable):

*Milestones (flight count)*
- **First Officer** — log your 1st flight
- **Frequent Flyer** — 10 flights
- **Captain** — 50 flights
- **Senior Captain** — 100 flights

*Distance / endurance (`distance_nm`, single or cumulative)*
- **Puddle Jumper** — a leg under 100 nm
- **Long Haul** — a single leg over 2,000 nm
- **Around the World** — 21,600 nm cumulative (Earth's circumference)
- **Mile High Club** — 100,000 nm cumulative

*Exploration (continent / region of arrivals)*
- **Globetrotter** — land on 5 different continents
- **Seven Seas** — all 7 continents (ties to Challenge airports / Antarctica)
- **Regional Regular** — 10 unique arrival countries/regions
- **Home Turf** — 25 flights within one continent

*Challenge / difficulty (`is_challenge` + airport metadata)*
- **Bush Pilot** — land on an unpaved runway
- **Short Field** — land at a strip under 3,000 ft
- **Thin Air** — land at an airport above 9,000 ft elevation
- **No ILS** — complete a flight with no approach system at arrival
- **Challenger** — complete 10 Challenge-mode flights

*Engagement (completed flag, ratings, sharing)*
- **Logbook Keeper** — mark 10 flights Completed
- **Critic** — rate 25 flights
- **Five Stars** — give a flight a 5★ overall rating
- **Show-off** — share a public flight link

*Fun / directional (`bearing_deg`)*
- **Due North** — a flight bearing within 5° of 360°
- **Round Trip** — log a flight whose dep/arr reverse an earlier one

**Implementation notes**
- No schema change beyond storing *which* badges a user earned — a `user_badges` table or a
  JSONB column on `profiles`.
- Compute client-side on the My Stats page first (cheapest); move to a server-side trigger
  only if leaderboard tamper-resistance is needed.

### Board identity — prerequisite for #2 leaderboard and #3 public badges
Auth currently only exposes `currentUser.email`. Showing email on a public board leaks PII — a non-starter.

**Recommended: pilot callsign**
- Add a `display_name` (callsign) column to `profiles`.
- **Auto-generate a default** (e.g. `SLF-A4F2`) so signup is never blocked by a prompt — the board works for everyone immediately.
- Make it **editable** in My Flights (one optional field). Validate length, strip HTML (reuse `escapeHtml`), enforce uniqueness if possible.
- **Never render email** anywhere public.

**Board row format:** `Callsign · stat · optional badge` — e.g. `SLF-A4F2 · 142 flights · 🌍 Globetrotter`.

---

## Core-Feature Depth

### 4. Aircraft-range-aware suggestions — Value: High · Effort: Medium
Add approximate range per aircraft preset and flag when a generated leg exceeds it (e.g. a
C172 offered a 2,000 nm leg).
- **Warn, don't block** — show a soft "⚠ Beyond typical range for [aircraft]" notice and let
  the user fly it anyway. Preserves the discovery feel (ferry-tank / challenge runs stay valid).
- Optional toggle later: "Keep me within range" for users who want a hard cap.
- Makes the core generator smarter using data mostly already present.

### 5. Multi-leg tour generator — Value: Medium–High · Effort: Medium
Extend existing "Next Hop" / "Add Hop" into "generate me a 5-leg tour from KSEA" that chains
reachable airports into a coherent trip.
- Natural evolution of existing pieces; great for content and sharing.

### 6. Weather-aware filtering — Value: Medium · Effort: Medium
"Only suggest arrivals reporting VFR" (or inversely, "challenge weather").
- METAR/TAF is already pulled — let it feed the filter, not just the brief.

---

## Polish / UX

- **Scenic filters** — coastal, mountainous, island. Strong appeal for sim sightseeing;
  needs a tag/elevation derivation in the airport data. (Value: Medium · Effort: Medium)
- **First-run onboarding** — one-pass tooltip tour; the filter panel is dense for newcomers.
  (Value: Medium · Effort: Low)
- **Reroll button** — one-tap "give me another" without re-touching filters.
  (Value: Medium · Effort: Low)

---

## Technical / Operational

- **Trim / lazy-load `airports.json`** — 18 MB precached on install is a heavy first load,
  especially on mobile. Split by region or strip unused fields. Biggest perf win available.
- **Error visibility** — many silent `.catch(() => {})` blocks. A lightweight client error
  beacon would surface failures currently invisible.
- **Airport data freshness** — document a refresh process/source so the DB doesn't drift from
  real-world AIRAC over time.
- **Carry-over from code review** — `pull-route` CORS lockdown, missing `profiles` migration.

---

## Suggested Next Sequence

1. **OG share previews** (growth, cheap)
2. **Daily Challenge + achievements** (retention, reuses existing logic)
3. **Aircraft-range-aware generation** (makes the core feature smarter)
