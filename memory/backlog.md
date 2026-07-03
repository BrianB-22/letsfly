# Backlog / Future Considerations

## Idle recording on the ground
Users may accidentally leave recording running for hours while parked, generating large track logs and wasting storage. Consider:
- Auto-warning if recording has been active > 30 min with no phase change from Idle/Taxiing
- Auto-stop recording if no airborne activity detected after X minutes
- Server-side: reject or truncate uploads where flight time is near-zero but track is very long

## Stall warning dataref
`sim/flightmodel/misc/stall_warning` returns 404 in XP12. Need to find the correct dataref. Without it, stall detection is missing.

## Tire sink depth threshold
Currently 0.05m triggers OffRunway. Need a real test flight on pavement vs grass to confirm the right value.

## MSFS 2020/2024 support
~85% of XP12 datarefs have SimConnect equivalents. Missing: tire sink/skid, lateral/axial G (derivable), crash detection edge cases. Would need a full new connector against SimConnect. ScoringVersion prefix would be `msfs2020-x.x`.

## WebSocket API (XP12)
XP12 exposes a WebSocket at `ws://localhost:8086/api/v3` with 10Hz push. Would be more efficient than REST polling once Phase 1 is stable.
