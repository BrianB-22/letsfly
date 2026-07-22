---
name: xp12-web-api-spec
description: XP12 Web API reference — endpoints, dataref naming, confirmed paths, known 404s, and units. Source: live querying + developer.x-plane.com/article/x-plane-web-api/
metadata:
  type: reference
---

# X-Plane 12 Web API — CheckRide Reference

## Overview

XP12 exposes a local REST API for reading/writing datarefs and sending commands.
- **Base URL:** `http://localhost:8086/api/v3/`
- **Authentication:** None (localhost only). Returns 403 if "Disable Incoming Traffic" is enabled in XP12 settings.
- **API Version:** v3 (introduced in XP12 12.4.0). Check `/api/capabilities` for supported versions.
- **No rate limiting** documented; empirically, hammering 46+ sequential requests can trigger timeouts.

---

## Key Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/v3/datarefs?filter[name]=<name>` | Resolve dataref name → numeric ID (exact match, case-sensitive) |
| `GET` | `/api/v3/datarefs/{id}/value` | Read current value |
| `PATCH` | `/api/v3/datarefs/{id}/value` | Write value |
| `GET` | `/api/v3/capabilities` | List supported API versions |

**Important:** Dataref IDs are **session-local** — always re-resolve by name each session.

---

## Data Types

| Type | JSON shape | Access |
|------|-----------|--------|
| `float` / `double` | `{ "data": 1.23 }` | `.GetDouble()` |
| `int` | `{ "data": 5 }` | `.GetInt32()` |
| `float_array` | `{ "data": [1.0, 2.0, ...] }` | `.data[index].GetDouble()` |
| `int_array` | `{ "data": [0, 1, ...] }` | `.data[index].GetInt32()` |
| `data` (string) | `{ "data": "<base64>" }` | Base64 → UTF-8, trim null bytes |

---

## Resolution Behaviour

- `filter[name]` is exact-match and **case-sensitive**.
- HTTP 404 or `error_code: "invalid_dataref_name"` = dataref does not exist in this XP12 build/aircraft.
- Once a dataref 404s, **never retry** — cache in `_notFound` set.
- Datarefs may 404 during flight loading (XP12 not fully initialised). Wait for `sim/flightmodel2/gear/on_ground` to resolve before trusting any data.
- On timeout mid-pass, skip that item and retry unresolved ones next poll cycle — do not bail the whole pass.

---

## Confirmed Working Datarefs (live-tested, XP12 12.x, King Air 350)

### Position
| Dataref | Type | Units | Notes |
|---------|------|-------|-------|
| `sim/flightmodel/position/latitude` | double | degrees | |
| `sim/flightmodel/position/longitude` | double | degrees | |
| `sim/flightmodel/position/groundspeed` | float | **m/s** | × 1.94384 → knots |
| `sim/flightmodel/position/indicated_airspeed` | float | **kias** | Already in knots |
| `sim/flightmodel/position/vh_ind_fpm` | float | fpm | Vertical speed |
| `sim/flightmodel/position/elevation` | double | m MSL | × 3.28084 → feet |
| `sim/flightmodel/position/y_agl` | float | m AGL | × 3.28084 → feet; garbage during loading |
| `sim/flightmodel/position/alpha` | float | degrees | Angle of attack |
| `sim/flightmodel/position/phi` | float | degrees | Bank angle (+right) |
| `sim/flightmodel/position/theta` | float | degrees | Pitch angle (+nose up) |
| `sim/flightmodel/position/mag_psi` | float | degrees | Magnetic heading |
| `sim/flightmodel/position/hpath` | float | degrees | GPS track (true) |
| `sim/flightmodel/position/P` | float | deg/sec | Roll rate |
| `sim/flightmodel/position/Q` | float | deg/sec | Pitch rate |
| `sim/flightmodel/position/R` | float | deg/sec | Yaw rate |

### Forces
| Dataref | Type | Units | Notes |
|---------|------|-------|-------|
| `sim/flightmodel/forces/g_nrml` | float | G | Normal (vertical) |
| `sim/flightmodel/forces/g_side` | float | G | Lateral (crooked landings) |
| `sim/flightmodel/forces/g_axil` | float | G | Axial/longitudinal (braking) |
| `sim/flightmodel/forces/fnrml_gear` | float | Newtons | Total normal force on all gear — useful for touchdown impact |

