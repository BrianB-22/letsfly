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

### 1. CheckRide Windows Client (C# WPF)
- Reuses architecture from `client-app.md` (`ISimConnector`, `FlightTracker`, `FlightDataSnapshot`)
- Login via SimLetsFly Supabase credentials (same auth)
- Flight picker: pulls user's My Flights list, user selects one before launching the sim
- Monitors the flight in real-time via sim connector
- Uploads `CheckRideResult` JSON to Supabase on completion

### 2. Supabase Backend
- New table: `checkride_results`
- Links to `saved_flights.id` (many runs per flight card)
- Stores event log + computed score + summary stats

### 3. SimLetsFly Website — CheckRide Report View
- Each My Flights card shows a CheckRides section if runs exist
- Expandable list of runs with date, score, grade, and key stats
- Drill into a run for the full breakdown and event timeline
- Progress trend shown if 3+ runs exist

---

## Simulator Support

| Sim | Connection | Priority |
|---|---|---|
| X-Plane 12 | REST API `localhost:8086` | First |
| X-Plane 11 | UDP broadcast `localhost:49000` | Second |
| MSFS 2020/2024 | SimConnect SDK | Third |

---

## Flight Monitoring

### Core Data (polled every ~1 second via `FlightDataSnapshot`)

| Field | Source (XP12) | Used For |
|---|---|---|
| Latitude / Longitude | `flightmodel/position/lat` `lon` | Route tracking |
| Groundspeed | `flightmodel/position/groundspeed` | Takeoff/landing detection |
| Airspeed (IAS) | `flightmodel/position/indicated_airspeed` | Overspeed, speed management |
| Vertical speed | `flightmodel/position/vh_ind_fpm` | Landing quality, approach rate |
| Altitude MSL | `flightmodel/position/elevation` | Phase detection |
| AoA | `flightmodel/position/alpha` | Stall detection (pre-warning) |
| G-force (normal) | `flightmodel/forces/g_nrml` | Manoeuvre smoothness |
| On ground | `flightmodel2/gear/on_ground` | Phase transitions |
| Gear position | `flightmodel2/gear/deploy_ratio` | Gear-up after takeoff, gear-down on approach |
| Parking brake | `flightmodel/controls/parkbrake` | Flight complete trigger |
| Stall warning | `flightmodel/misc/stall_warning` | Stall events |
| Damage | `flightmodel2/misc/has_crashed` | Crash flag |
| Overspeed | `flightmodel/failures/over_vne` | Overspeed flag |
| Flap position | `flightmodel/controls/flaprat` | Flaps retracted in cruise |
| Pitot heat | `systems/pitot_heat_on` | On while airborne |
| Landing lights | `electrical/landing_lights_on` | On during approach/landing |
| Beacon | `electrical/beacon_lights_on` | On while engines running |
| Aircraft name | `aircraft/view/acf_ui_name` | Report metadata |

### System State Checks (phase-based, best-effort)

These are not checklist items — they are observable system states checked at the
appropriate phase of flight. False positives are possible on aircraft that auto-manage
systems. Each item can only flag once per flight.

| Check | What's Verified | Phase |
|---|---|---|
| Pitot heat | Should be ON | Airborne |
| Flaps | Should be retracted | Cruise (> 5 min airborne) |
| Gear | Should be retracted | Climb (> 400ft AGL, airspeed increasing) |
| Gear | Should be extended | Short final (< 500ft AGL, aligned with runway) |
| Landing lights | Should be ON | Approach / landing |
| Beacon | Should be ON | Engines running |

---

## Flight Phases

```
Idle → Taxiing → Airborne → Cruise → Approach → Landed → Parked (Complete)
```

| Phase | Trigger |
|---|---|
| Idle | App started, waiting for sim |
| Taxiing | Groundspeed > 2kt, on ground |
| Airborne | on_ground transitions false |
| Cruise | > 1,000ft AGL, climb rate < 500 fpm, stable |
| Approach | Descending, heading toward arrival airport |
| Landed | on_ground transitions true |
| Parked | Parking brake set + groundspeed < 1kt |

---

## CheckRide Events

Events are logged with timestamp, phase, and a plain-English description shown in the report.

| Event | Trigger | Description shown |
|---|---|---|
| `OVERSPEED` | `over_vne` flag true | Exceeded Vne |
| `STALL` | `stall_warning` above threshold | Stall warning triggered |
| `HIGH_G` | g > 2.5 | High G-force manoeuvre |
| `VERY_HIGH_G` | g > 3.5 | Excessive G-force |
| `CRASH` | `has_crashed` true | Aircraft damage / crash |
| `HARD_LANDING` | touchdown VS > 600 fpm | Hard landing |
| `FIRM_LANDING` | touchdown VS 300–600 fpm | Firm landing |
| `HIGH_DESCENT_RATE` | VS > 1,500 fpm below 1,000ft AGL | Excessive descent on approach |
| `GEAR_UP_LANDING` | gear not extended at touchdown | Gear-up landing |
| `SYSTEM_PITOT_HEAT` | pitot heat off while airborne | Pitot heat off in flight |
| `SYSTEM_FLAPS_CRUISE` | flaps extended in cruise | Flaps not retracted after climb |
| `SYSTEM_GEAR_CRUISE` | gear extended in cruise | Gear not retracted after takeoff |
| `SYSTEM_GEAR_APPROACH` | gear not down on short final | Gear not down on approach |
| `SYSTEM_LANDING_LIGHTS` | landing lights off on approach | Landing lights off on approach |
| `SYSTEM_BEACON` | beacon off while engines running | Beacon off with engines running |

---

## Scoring

Base score: **100 points**

### Deductions

