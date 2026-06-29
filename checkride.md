# CheckRide — Planning Doc

## What It Is

An optional add-on to SimLetsFly. Users download a lightweight Windows client (Mac later)
that connects to their running simulator, monitors a flight from wheels-up to parking brake,
and uploads a graded report to their SimLetsFly account.

Multiple CheckRide runs can be recorded against the same My Flights card — the intent is to
fly the same route repeatedly and watch yourself improve. A score and breakdown are shown on
the SimLetsFly website after each run.

This is also the test bed for the career mode grading engine in the SimCareer project. The
monitoring and scoring logic built here transfers directly.

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
- Stores raw event log + computed score + summary stats

### 3. SimLetsFly Website — CheckRide Report View
- Each My Flights card shows a "CheckRides" section if runs exist
- Expandable list of runs with date, score, and key stats
- Drill into a run to see the full breakdown and event timeline

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
| Airspeed (IAS) | `flightmodel/position/indicated_airspeed` | Overspeed detection |
| Vertical speed | `flightmodel/position/vh_ind_fpm` | Landing quality |
| Altitude MSL | `flightmodel/position/elevation` | Phase detection |
| G-force (normal) | `flightmodel/forces/g_nrml` | Hard manoeuvre detection |
| On ground | `flightmodel2/gear/on_ground` | Phase transitions |
| Parking brake | `flightmodel/controls/parkbrake` | Flight complete trigger |
| Stall warning | `flightmodel/misc/stall_warning` | Stall events |
| Damage | `flightmodel2/misc/has_crashed` | Crash flag |
| Overspeed | `flightmodel/failures/over_vne` | Overspeed flag |
| Aircraft name | `aircraft/view/acf_ui_name` | Report metadata |

### Procedure Monitoring (best-effort, generic)

These are readable datarefs available on most aircraft. False positives possible on
aircraft that auto-manage lights or systems. Noted as "best effort" in the report.

| Procedure | Dataref / Check | Phase |
|---|---|---|
| Pitot heat | `systems/pitot_heat_on` | Airborne |
| Taxi lights | `electrical/taxi_light_on` | Taxi phase |
| Landing lights on | `electrical/landing_lights_on` | Final approach / landing |
| Landing lights off | `electrical/landing_lights_on` | Cruise (if left on) |
| Beacon / strobe | `electrical/beacon_lights_on` | Before engine start → shutdown |
| Nav lights | `electrical/nav_lights_on` | Airborne |
| Flaps retracted | `controls/flaprat` | Cruise (flaps left extended) |

Procedure violations are logged as events with a timestamp and phase. They reduce the
score but do not fail the flight. Each violation type can only deduct once per flight.

---

## Flight Phases

```
Idle → Taxiing → Airborne → Cruise → Approach → Landed → Parked (Complete)
```

| Phase | Trigger |
|---|---|
| Idle | App started, waiting |
| Taxiing | Groundspeed > 2kt, on ground |
| Airborne | on_ground transitions false |
| Cruise | Altitude > 1,000ft AGL, climb rate settling |
| Approach | Altitude descending below cruise, heading toward arrival |
| Landed | on_ground transitions true |
| Parked | Parking brake set + groundspeed < 1kt |

---

## CheckRide Events

During the flight, events are logged with a timestamp and phase:

| Event | Trigger |
|---|---|
| `OVERSPEED` | `over_vne` flag true |
| `STALL` | `stall_warning` above threshold |
| `HIGH_G` | `g_nrml` > 2.5 |
| `VERY_HIGH_G` | `g_nrml` > 3.5 |
| `CRASH` | `has_crashed` true |
| `HARD_LANDING` | touchdown VS > 600 fpm |
| `FIRM_LANDING` | touchdown VS 300–600 fpm |
| `PROCEDURE_*` | any procedure violation (see above) |

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
| Each procedure violation | −3 |

### Bonuses

| Achievement | Bonus |
|---|---|
| Smooth landing (< 150 fpm) | +5 |
| Greaser (< 75 fpm) | +10 (replaces smooth) |
| Zero procedure violations | +5 |
| Zero overspeed / stall events | +5 |

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
  "score": 87,
  "grade": "A",
  "dep_icao": "KPGD",
  "arr_icao": "KFFO",
  "flight_time_sec": 9840,
  "landing_vs_fpm": -182,
  "max_g": 1.4,
  "events": [
    { "type": "PROCEDURE_PITOT_HEAT", "phase": "airborne", "ts": 142 },
    { "type": "FIRM_LANDING", "phase": "landed", "ts": 9840 }
  ],
  "summary": {
    "overspeed_count": 0,
    "stall_count": 0,
    "high_g_count": 0,
    "procedure_violations": 1,
    "crashed": false,
    "landing_quality": "Normal"
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

  Jun 28, 2026  ·  X-Plane 12  ·  Cessna 172    A  87
  Jun 25, 2026  ·  X-Plane 12  ·  Cessna 172    C  61
  Jun 22, 2026  ·  X-Plane 12  ·  Cessna 172    B  74
```

Clicking a row expands the full report: score breakdown, event list with timestamps,
landing VS, max G, flight time, and procedure violations.

Progress trend shown if 3+ runs exist (improving / declining / steady).

---

## Client App — Key Differences from Career Client

| | Career Client | CheckRide Client |
|---|---|---|
| Mission selection | Required (pick a career mission) | Optional (pick any My Flight) |
| Flight validation | Must match mission dep/arr | No validation, any flight |
| Report content | Flight record (basic stats) | Full graded event log |
| Score | N/A | Yes — 0–100+ with grade |
| Multi-run | One per mission | Unlimited per flight card |

The `CheckRideSession` class wraps `FlightTracker` and adds the event logger and scorer.
Career mode later wraps `CheckRideSession` and adds mission matching on top.

---

## Build Order

1. `CheckRideSession` — event logger + scorer on top of existing `FlightTracker`
2. Supabase table + RLS policies
3. Windows client UI — login, flight picker, session status, upload confirmation
4. XP12 connector first (REST API, cleanest datarefs)
5. Website: CheckRides section in My Flights cards + report view
6. MSFS connector second
7. XP11 connector third (UDP, most limited)
8. Mac client last

---

## Open Questions

- **Procedure false positives** — how to handle aircraft that auto-manage lights?
  Option: let user toggle procedure monitoring on/off per run.
- **Route deviation scoring** — penalise flying way off the planned route?
  Probably not for v1 — too complex, GPS drift issues.
- **Multiplayer / shared reports** — out of scope for v1.
- **SimBrief integration** — share a route with CheckRide for more precise procedure timing?
  Future consideration.