### Gear / Ground Contact
| Dataref | Type | Index | Notes |
|---------|------|-------|-------|
| `sim/flightmodel2/gear/on_ground` | int_array | [0]=nose [1]=L-main [2]=R-main | 1 = on ground |
| `sim/flightmodel2/gear/deploy_ratio` | float_array | [0]=nose [1]=L-main [2]=R-main | 0=retracted 1=extended |
| `sim/flightmodel2/gear/tire_vertical_deflection_mtr` | float_array | [0]=nose [1]=L-main [2]=R-main | Tire rubber compression in metres. **Pavement baseline King Air 350: nose≈0.077m, mains≈0.131m.** Replaces missing `tire_sink_depth`. |

### Controls
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/flightmodel/controls/parkbrake` | float | 1.0 = set |
| `sim/flightmodel/controls/flaprat` | float | 0–1 fraction |
| `sim/flightmodel2/controls/speedbrake_ratio` | float | 0–1 |
| `sim/cockpit2/engine/actuators/throttle_ratio` | float_array | [0] = engine 1, 16-element array |

### Engine
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/flightmodel/engine/ENGN_running` | int_array | [0]=eng1 [1]=eng2; 0=off 1=running; 16-element array |
| `sim/cockpit2/engine/indicators/N1_percent` | float_array | [0]=eng1 N1% (prop/power turbine speed). **Universal limit: >100% = over-speed on any turbine.** |
| `sim/cockpit2/engine/indicators/N2_percent` | float_array | [0]=eng1 N2% (gas generator speed). Aircraft-specific limits. |
| `sim/cockpit2/engine/indicators/ITT_deg_C` | float_array | [0]=eng1 inter-turbine temperature °C. **Aircraft-specific limits** — King Air 350 PT6A: 820°C continuous, 1090°C starting. Log for visibility; do not score with fixed threshold. |
| `sim/cockpit2/engine/indicators/engine_speed_rpm` | float_array | [0]=eng1 RPM, 16-element array |
| `sim/cockpit2/engine/actuators/throttle_ratio` | float_array | [0]=eng1 throttle 0–1 |

