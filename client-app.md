# SimCareer Tracker — Client App

A C# Windows WPF app that connects to your flight sim, captures flight telemetry, and posts
completed flight data to the SimCareer API.

**UI:** System tray icon (shows connection status) + a simple WPF window for the mission
panel and login. Click the tray icon to open the window. App stays alive in the tray while
you fly — no window needed during the flight itself.

**Phases:**
- Console app — initial development and testing, proves the data capture pipeline
- WPF app — full UI with mission panel, login form, status indicators

---

## Design Goals

- Modular — one standard data format, pluggable connectors per sim
- Sim-agnostic core — FlightTracker logic knows nothing about X-Plane or MSFS
- Easy to extend — adding a new sim is just a new connector project
- Lightweight — runs in background, low CPU, no UI during flight

---

## Architecture

### `ISimConnector` — interface each sim implements
- `Connect()` / `Disconnect()`
- `IsConnected` property
- Fires `FlightDataReceived` event with a `FlightDataSnapshot` every ~1 second

### `FlightDataSnapshot` — normalized format all connectors output
```csharp
double   Latitude
double   Longitude
double   GroundspeedKts
double   VerticalSpeedFpm
double   AltitudeMslFt
bool     OnGround
bool     ParkingBrakeSet
double   FuelTotalKg
double   GForceNormal
bool     HasCrashed
bool     Overspeed
string   AircraftName
DateTime Timestamp
```

### `FlightTracker` — core state machine, consumes snapshots
States: **Idle → Airborne → Landed → Complete**

- Idle: waiting for takeoff (groundspeed > threshold, OnGround transitions false)
- Airborne: recording flight — tracking max G, overspeed events, crash flag
- Landed: touchdown detected (OnGround true) — captures landing VS, arrival lat/lon
- Complete: parking brake set + groundspeed < 1kt — builds final FlightRecord

Knows nothing about sims. Purely driven by FlightDataSnapshot stream.

### `FlightRecord` — the output of a completed flight
```csharp
string   DepIcao
double   DepLat, DepLon
string   ArrIcao
double   ArrLat, ArrLon
DateTime DepartureTime
DateTime ArrivalTime
DateTime ParkTime
int      FlightTimeSec
double   FuelStartKg
double   FuelEndKg
double   FuelUsedKg
double   LandingVsFpm
LandingQuality LandingQuality   // Smooth / Normal / Hard / VeryHard
double   MaxGForce
bool     HasCrashed
bool     HadOverspeed
string   AircraftName
```

**Landing quality thresholds:**
- Smooth: < 100 fpm
- Normal: 100–300 fpm
- Hard: 300–600 fpm
- Very hard: > 600 fpm

### `AirportMatcher`
- Loads airports.json once at startup
- `FindNearest(lat, lon, maxRangeNm)` — Haversine distance calc, returns nearest airport
- Used at takeoff (departure) and touchdown (arrival)
- Match radius: ~3nm

---

## Connectors

### `XPlane12Connector`
- X-Plane 12 local REST API on `localhost:8086`
- Must be enabled in XP12 settings
- Polls datarefs via HttpClient every 1 second

**Key datarefs:**
| Dataref | Used for |
|---|---|
| `sim/flightmodel/position/latitude` | Position |
| `sim/flightmodel/position/longitude` | Position |
| `sim/flightmodel/position/groundspeed` | Takeoff/landing detection (m/s) |
| `sim/flightmodel/position/vh_ind_fpm` | Vertical speed |
| `sim/flightmodel/position/elevation` | Altitude MSL |
| `sim/flightmodel/controls/parkbrake` | Flight-ended trigger |
| `sim/flightmodel/weight/m_fuel_total` | Fuel weight in kg |
| `sim/flightmodel/forces/g_nrml` | Normal G-force |
| `sim/flightmodel2/misc/has_crashed` | Crash detection |
| `sim/flightmodel/failures/over_vne` | Overspeed |
| `sim/flightmodel2/gear/on_ground` | On ground (array) |
| `sim/aircraft/view/acf_ui_name` | Aircraft name |

### `XPlane11Connector`
- Reads UDP broadcast on `localhost:49000`
- User must enable "Send network data" in XP11 settings and configure data groups
- More limited dataref access than XP12 REST API
- Infer on-ground from groundspeed + AGL altitude

