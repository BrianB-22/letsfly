# CheckRide — Project Status

## What's built (Phase 1)
.NET 8 WinForms system tray app. Connects to XP12 Web API v3 at `http://localhost:8086/api/v3/`.
Auto-detects when a flight loads (probes latitude, not on_ground — works airborne or on ground).
Produces a graded JSON report + text log in `[MyDocuments]\CheckRide\` (OneDrive-redirected on this machine: `C:\Users\Admin\OneDrive\Documents\CheckRide\`).

## Detection coverage
- Phase transitions: Idle → Taxiing → Airborne → Cruise → Approach → Landed
- Speed events: Overspeed (IAS > Vno), FlapOverspeed (IAS > Vfe with flaps), Stall warning
- G-force: HighG (>2.5g), VeryHighG (>3.5g)
- Bank: HighBank (>45°), VeryHighBank (>60°)
- Landing quality: Greaser / Smooth / Firm / Hard + lateral G sideload
- Approach: Unstable, ExcessiveSpeed, HighDescentRate
- Gear: GearUpLanding, RunwayExcursion
- Systems: PitotHeat, LandingLights, Beacon, Strobes, Transponder, AntiIce, Barometer, **IMC** (vis < 3SM or cloud coverage > 75%)
- Taxi: **TaxiFastSpeed** (GS > 25kt + lateral G or hdg rate), **TaxiAggressiveTurn** (Gl > 0.3G)
- Takeoff: **TakeoffLowPower** (throttle < 85% at liftoff), **TakeoffHeadingDeviation** (>20° from departure hdg below 500ft AGL), **TakeoffDirectionalControl** (Gl > 0.4G below 200ft AGL)
- Failures: EngineFire, EngineOut, EngineOverspeed (N1>100%), OilPressure, FuelPressure, Hydraulic, LowVoltage, OverG, IcingDamage
- Engine monitoring: N1%/N2%/ITT logged every tick (N1>100% scored, ITT logged only)
- OAT, wind, visibility, cloud coverage logged every 30s as FlightConditionSample

## Scoring
All thresholds and weights in `ScoringConfig` static class at top of `FlightMonitor.cs`.
Scoring version: `xp12-1.19` (const `CheckRideReport.ScoringVersionConst` — bump on any change to detection/penalty behavior)

## Fixed bugs (scoring re-trigger / false-positive)
Found 2026-08-10 from real production data (two live user flights, one C-graded one D-graded) pasted by the user, who suspected re-triggers were tanking scores. Confirmed both:
1. **`SpeedLimitViolation` re-trigger** — was a raw edge-trigger at exactly 250kt with no hysteresis (unlike `Overspeed`, which has `OverspeedResetHysteresisKts`). An aircraft holding 250kt on autothrottle through a 250kt-restricted climb re-armed on every sub-knot flicker, firing 4x for one continuous compliant segment (-40 pts on a real A319 flight). Fixed by adding a Schmitt-trigger latch (`SpeedLimitResetHysteresisKts = 5.0`), mirroring the Overspeed pattern.
2. **System-failure false positives at cold-and-dark** — `OilPressureLow`/`FuelPressureLow`/`HydraulicPressureLow`/`LowVoltage` had no gate on engine state, so they fired (once each, but simultaneously) the moment a cold-and-dark aircraft was loaded — before engine start, before any systems were powered. Real flight: all 4 fired at T=1020 while phase was still `Idle`, ~700s before `EngineStart` — cost -40 pts before the pilot touched the throttle, on an otherwise clean flight (D grade, should've been much higher). Fixed by gating all 4 checks behind `anyEngineRunning`.

Both fixed in `FlightMonitor.cs` DetectEvents/DetectFailures; version bumped 1.16→1.19.

## Key XP12 / King Air 350 findings
- `sim/cockpit2/engine/actuators/throttle_ratio` reads **1.0 for both full forward AND full reverse** on King Air 350 turboprops — ambiguous without `prop_in_beta`
- Added `sim/flightmodel2/engines/prop_in_beta` → `PropInBeta` bool on snapshot — tick log now shows `Rev=True/False`
- `sim/flightmodel2/gear/on_ground` index 0 = **nose gear only** (lifts first during rotation — not a reliable "all wheels off" indicator)
- `CloudBaseAglM` from `sim/weather/region/cloud_base_msl_m` is **MSL altitude**, not AGL — field name in code is misleading; ceiling AGL calculation requires airport elevation
- `StallW=1.00` confirmed fires for King Air 350 stick shaker at ~1.2× stall speed (not aerodynamic stall)
- `acf_Vne` = 400kt anomaly for King Air 350 (real Vmo ~180kt)

## Known XP12 Web API limitations
- No surface type datarefs (asphalt/grass/dirt) — exhaustively tested, all 404
- Vle — not exposed; using `acf_Vs` (clean stall speed) as fallback
- ITT/torque limits are aircraft-specific — not scored

## Needs testing (next session)
1. **Taxi detection** — do an erratic taxi run; confirm TaxiFastSpeed and TaxiAggressiveTurn appear in log/JSON
2. **Takeoff detection** — deliberate low-power or crooked takeoff; confirm TakeoffLowPower / TakeoffHeadingDeviation / TakeoffDirectionalControl fire
3. **PropInBeta** — check next flight rollout for `Rev=True` in tick log to confirm `sim/flightmodel2/engines/prop_in_beta` resolves in XP12
4. **Small-talk callout** — long cruise flight (or shortened test constants) to confirm `CalloutSmallTalk` fires every ~30-40 min while airborne and plays a random `sounds\small_talk\*.wav`

## Pending decisions
- Vne=400kt: cap at Vno or ignore?
- Per-aircraft engine limit config file (for ITT scoring on specific airframes)
- MSFS support (future)
