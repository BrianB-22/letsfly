# Memory Index

## Project
- [CheckRide Phase 1 status](checkride-phase1-status.md) — prototype built and testing; what's done, key bugs fixed, ScoringVersion=1.2
- [XP12 API lessons](checkride-xp12-api.md) — unit gotchas, Int64 IDs, array datarefs, has_crashed limitation, per-dataref unit list
- [XP12 Web API spec](xp12-web-api-spec.md) — endpoints, confirmed working datarefs, known 404s with likely fixes, unit conversions, empirical quirks
- [Client login pulls user config](client-login-config.md) — on login: validate app version AND pull scoring preferences (e.g. disable system-check deductions)

## Backlog
- [Backlog / future considerations](backlog.md) — idle recording waste, stall dataref, tire threshold, MSFS, WebSocket

## Feedback
- [Scoring should reflect reality](feedback-scoring-philosophy.md) — bad flights must score badly; crash/excursion = automatic F; don't let bonuses inflate broken sessions
- [Wide net on datarefs](feedback-wide-net-datarefs.md) — add all potentially useful datarefs upfront, log raw values, prune after seeing real data
