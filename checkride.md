# CheckRide — Planning Doc

## What It Is

An optional add-on to SimLetsFly. Users download a lightweight Windows client (Mac later)
that connects to their running simulator, monitors a flight from departure to parking brake,
and uploads a graded report to their SimLetsFly account.

The focus is **ride quality and safety** — not checklist compliance. CheckRide observes how
the flight was flown: smoothness, speed discipline, landing quality, and whether common
system states were correct for the phase of flight (pitot heat on while airborne, gear up
after takeoff, flaps retracted in cruise, etc.). These are phase-based state checks, not
procedural sequencing.

Multiple runs can be recorded against the same My Flights card — fly the same route
repeatedly and watch your scores improve over time.

This is also the test bed for the career mode grading engine in the SimCareer project.
The monitoring and scoring logic built here transfers directly.

---

## Components

### 1. CheckRide Windows Client (C# .NET WinForms)
- .NET 6 WinForms tray app (`net6.0-windows`)
- Recording is **manual only** — Start/End Recording via tray menu
- Login via SimLetsFly Supabase credentials (same auth) — Phase 2
- Flight picker: pulls user's My Flights list, user selects one before launching the sim — Phase 2
- Monitors the flight in real-time via sim connector
- Uploads `CheckRideResult` JSON to Supabase on completion — Phase 2

### 2. Supabase Backend
- New table: `checkride_results`
- Links to `saved_flights.id` (many runs per flight card)
- Stores event log + computed score + summary stats + scoring version

### 3. SimLetsFly Website — CheckRide Report View
- Each My Flights card shows a CheckRides section if runs exist
- Expandable list of runs with date, score, grade, scoring version, and key stats
- Drill into a run for the full breakdown and event timeline
- Progress trend shown if 3+ runs exist

---

## Simulator Support

| Sim | Connection | Priority |
|---|---|---|
| X-Plane 12 | REST API `localhost:8086/api/v3/` | First |
| X-Plane 11 | UDP broadcast `localhost:49000` | Second |
| MSFS 2020/2024 | SimConnect SDK | Third |

### X-Plane 12 API — Implementation Notes

Learned during Phase 1 prototype development:

- **API version**: Must use `/api/v3/` — v1 and v2 have different naming conventions and will 404
- **Dataref IDs are 64-bit**: IDs are `Int64` (e.g., `2254628761384`). Using `Int32` silently overflows and breaks resolution
- **IDs are session-stable only**: Re-resolve all dataref names to IDs on every session start — IDs are not guaranteed to be the same across X-Plane restarts
- **Resolve one at a time**: Batch resolution silently fails if any dataref is missing. Resolve each name individually so a missing dataref does not block the others
- **Array datarefs**: `GET /api/v3/datarefs/{id}/value` returns the full array. Do not rely on `?index=N` URL parameter — fetch the full array and parse the element in C#
- **`indicated_airspeed` is already in kias**: Do not convert — XP12 returns this dataref in knots directly. `groundspeed`, `elevation`, and `y_agl` are in SI units (m/s or metres) and must be converted
- **String datarefs** (`value_type: data`): value is base64-encoded bytes
- **`sim/flightmodel/misc/stall_warning`** does not exist in XP12 — 404s. Needs correct dataref name
- **`sim/flightmodel2/misc/has_crashed`** does not fire for water impact / runway excursion — detect these separately using AGL and OnGround state
- **WebSocket API** available at `ws://localhost:8086/api/v3` — supports 10Hz dataref subscriptions. Future improvement over REST polling

---

## Flight Monitoring

### Core Data (polled every ~1 second via `FlightDataSnapshot`)

