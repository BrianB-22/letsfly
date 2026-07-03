# SQL Updates Needed — checkride_results

Run this on the SimLetsFly Supabase project to support the CheckRide client upload.

## ALTER TABLE

```sql
ALTER TABLE checkride_results
  ADD COLUMN distance_nm       numeric,          -- from stats.DistanceNm
  ADD COLUMN flight_time_sec   integer,          -- from stats.FlightTimeSec
  ADD COLUMN landing_quality   text,             -- from summary.LandingQuality (Greaser/Smooth/Firm/Hard)
  ADD COLUMN crashed           boolean DEFAULT false,
  ADD COLUMN imc_flight        boolean DEFAULT false,
  ADD COLUMN log_text          text;             -- raw tick log content, populated only when user opts in
```

## Supabase Storage bucket (NOT needed)
Log text is stored directly in log_text column — no Storage bucket required.

## Existing columns (already present)
id, flight_id, user_id, score, grade, aircraft, sim, scoring_version, recorded_at, events (jsonb), summary (jsonb), stats (jsonb)

## RLS notes
- The insert from the desktop client uses the user's JWT (Bearer token)
- RLS policy must allow INSERT where user_id = auth.uid()
- The existing SELECT policy (for the web flights page) already works

## What each flat column enables
- distance_nm / flight_time_sec → sort/filter by flight stats without JSONB queries
- landing_quality → filter "show me all Greaser landings"
- crashed → filter out crash sessions from stats
- imc_flight → filter IMC sessions
- log_text → raw debug data for support/detection tuning; only populated when user checks "Upload debug log"