| Event | Deduction |
|---|---|
| Overspeed (per occurrence) | −5 |
| Stall (per occurrence) | −5 |
| High G > 2.5 (per occurrence) | −3 |
| Very high G > 3.5 (per occurrence) | −8 |
| Crash | −30 |
| Hard landing (> 600 fpm) | −15 |
| Firm landing (300–600 fpm) | −5 |
| High descent rate on approach | −5 |
| Gear-up landing | −25 |
| Each system state flag | −3 |

### Bonuses

| Achievement | Bonus |
|---|---|
| Smooth landing (< 150 fpm) | +5 |
| Greaser (< 75 fpm) | +10 (replaces smooth) |
| No system state flags | +5 |
| No overspeed or stall events | +5 |

Minimum score: 0. Score can exceed 100 with bonuses.

### Grade Bands

| Score | Grade |
|---|---|
| 95+ | S — Exceptional |
| 85–94 | A — Proficient |
| 70–84 | B — Solid |
| 50–69 | C — Needs Work |
| < 50 | D — Review Required |

---

## CheckRideResult (uploaded to Supabase)

```json
{
  "id": "uuid",
  "user_id": "uuid",
  "flight_id": "saved_flights.id",
  "sim": "xplane12",
  "aircraft": "Cessna 172 Skyhawk",
  "recorded_at": "2026-06-28T14:22:00Z",
  "score": 91,
  "grade": "A",
  "dep_icao": "KPGD",
  "arr_icao": "KFFO",
  "flight_time_sec": 9840,
  "landing_vs_fpm": -124,
  "max_g": 1.4,
  "events": [
    { "type": "SYSTEM_PITOT_HEAT", "phase": "airborne", "ts": 142, "desc": "Pitot heat off in flight" },
    { "type": "FIRM_LANDING", "phase": "landed", "ts": 9840, "desc": "Firm landing — 312 fpm" }
  ],
  "summary": {
    "overspeed_count": 0,
    "stall_count": 0,
    "high_g_count": 0,
    "system_flags": 1,
    "crashed": false,
    "landing_quality": "Smooth"
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
  aircraft text,
  score int not null,
  grade text not null,
  dep_icao text,
  arr_icao text,
  flight_time_sec int,
  landing_vs_fpm int,
  max_g numeric(4,2),
  crashed boolean default false,
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

  Jun 28, 2026  ·  X-Plane 12  ·  Cessna 172    A  91   ↑
  Jun 25, 2026  ·  X-Plane 12  ·  Cessna 172    C  61
  Jun 22, 2026  ·  X-Plane 12  ·  Cessna 172    B  74
```

Clicking a row expands the full report: score breakdown, event list with timestamps
and plain-English descriptions, landing VS, max G, and system flags.

Progress arrow (↑ improving / → steady / ↓ declining) shown when 3+ runs exist.

---

## Client App — Key Differences from Career Client

| | Career Client | CheckRide Client |
|---|---|---|
| Mission selection | Required | Optional (any My Flight) |
| Flight validation | Must match dep/arr | None |
| Report content | Basic flight stats | Full graded event log |
| Score | N/A | Yes — 0–100+ with grade |
| Multi-run | One per mission | Unlimited per flight card |

`CheckRideSession` wraps `FlightTracker` and adds the event logger and scorer.
Career mode later wraps `CheckRideSession` and adds mission matching on top.

---

## Phase 1 — Local Prototype (build this first)

**Goal:** Prove the monitoring and scoring engine works against a real XP12 session.
No Supabase, no login, no flight picker. Just a tray app that records a flight to disk.

### What it is
- .NET Windows app with a system tray icon
- Right-click menu: **Start Recording** / **End Recording**
- X-Plane 12 only (REST API on `localhost:8086`)
- Writes two files to disk on each session:
  - `checkride_YYYYMMDD_HHMMSS.log` — running timestamped debug log (every poll cycle)
  - `checkride_YYYYMMDD_HHMMSS.json` — final `CheckRideReport` on end recording

### Project structure
```
CheckRide/
  CheckRide.sln
  CheckRide/
    Program.cs           ← WinForms entry point, creates tray app
    TrayApp.cs           ← NotifyIcon + ContextMenuStrip
    XP12Connector.cs     ← HTTP polling of XP12 REST API (localhost:8086)
    FlightMonitor.cs     ← phase state machine + event detection + scoring
    EventLogger.cs       ← writes running .log file
    ReportWriter.cs      ← serialises final CheckRideReport to .json
    Models/
      FlightPhase.cs
      FlightEvent.cs
      CheckRideReport.cs
```

### Tray behaviour
- **Idle** — grey icon, right-click shows "Start Recording" (enabled) + "End Recording" (greyed)
- **Recording** — coloured icon, shows "Recording… KPGD → KFFO" or just "Recording…"
- **End Recording** — stops polling, scores the session, writes both files, shows a Windows notification with the score and output path

### Output files location
`%USERPROFILE%\Documents\CheckRide\` (created if not present)

### Phase 2 (after prototype is validated)
- Add login + Supabase upload on end recording
- Add flight picker (My Flights list from API)
- Replace tray notification with a brief results window

---

## Full Build Order (post-prototype)

1. ~~Phase 1 prototype~~ (see above)
2. Supabase table + RLS policies
3. Login + flight picker UI
4. Supabase upload on end recording
5. Website: CheckRides section in My Flights cards + report view
6. MSFS connector
7. XP11 connector (UDP, most limited)
8. Mac client

---

## Open Questions

- **System state false positives** — aircraft that auto-manage lights/systems will flag
  incorrectly. Option: per-run toggle to disable system checks, or a known aircraft list.
- **Route deviation** — penalise flying way off the planned route? Probably not v1.
- **Multiplayer / shared reports** — out of scope for v1.