| Field | Dataref | Units | Used For |
|---|---|---|---|
| Latitude / Longitude | `flightmodel/position/latitude` / `longitude` | degrees | Route tracking |
| Groundspeed | `flightmodel/position/groundspeed` | m/s → kts | Takeoff/landing detection |
| Airspeed (IAS) | `flightmodel/position/indicated_airspeed` | **kias (no conversion)** | Overspeed, approach speed |
| Vertical speed | `flightmodel/position/vh_ind_fpm` | fpm (no conversion) | Landing quality, approach rate |
| Altitude MSL | `flightmodel/position/elevation` | m → ft | Phase detection |
| Altitude AGL | `flightmodel/position/y_agl` | m → ft | Approach/landing checks |
| AoA | `flightmodel/position/alpha` | degrees | Stall detection |
| G-force (normal) | `flightmodel/forces/g_nrml` | G | Manoeuvre smoothness |
| On ground | `flightmodel2/gear/on_ground` | array[0] bool | Phase transitions |
| Gear position | `flightmodel2/gear/deploy_ratio` | array[0] 0–1 | Gear checks |
| Tire sink depth | `flightmodel2/gear/tire_sink_depth` | array[1] metres | Off-runway detection |
| Parking brake | `flightmodel/controls/parkbrake` | 0–1 | Manual reference |
| Stall warning | `flightmodel/misc/stall_warning` | — | **404 in XP12 — needs correct dataref** |
| Damage | `flightmodel2/misc/has_crashed` | bool | Crash flag (structural only) |
| Overspeed | `flightmodel/failures/over_vne` | bool | Overspeed flag |
| Flap position | `flightmodel/controls/flaprat` | 0–1 | Flaps retracted in cruise |
| Pitot heat | `cockpit/switches/pitot_heat_on` | bool | On while airborne |
| Landing lights | `cockpit2/switches/landing_lights_on` | bool | On during approach/landing |
| Beacon | `cockpit/electrical/beacon_lights_on` | bool | On while engines running |
| Aircraft name | `aircraft/view/acf_ui_name` | base64 string | Report metadata |

### System State Checks (phase-based, best-effort)

These are not checklist items — they are observable system states checked at the
appropriate phase of flight. False positives are possible on aircraft that auto-manage
systems. Each item can only flag once per flight.

| Check | What's Verified | Phase |
|---|---|---|
| Pitot heat | Should be ON | Airborne |
| Flaps | Should be retracted | Cruise (> 5 min airborne) |
| Gear | Should be retracted | Cruise |
| Gear | Should be extended | Short final (< 500ft AGL) |
| Landing lights | Should be ON | Approach / Airborne below 500ft AGL / Landed |
| Beacon | Should be ON | Engines running |

---

## Flight Phases

```
Idle → Taxiing → Airborne → [Cruise → Approach →] Landed
```

Cruise and Approach are optional — pattern/circuit flights may go directly Airborne → Landed.
Approach event detection also fires in the Airborne phase when below 2,000ft AGL and descending,
so short-pattern flights are scored correctly even without a formal Cruise phase.

| Phase | Trigger |
|---|---|
| Idle | App started, waiting for movement |
| Taxiing | Groundspeed > 2kt, on ground |
| Airborne | `on_ground` transitions false |
| Cruise | > 1,000ft AGL, climb rate < 500fpm for 30 consecutive seconds |
| Approach | Descending below 5,000ft AGL at > 300fpm from Cruise |
| Landed | `on_ground` transitions true |

**Recording is manual only.** Start/End Recording via tray menu. No automatic stop on parking brake or timeout.

**Touch-and-go**: Landed → Airborne transition requires AGL > 10ft to filter out runway bumps and terrain mesh errors.

---

## CheckRide Events

Events are logged with timestamp, phase, and a plain-English description shown in the report.

| Event | Trigger | Deduction |
|---|---|---|
| `OVERSPEED` | `over_vne` flag true | −5 per occurrence |
| `STALL` | `stall_warning` above threshold | −5 per occurrence |
| `HIGH_G` | G > 2.5 (rising edge) | −3 per occurrence |
| `VERY_HIGH_G` | G > 3.5 (rising edge) | −8 per occurrence |
| `CRASH` | `has_crashed` true | −40 + automatic F |
| `RUNWAY_EXCURSION` | Off surface at speed after landing, or AGL negative at speed | −35 + automatic F |
| `HARD_LANDING` | Touchdown VS > 600 fpm | −15 |
| `FIRM_LANDING` | Touchdown VS 300–600 fpm | −5 |
| `HIGH_DESCENT_RATE` | VS < −1,500 fpm below 1,000ft AGL | −5 |
| `EXCESSIVE_APPROACH_SPEED` | IAS > 175kt below 1,000ft AGL | −15 |
| `UNSTABLE_APPROACH` | VS < −1,000 fpm or IAS > 150kt below 500ft AGL | −15 |
| `GEAR_UP_LANDING` | Gear not extended at touchdown | −25 |
| `OFF_RUNWAY` | Tire sink depth > 0.05m at > 20kt during taxi or rollout | −20 |
| `SYSTEM_PITOT_HEAT` | Pitot heat off while airborne | −3 |
| `SYSTEM_FLAPS_CRUISE` | Flaps extended in cruise (> 5 min airborne) | −3 |
| `SYSTEM_GEAR_CRUISE` | Gear extended in cruise | −3 |
| `SYSTEM_GEAR_APPROACH` | Gear not down below 500ft AGL | −3 |
| `SYSTEM_LANDING_LIGHTS` | Landing lights off below 500ft AGL or on approach | −3 |
| `SYSTEM_BEACON` | Beacon off while airborne or taxiing | −3 |

