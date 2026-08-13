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
Scoring version: `xp12-1.20` (const `CheckRideReport.ScoringVersionConst` — bump on any change to detection/penalty behavior)
- 1.20 (2026-08-13): diversion handling shipped — Engine Fire/Out no longer cost points; landing away from plan after a declared diversion (`DiversionDeclared`, automatic on engine failure or manual via a new status-bar checkbox) logs `DivertedToAlternate` instead of `WrongArrivalAirport`, no penalty. See todo.md CheckRide Scoring section for full detail.

## Fixed bugs (scoring re-trigger / false-positive)
Found 2026-08-10 from real production data (two live user flights, one C-graded one D-graded) pasted by the user, who suspected re-triggers were tanking scores. Confirmed both:
1. **`SpeedLimitViolation` re-trigger** — was a raw edge-trigger at exactly 250kt with no hysteresis (unlike `Overspeed`, which has `OverspeedResetHysteresisKts`). An aircraft holding 250kt on autothrottle through a 250kt-restricted climb re-armed on every sub-knot flicker, firing 4x for one continuous compliant segment (-40 pts on a real A319 flight). Fixed by adding a Schmitt-trigger latch (`SpeedLimitResetHysteresisKts = 5.0`), mirroring the Overspeed pattern.
2. **System-failure false positives at cold-and-dark** — `OilPressureLow`/`FuelPressureLow`/`HydraulicPressureLow`/`LowVoltage` had no gate on engine state, so they fired (once each, but simultaneously) the moment a cold-and-dark aircraft was loaded — before engine start, before any systems were powered. Real flight: all 4 fired at T=1020 while phase was still `Idle`, ~700s before `EngineStart` — cost -40 pts before the pilot touched the throttle, on an otherwise clean flight (D grade, should've been much higher). Fixed by gating all 4 checks behind `anyEngineRunning`.

Both fixed in `FlightMonitor.cs` DetectEvents/DetectFailures; version bumped 1.16→1.19.

## Opt-in flight log upload (2026-08-10)
Added a "Upload flight log, to improve development" checkbox (`FlightListForm`, bottom-left,
idle-only) so testers can send the raw per-second session log alongside their score for
debugging/scoring-tuning purposes — this is what made the speed-limit/cold-start bugs above
diagnosable from real data in the first place.
- New `checkride_logs` table (`supabase/migrations/20260810000000_add_checkride_logs.sql`),
  FK'd to `checkride_results.id`, RLS: insert-only for the owning user, no SELECT policy for
  anon/authenticated (service_role only) — same privacy posture as the log_text lockdown (H-1).
- `checkride_results.id` is now generated client-side (`Guid.NewGuid()` in
  `SupabaseClient.UploadCheckRideAsync`) instead of relying on the DB default, so it can be
  reused as the FK for the log upload without a round trip.
- The old `checkride_results.log_text` column is no longer written to — superseded, not
  dropped (see `sql-updates-needed.md`).
- Checkbox defaults **checked** (2026-08-10: flipped from unchecked to gather data while
  actively hunting scoring bugs — revisit the default once that settles down) and is not
  persisted across sessions (fast follow if a "remember my choice" pref is wanted later).

## Other UI tweaks (2026-08-10)
- Initial window size 880×560 → 1040×680 (felt cramped).
- "?" icon-only Help button moved from bottom-left to a labeled "Help" button in the top bar.
- Refresh button now has a tooltip ("Refresh Saved Flights in SimLetsFly").
- `LoginForm` titlebar now shows the client version (`CheckRide for SimLetsFly v0.2.7 — Sign In`).
- `LoginForm`'s error label is now a `LinkLabel` (`_lblError`) — any message containing
  `simletsfly.com` or `simletsfly.com/checkride` (e.g. the version-gate rejection message)
  renders that URL as a clickable link via `SetErrorLink()`. Falls back to plain text for
  ordinary auth errors with no URL.
- `OnFormClosing` close-confirmation broadened from `AppState.Recording` only to also
  cover `WaitingXP12` (waiting on X-Plane) and `Uploading` (score not yet saved) — closing
  during any of those now warns before letting the window close.

## Fixed: version gate only checked at login, not for cached sessions (2026-08-10)
`Program.cs` skips `LoginForm` entirely when `SessionStore.Load()` returns a cached
session — but `VerifyClientAsync` only used to be called from inside `LoginForm.OnLogin`.
So once anyone had ever logged in successfully, they could keep running a blocked,
out-of-date exe forever without ever hitting the gate again. Fixed:
- `SupabaseClient.VerifyClientAsync` is now an instance method (was `static`, took a raw
  token) — calls `EnsureTokenAsync()` first so it also works against a stale cached
  session, not just a token fresh off `LoginAsync`.
- `Program.cs` now calls it against any cached session before launching `TrayApp`. Any
  failure (version block, or expired refresh token) falls through to `LoginForm`, reusing
  its existing blocked-message-with-link UI rather than a bare MessageBox.
- `TrackEvent("checkride_login")` moved out of `VerifyClientAsync` (was firing on every
  app launch with a cached session, not just real logins) to `LoginForm.OnLogin` only.

## Added: "Remember Me" on login (2026-08-10)
New `LoginPrefs.cs` — unencrypted local JSON (`%LOCALAPPDATA%\SimLetsFly\CheckRide\login_prefs.json`,
just an email + bool, not sensitive like `SessionStore`'s tokens) persists the last
successfully-used email when "Remember Me" is checked at login; unchecking it on a
future login forgets the saved address again. `LoginForm` pre-fills the email field and
jumps focus straight to the password field when a remembered email exists.

## Open — bottom status area
User wants to talk through an issue with the bottom status bar/label; not yet described.

## Fixed: version gate ignored patch number (2026-08-10)
`supabase/functions/verify-client/index.ts` `parseVersion()` only parsed major.minor,
dropping the patch segment entirely. Set `app_config.min_client_version = '0.2.7'`
expecting it to block `0.2.6.0` clients — it didn't, because both parsed to `[0,2]` and
compared equal. Fixed `parseVersion` to keep 3 segments and the `allowed` comparison to
check patch when major.minor match. **Needs `supabase functions deploy verify-client`
to take effect** — I don't have the Supabase CLI in this environment, so this fix is
committed but not deployed. Also updated `PUBLISH.md`'s gate instructions to use full
major.minor.patch instead of major.minor-only.

## Key XP12 / King Air 350 findings
- `sim/cockpit2/engine/actuators/throttle_ratio` reads **1.0 for both full forward AND full reverse** on King Air 350 turboprops — ambiguous without `prop_in_beta`
- Added `sim/flightmodel2/engines/prop_in_beta` → `PropInBeta` bool on snapshot — tick log now shows `Rev=True/False`
- `sim/flightmodel2/gear/on_ground` index 0 = **nose gear only** (lifts first during rotation — not a reliable "all wheels off" indicator)
- `CloudBaseAglM` from `sim/weather/region/cloud_base_msl_m` is **MSL altitude**, not AGL — field name in code is misleading; ceiling AGL calculation requires airport elevation
- `StallW=1.00` confirmed fires for King Air 350 stick shaker at ~1.2× stall speed (not aerodynamic stall)
- `acf_Vne` = 400kt anomaly for King Air 350 (real Vmo ~180kt)
- **Confirmed false positive (2026-08-10):** `sim/cockpit/radios/transponder_mode` read `1` (STBY) for the entire duration of live flight `checkride_7e99959e_20260810_210239` from 307.5s through 2405s (past liftoff and well into cruise), triggering the one-shot `SystemTransponder` flag (-3 pts) at liftoff, despite the user having the panel set to ALT the whole time. Live test confirmed the dataref *does* respond to panel changes — switching the King Air 350's transponder to ON moved the reading from `1`→`2` within a second — but it never produces `4` (ALT) at any point, including with ALT selected. So the addon's custom avionics panel supports OFF/STBY/ON on this dataref but caps out at ON=2 and has no path to write ALT=4. Confirmed King-Air-350-specific tool bug, not a pilot procedure miss (same family as the `throttle_ratio`/`prop_in_beta` and `acf_Vno`/`Va` mismatches above). Root cause found via live test: cycled OFF→STBY→ON→ALT and dataref read 0→1→2→**3**, never 4. The King Air 350 addon's transponder panel has only 4 positions (OFF/STBY/ON/ALT — no TEST), so it writes sequential indices 0-3 through the same stock dataref instead of the 5-position stock legend (0=off 1=stby 2=on 3=test 4=ALT). ALT=4 is simply unreachable on this airframe; ALT reports as 3. Fix: `DetectSystemChecks` (FlightMonitor.cs ~line 1039, `snap.TransponderMode != 4`) needs an aircraft-aware check — either accept `3` as ALT-equivalent for King Air 350 specifically, or (more robust) treat "highest observed/selectable mode value" as ALT per-airframe rather than hardcoding 4.

- **King Air 350 addon has its own FMS, doesn't sync stock datarefs** — stock `sim/cockpit2/radios/indicators/fms1_act_eta*` and `fms_distance_to_tod_pilot` read `0` even with a route loaded. The addon exposes its own path instead: `KA350/fms/fmsEntryCount` (route leg count, confirms a plan is loaded), `KA350/instruments/EHSI_Pilot/ehsiDistNM` (distance to next active waypoint), `KA350/instruments/EHSI_Pilot/gpsDmeTime`/`dmeTimeMMgps` (time to next waypoint). No dataref found for total remaining route distance/ETE to final destination — only per-leg values are exposed. Confirmed live via manual poll 2026-08-10, not wired into the app.

## Ad-hoc live API polling (debugging technique, 2026-08-10)
Used to troubleshoot the transponder and FMS issues above by querying XP12's Web API v3
directly with `curl` while a flight was in progress — no code changes needed, works
against any running session:

1. **Resolve name → id:** `GET http://localhost:8086/api/v3/datarefs?filter[name]=<dataref path>`
   returns `{"data":[{"id":<int>,"is_writable":bool,"name":...,"value_type":...}]}`.
2. **Poll value:** `GET http://localhost:8086/api/v3/datarefs/<id>/value` → `{"data":<value>}`.
3. **curl gotcha:** the literal `[` `]` in `filter[name]` trip curl's URL-globbing parser
   and silently produce no request/output. Either pass `-g` (disable globbing) or
   URL-encode as `filter%5Bname%5D=`. Also URL-encode `/` in the dataref path as `%2F`
   (or just leave literal `/` — both worked in testing, but encoding is safer).
4. **String-type datarefs come back base64-encoded**, fixed-width, null/space-padded
   (e.g. active FMS waypoint ident). Decode with `base64 -d` then `xxd`/`strings` to read.
5. **Full dataref catalog:** `GET /api/v3/datarefs` (no filter) dumps everything X-Plane
   exposes (~9,600 entries for this XP12 install) — useful for `grep`-ing for addon-specific
   paths (e.g. everything under `KA350/`) when the stock dataref for something doesn't work.
6. Distance-to-destination was cross-checked this way: pulled live lat/lon via
   `sim/flightmodel/position/latitude`/`longitude`, looked up the destination airport's
   lat/lon in `refdata/airports.json`, and ran Haversine by hand (`awk`, no python3
   available in this shell) — got 51.0nm to KNYL, matching reality. This is the same
   distance-remaining approach recommended for the callout/UI feature idea above.

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

## TODO
- **Fix King Air 350 transponder ALT false positive** — `DetectSystemChecks` (FlightMonitor.cs ~line 1039) hardcodes `snap.TransponderMode != 4` for the ALT check, but this addon's 4-position panel (OFF/STBY/ON/ALT, no TEST) writes ALT as `3` instead of the stock `4`. See root-cause writeup above under "Key XP12 / King Air 350 findings." Needs an aircraft-aware fix before this airframe's transponder check is trustworthy again.
- **Distance/time-to-destination for callouts + UI progress** — idea captured in `CheckRide_Voice_System_Plan.md` under "Route Progress Awareness." Destination is already known (`_expectedArrId` etc., from the SimLetsFly-selected flight plan), so live NM-remaining is just a Haversine against current GPS position — same approach validated manually 2026-08-10 (computed 51.0nm to KNYL live via the Web API + `airports.json`, matched reality). Don't rely on the King Air 350 addon's own FMS distance field for this — confirmed stale/infrequently-updated in testing.
- **Add full switch/system-settings dump for debugging** (not for scoring) — log a complete snapshot of all switch/system dataref states at three points: (1) session start, (2) just before takeoff roll, (3) on shutdown. Goal: make it much faster to troubleshoot other testers' reports (like the transponder-mode confusion above) by having the raw before/after state on hand instead of having to grep tick-by-tick through the whole log to reconstruct what a switch was doing.
- **Consider: is `PenaltyEngineOverspeed` (-20) too harsh / threshold too tight?** Observed 2026-08-10 on live King Air 350 flight `checkride_7e99959e_20260810_210239`: N1 sat at 101.9-102.0% for 11+ sustained seconds at Thr=0.99 cruise power, tripping the one-shot flag at the `N1OverspeedPct(100.0) + N1OverspeedBufferPct(2.0)` = 102% line. Not a re-trigger bug (fires once, condition was genuinely sustained, not a blip) — but two things worth reconsidering: (1) the 100%/+2% threshold is labeled "universal %" in code, never verified against this airframe's actual PT6A-60 POH N1 redline the way V-speeds were; (2) -20 pts sits in the same severity tier as Engine Out (-25) and Engine Fire (-30), which seems disproportionate for a few-seconds, few-percent overtorque vs. an actual failure. Not changed yet — just flagging for review.