### Annunciators / Failure Flags
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/cockpit2/annunciators/engine_fires` | int_array | [0]=eng1 [1]=eng2; 1=fire warning active |
| `sim/cockpit2/annunciators/oil_pressure` | int | 1=low oil pressure warning |
| `sim/cockpit2/annunciators/fuel_pressure` | int | 1=low fuel pressure warning |
| `sim/cockpit2/annunciators/hydraulic_pressure` | int | 1=hydraulic pressure warning |
| `sim/cockpit2/annunciators/low_voltage` | int | 1=electrical/voltage failure |
| `sim/cockpit2/annunciators/master_caution` | int | 1=master caution lit |
| `sim/cockpit2/annunciators/master_warning` | int | 1=master warning lit |
| `sim/cockpit2/annunciators/gear_unsafe` | int | 1=gear unsafe / in transit |
| `sim/flightmodel/failures/frm_ice` | float | 0=no damage; >0.01=icing damage to airframe |
| `sim/flightmodel/failures/over_g` | int | 1=XP12 registered structural G overload |
| `sim/operation/failures/rel_engfir0` | int | Engine 1 fire failure flag |
| `sim/operation/failures/rel_engfir1` | int | Engine 2 fire failure flag |

### Aircraft Performance Limits (King Air 350 live values)
> **Path changed in XP12:** moved from `sim/aircraft/limits/*` → `sim/aircraft/view/acf_*`

| Dataref | Type | Value (King Air 350) | Notes |
|---------|------|---------------------|-------|
| `sim/aircraft/view/acf_Vso` | float | **83 kias** | Stall speed, landing config. Vref = 1.3 × 83 = 108 kt |
| `sim/aircraft/view/acf_Vs` | float | **98 kias** | Stall speed, clean config |
| `sim/aircraft/view/acf_Vno` | float | **182 kias** | Normal operating speed |
| `sim/aircraft/view/acf_Vne` | float | **400 kias** | Never exceed — suspiciously high for King Air 350 (real Vmo≈180kt); may be placeholder |
| `sim/aircraft/view/acf_Vfe` | float | **200 kias** | Max flaps extended |
| `sim/aircraft/view/acf_Vle` | — | **404** | Max gear extended — not found under any tested path |

### Systems
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/cockpit/switches/pitot_heat_on` | int | 1 = on |
| `sim/cockpit2/switches/landing_lights_on` | int | 1 = on |
| `sim/cockpit/electrical/beacon_lights_on` | int | 1 = on |
| `sim/cockpit2/switches/strobe_lights_on` | int | 1 = on |
| `sim/cockpit/radios/transponder_mode` | int | 0=off 1=stby 2=on 3=test 4=ALT |
| `sim/cockpit2/autopilot/autopilot_on` | int | 1 = engaged |
| `sim/cockpit2/annunciators/stall_warning` | int | 1 = stall warning active. **Replaces missing `sim/flightmodel/misc/stall_warning`** |
| `sim/cockpit2/gauges/indicators/slip_deg` | float | degrees | Ball/slip indicator; useful for coordinated flight checks |

### Navigation
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/cockpit/radios/nav1_vdef_dot` | float | ILS glideslope deviation, dots |
| `sim/cockpit/radios/nav1_hdef_dot` | float | ILS localizer deviation, dots |

### Aircraft Info
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/aircraft/view/acf_ui_name` | data (base64) | Aircraft UI name string |

### Anti-ice / Temperature
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/cockpit/switches/anti_ice_on` | int | 1=anti-ice system on |
| `sim/weather/aircraft/temperature_ambient_deg_c` | float | OAT in °C at aircraft altitude. Icing risk: 0°C to −20°C + cloud coverage > 0.1 |

### Time of Day
| Dataref | Type | Notes |
|---------|------|-------|
| `sim/time/local_time_sec` | float | Seconds since midnight (local sim time) |
| `sim/graphics/scenery/sun_pitch_degrees` | float | Sun elevation; < 0 = night |

### Weather (XP12 — confirmed working paths)
| Dataref | Type | Index / Notes |
|---------|------|--------------|
| `sim/weather/aircraft/wind_speed_kts` | float_array | **13 elements** by altitude level. [0] = surface wind. See altitude levels below. |
| `sim/weather/aircraft/wind_direction_degt` | float_array | **13 elements** by altitude level. [0] = surface wind direction (°T). |
| `sim/weather/aircraft/wind_altitude_msl_m` | float_array | **13 elements** — MSL altitude in metres for each wind array index. [0]=0m [1]=540m [2]=988m ... [12]=16179m |
| `sim/weather/visibility_reported_m` | float | Horizontal visibility in metres (e.g. 51410m ≈ 28nm = good VFR) |
| `sim/weather/region/cloud_base_msl_m` | float_array | 3 cloud layers; [0]=lowest. Metres MSL. Subtract airport elevation for AGL. |
| `sim/weather/region/cloud_coverage_percent` | float_array | 3 cloud layers; [0]=lowest. **Scale is 0–1 (not 0–100 despite name).** e.g. 0.498 = ~50% coverage. |
| `sim/weather/region/turbulence` | float_array | **13 elements**, not 3 as previously assumed — same element count as the wind arrays. Confirmed live 2026-07-19 (King Air 350, FL280): values ~0.01–0.02, but two readings taken ~2.5min apart during a subjective bumpy→smooth transition were nearly identical. Likely a regional forecast value like cloud coverage, not a live felt-turbulence signal — **not yet trusted, needs more contrast testing**. See `todo.md` CheckRide Scoring section. |

---

## Datarefs Confirmed 404 (do not retry)

| Dataref | Reason / Replacement |
|---------|---------------------|
| `sim/aircraft/limits/Vso` | Path changed → `sim/aircraft/view/acf_Vso` |
| `sim/aircraft/limits/Vno` | Path changed → `sim/aircraft/view/acf_Vno` |
| `sim/aircraft/limits/Vne` | Path changed → `sim/aircraft/view/acf_Vne` |
| `sim/aircraft/limits/Vfe` | Path changed → `sim/aircraft/view/acf_Vfe` |
| `sim/aircraft/limits/Vle` | No replacement found — Vle not exposed in tested aircraft |
| `sim/flightmodel/misc/stall_warning` | Replaced by `sim/cockpit2/annunciators/stall_warning` |
| `sim/flightmodel2/gear/tire_sink_depth` | Replaced by `sim/flightmodel2/gear/tire_vertical_deflection_mtr` |
| `sim/flightmodel2/gear/tire_skid` | No replacement found yet |
| `sim/weather/aircraft_wind_speed_kt` | Path changed → `sim/weather/aircraft/wind_speed_kts` |
| `sim/weather/aircraft_wind_direction_degt` | Path changed → `sim/weather/aircraft/wind_direction_degt` |
| `sim/weather/cloud_base_msl_m` | Path changed → `sim/weather/region/cloud_base_msl_m` |
| `sim/time/paused` | Not available via Web API |

---

## Tire Deflection Readings (King Air 350, engines off)

`sim/flightmodel2/gear/tire_vertical_deflection_mtr` measures **rubber compression**, not surface sinkage.

| Surface | Nose [0] | L-Main [1] | R-Main [2] |
|---------|----------|------------|------------|
| Pavement | 0.0768m | 0.1307m | 0.1309m |
| Grass (off-runway) | 0.0887m | 0.1265m | 0.1260m |
| Delta | +0.012m | -0.004m | -0.005m |

**Conclusion: NOT suitable for off-runway detection.** The nose deflection increases ~+0.012m on grass vs pavement but does not scale with depth, and no surface type or friction datarefs are exposed via the Web API. XP12 simulates ground drag internally but does not surface it as a readable value.

Additional surface datarefs tried and confirmed 404: `ground/surf_type`, `ground/ground_friction`, `ground/on_ground_ratio`, `ground/veh_on_ground`, `failures/ground_rough`.

`fnrml_gear` also barely changes (56694N grass vs 56967N pavement — 0.5% delta, unreliable).

**Off-runway detection is removed from scoring** — no viable dataref exists in XP12 Web API. Gear indices [3]+ are always 0.0 on King Air 350 (only 3 gear legs).

---

## XP12 Weather System Architecture

XP12 overhauled weather in 12.x. Key changes from XP11:
- Old `sim/weather/aircraft_*` flat datarefs **moved** to `sim/weather/aircraft/` namespace
- Wind arrays expanded from 3 → **13 altitude levels**
- Cloud/wind/turbulence are true arrays, not array-like notation
- **Use index [0]** for surface/low-altitude wind (0m MSL)
- Cloud base stored as **MSL**, not AGL — subtract airport elevation to get AGL ceiling
- `sim/weather/region/cloud_coverage_percent` scale is **0.0–1.0** despite "percent" in name

---

## Engine State Detection

`sim/flightmodel/engine/ENGN_running` int_array, 16 elements:
- `0` = engine off / not running
- `1` = engine running

Check `ENGN_running[0]` (engine 1) for beacon-on-engines-running checks. King Air 350 has 2 engines: indices [0] and [1].

---

## Empirical API Quirks

1. **Flight loading gap:** Most datarefs 404 until XP12 finishes placing the aircraft. Wait for `on_ground` to resolve.
2. **Timeout cascade:** Rapid sequential requests (~46+) can cause XP12 API to queue-block. Skip timed-out items, retry next cycle.
3. **AGL garbage during load:** `y_agl` returns −8M ft or −1,046ft during aircraft placement. Normal ground = ~0.
4. **Post-crash freeze:** IAS and VS freeze at last value while GS→0. Reliable crash signature.
5. **Groundspeed is m/s:** `sim/flightmodel/position/groundspeed` returns m/s. Multiply by 1.94384 for knots. `indicated_airspeed` is already in kias — no conversion needed.
6. **Vne=400 anomaly:** King Air 350 `acf_Vne` returns 400 kias — likely a Laminar placeholder. Real King Air 350 Vmo ≈ 180 kias. Use `acf_Vno` (182 kias) as the practical upper speed limit for this aircraft.

---

## Unit Conversion Reference

| From | To | Factor |
|------|----|--------|
| m/s | knots | × 1.94384 |
| metres | feet | × 3.28084 |
| Cloud base MSL → AGL | subtract airport elevation (ft) | |
| Vref | Vso × 1.3 | Approach speed threshold |