### `MsfsConnector`
- SimConnect SDK (Microsoft, native C# support)
- Subscribe to system events: `Crashed`
- Poll SimVars via SimConnect data definitions
- Windows only (SimConnect is MSFS-specific)

---

## Project Structure

```
SimCareerTracker/
  SimCareerTracker.Core/
  │   ISimConnector.cs
  │   FlightDataSnapshot.cs
  │   FlightRecord.cs
  │   LandingQuality.cs
  │   FlightTracker.cs
  │   AirportMatcher.cs
  │
  SimCareerTracker.Connectors.XPlane12/
  │   XPlane12Connector.cs
  │   XPlane12DataRefPoller.cs
  │
  SimCareerTracker.Connectors.XPlane11/
  │   XPlane11Connector.cs
  │   XPlane11UdpParser.cs
  │
  SimCareerTracker.Connectors.Msfs/
  │   MsfsConnector.cs
  │   SimConnectWrapper.cs
  │
  SimCareerTracker.App/
      Program.cs              ← console app for now, system tray later
      ConnectorFactory.cs     ← auto-detect sim or read from config
      ApiClient.cs            ← POST FlightRecord to career API
      appsettings.json        ← sim preference, API endpoint, auth token
```

---

## ConnectorFactory — sim auto-detection

On startup, try in order:
1. Ping XP12 REST API (`localhost:8086`) — if responds, use `XPlane12Connector`
2. Check for XP11 UDP broadcast — if receiving packets, use `XPlane11Connector`
3. Try SimConnect connection — if succeeds, use `MsfsConnector`
4. If none found, wait and retry every 30 seconds

User can override auto-detection via `appsettings.json`.

---

## Console App Test Output (target)

```
[10:14:22] SimCareer Tracker started
[10:14:23] Auto-detecting sim... X-Plane 12 found (localhost:8086)
[10:14:45] Takeoff detected
           Airport : KLAX — Los Angeles Intl (0.4nm)
           Fuel    : 1,840 kg
           Aircraft: Cessna 172 Skyhawk
[10:57:12] Touchdown detected
           Airport : KSFO — San Francisco Intl (0.1nm)
           VS      : -187 fpm (Normal)
[10:58:03] Parking brake set — flight complete
           Flight time : 43:21
           Fuel used   : 312 kg
           Max G       : 1.4
           Overspeed   : No
           Crash       : No
           Landing     : Normal
[10:58:03] Posting to SimCareer API...
[10:58:04] Flight recorded ✓
```

---

## Reliability — In-Memory Retry

No local file. On flight complete, POST to API immediately. If the request fails, hold the
FlightRecord in memory and retry until it succeeds or the app is closed. Nothing written to
disk = no tamper surface. Flight loss risk is negligible since the app is running and the
flight just completed.

Server-side sanity checks validate all incoming records regardless:
- Flight time plausible for distance + aircraft cruise speed?
- Fuel usage within reasonable range for aircraft?
- Reject or flag anything that doesn't add up

## Authentication

In-app login form — user enters email + password once. App exchanges credentials for a
Supabase JWT and refresh token, stores in Windows Credential Manager (not a plain text file).
Refresh token keeps the session alive automatically. User only has to log in again if they
explicitly sign out or the refresh token expires.

## Mission Panel

App polls the career API every few minutes and displays all accepted missions. User selects
one before launching their sim. Poll interval refreshes automatically when the app regains
focus.

**Each mission shows:**
- Career type + mission title
- Departure: ICAO + airport name + city
- Arrival: ICAO + airport name + city
- Distance (nm)
- Pay + any bonus condition
- Time remaining before expiry

**Example list:**
```
● ACCEPTED MISSIONS (2)

  [1] Cargo Run
      KLAX  Los Angeles Intl, CA
      →
      KSFO  San Francisco Intl, CA
      340nm · $1,200 · +$200 if on time
      Expires in 5 days

  [2] Medevac Transfer
      KBOS  Boston Logan Intl, MA
      →
      KBDL  Bradley Intl, CT
      98nm · $800
      Expires in 2 days

  [Select mission to fly]
```

Once a mission is selected the app enters "ready" state — waiting for the sim to launch
and takeoff to be detected. User can deselect and pick a different mission any time before
takeoff.

## Mission Selection & Matching

Before flying, user selects a mission from the panel. App tags all telemetry with that
mission ID.

On flight complete, FlightRecord is posted with the mission ID. API validates:
- Does dep ICAO match the mission departure?
- Does arr ICAO match the mission arrival?
- Is flight time plausible for the distance and aircraft?
- If validation passes → mission marked complete, earnings + rep applied
- If dep/arr don't match → API flags it for review (wrong mission selected, diverted, etc.)

This keeps mission management on the career site (accept, browse, abandon) while the app
handles pre-flight selection and in-flight capture. Clean separation.