---

## Scoring

Base score: **100 points**. Score is capped at 100 maximum.

### Deductions

See event table above. System flag deductions are −3 per flag.

### Bonuses

| Achievement | Bonus |
|---|---|
| Smooth landing (< 150 fpm) | +5 |
| Greaser (< 75 fpm) | +10 (replaces smooth) |
| No system state flags | +5 |
| No overspeed or stall events | +5 |

Landing quality bonuses are suppressed if the flight ended in a crash or runway excursion.

### Grade Bands

| Score | Grade | Condition |
|---|---|---|
| 95–100 | S — Exceptional | |
| 85–94 | A — Proficient | |
| 70–84 | B — Solid | |
| 50–69 | C — Needs Work | |
| 30–49 | D — Review Required | |
| < 30 | F — Failed | |
| — | F — Failed | Automatic on crash or runway excursion |

---

## Scoring Versioning

Every `CheckRideReport` includes a `ScoringVersion` field (e.g., `"1.1"`).

**Why this matters**: Scoring logic changes between app versions — new events added, penalties adjusted. A score produced under v1.0 rules is not directly comparable to one produced under v1.2 rules. The version field makes this explicit.

**Client version gating**: When Supabase sync is added (Phase 2), the server will check the client's app version on login. If the client is not on the current expected version, the user is shown an "update required" message and sync is blocked. This ensures the database never contains mixed-version scores.

**Leaderboards**: Each score on the leaderboard displays the scoring version that produced it (e.g., a `v1.1` badge). Users can filter by version. This keeps comparisons fair as the algorithm evolves.

**Bumping the version**: Increment `ScoringVersion` in `CheckRideReport.cs` any time detection logic or penalties change — not just when new features are added.

---

## CheckRideResult (uploaded to Supabase)

```json
{
  "id": "uuid",
  "user_id": "uuid",
  "flight_id": "saved_flights.id",
  "sim": "xplane12",
  "scoring_version": "1.1",
  "aircraft": "King Air 350",
  "recorded_at": "2026-06-29T03:20:38Z",
  "score": 87,
  "grade": "A",
  "dep_icao": "KPGD",
  "arr_icao": "KFFO",
  "flight_time_sec": 9840,
  "landing_vs_fpm": -68,
  "max_g": 1.41,
  "events": [
    { "type": "SystemPitotHeat", "phase": "Airborne", "ts": 191, "desc": "Pitot heat off in flight" }
  ],
  "summary": {
    "overspeed_count": 0,
    "stall_count": 0,
    "high_g_count": 0,
    "system_flags": 1,
    "crashed": false,
    "runway_excursion": false,
    "landing_quality": "Greaser"
  }
}
```

---

## Supabase Table: `checkride_results`

```sql
create table checkride_results (
  id uuid primary key default gen_random_uuid(),
  user_id uuid references auth.users(id) on delete cascade,
  flight_id uuid references saved_flights(id) on delete cascade,
  sim text not null,
  scoring_version text not null,
  aircraft text,
  score int not null,
  grade text not null,
  dep_icao text,
  arr_icao text,
  flight_time_sec int,
  landing_vs_fpm int,
  max_g numeric(4,2),
  crashed boolean default false,
  runway_excursion boolean default false,
  events jsonb default '[]',
  summary jsonb default '{}',
  recorded_at timestamptz default now()
);

alter table checkride_results enable row level security;
create policy "Users manage own checkrides"
  on checkride_results for all using (auth.uid() = user_id);
```

