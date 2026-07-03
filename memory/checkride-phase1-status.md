# CheckRide Phase 1 Status

CheckRide Phase 1 is functional as of 2026-06-29. Builds and runs as a system tray app, produces `.log` and `.json` files, tested against real X-Plane 12 flights.

**What's built:**
- .NET 6 WinForms tray app at `c:\code\SimLetsFly\CheckRide\`
- Manual Start/End Recording — no automatic stop
- XP12 REST API v3 connector polling ~40 datarefs per second
- Phase state machine: Idle → Taxiing → Airborne → [Cruise → Approach →] Landed
- Scoring engine with F–S grades; F is automatic on crash or runway excursion
- FlightStats block in JSON (distance NM, AP%, max values, wind at landing, go-arounds)
- Aircraft performance limits (Vso, Vno, Vne, Vfe, Vle) pulled from XP12 — no user input needed
- Aircraft-aware approach speed thresholds (Vref = 1.3 × Vso)
- ScoringVersion field in every report JSON

**Key bugs fixed during testing:**
- `indicated_airspeed` is already kias — was being double-converted from m/s
- `on_ground` is an array — must parse [0], not use `?index=0` URL param
- Approach checks only ran in Approach phase — now also fire in Airborne below 2,000ft AGL (pattern/circuit flights never reach Cruise/Approach)
- `has_crashed` does not fire for water impact — detect via AGL < −2ft or OnGround=false at speed

**ScoringVersion:** currently `xp12-1.2`. Format is `<sim>-<version>` — encodes both sim type and scoring logic version in one string. Bump the numeric part whenever detection logic or penalties change. Future sims get their own prefix (e.g. `msfs2020-1.0`). Server gates on the full string.

**Samples:** `CheckRide\CheckRide\samples\` — keep for regression reference.

**Phase 2:** Supabase login, flight picker, score upload.