---

## Website Integration — My Flights Card

Each card gets a **CheckRides** section below the existing details:

```
CHECKRIDES  [+ Start New Run]

  Jun 29, 2026  ·  X-Plane 12  ·  King Air 350    A  87  v1.1  ↑
  Jun 28, 2026  ·  X-Plane 12  ·  King Air 350    F  29  v1.1
  Jun 25, 2026  ·  X-Plane 12  ·  King Air 350    C  61  v1.0
```

Clicking a row expands the full report: score breakdown, event list with timestamps
and plain-English descriptions, landing VS, max G, and system flags.

Progress arrow (↑ improving / → steady / ↓ declining) shown when 3+ runs exist.
Leaderboards filter by scoring version by default — only like-version scores are compared.

---

## Client App — Key Differences from Career Client

| | Career Client | CheckRide Client |
|---|---|---|
| Mission selection | Required | Optional (any My Flight) |
| Flight validation | Must match dep/arr | None |
| Report content | Basic flight stats | Full graded event log |
| Score | N/A | Yes — 0–100 with grade |
| Multi-run | One per mission | Unlimited per flight card |

`CheckRideSession` wraps `FlightTracker` and adds the event logger and scorer.
Career mode later wraps `CheckRideSession` and adds mission matching on top.

---

## Phase 1 — Local Prototype (in progress)

**Goal:** Prove the monitoring and scoring engine works against a real XP12 session.
No Supabase, no login, no flight picker. Just a tray app that records a flight to disk.

### What it is
- .NET 6 WinForms app with a system tray icon
- Right-click menu: **Start Recording** / **End Recording**
- X-Plane 12 only (REST API on `localhost:8086/api/v3/`)
- Writes two files to disk on each session:
  - `checkride_YYYYMMDD_HHMMSS.log` — running timestamped debug log (every poll cycle)
  - `checkride_YYYYMMDD_HHMMSS.json` — final `CheckRideReport` on end recording

### Project structure
```
CheckRide/
  CheckRide.sln
  CheckRide/
    Program.cs           ← WinForms entry point
    TrayApp.cs           ← NotifyIcon + ContextMenuStrip
    XP12Connector.cs     ← HTTP polling of XP12 REST API v3
    FlightMonitor.cs     ← phase state machine + event detection + scoring
    EventLogger.cs       ← writes running .log file
    ReportWriter.cs      ← serialises final CheckRideReport to .json
    Models/
      FlightPhase.cs
      FlightEvent.cs
      CheckRideReport.cs
    samples/             ← saved session logs for review/regression testing
```

### Tray behaviour
- **Idle** — right-click shows "Start Recording" (enabled) + "End Recording" (greyed)
- **Recording** — shows "Recording…", both menu items swap state
- **End Recording** — stops polling, scores the session, writes both files, shows a Windows notification with the score and output path

### Output files location
`%USERPROFILE%\Documents\CheckRide\` (created if not present)

### Phase 2 (after prototype is validated)
- Add login + Supabase upload on end recording
- Add flight picker (My Flights list from API)
- Replace tray notification with a brief results window
- Server-side version gating on sync

---

## Full Build Order (post-prototype)

1. ~~Phase 1 prototype~~ (in progress)
2. Supabase table + RLS policies
3. Login + flight picker UI
4. Supabase upload on end recording (with client version check)
5. Website: CheckRides section in My Flights cards + report view + leaderboard versioning
6. MSFS connector
7. XP11 connector (UDP, most limited)
8. Mac client

---

## Open Questions

- **Correct stall warning dataref** — `sim/flightmodel/misc/stall_warning` 404s in XP12. Need correct path.
- **Approach speed threshold** — currently 175kt below 1,000ft as a general turboprop threshold. Should ideally be aircraft-type-aware.
- **System state false positives** — aircraft that auto-manage lights/systems will flag incorrectly. Option: per-run toggle to disable system checks, or a known aircraft list.
- **WebSocket connector** — current REST polling (20 requests/sec) works but WebSocket subscriptions at 10Hz would be more efficient and reliable. Good refactor target for Phase 2.
- **Route deviation** — penalise flying way off the planned route? Probably not v1.
- **Multiplayer / shared reports** — out of scope for v1.
