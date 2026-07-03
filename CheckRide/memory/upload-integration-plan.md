# CheckRide — Upload & Web Integration Plan

## Flow
1. User opens CheckRide tray app → if no stored token, shows login
2. Login → fetches user's planned flights from SimLetsFly API
3. Flight picker UI — shows list: origin → destination, date, flight name
4. User picks a flight → clicks **Start Flight**
5. App arms monitoring, stores the selected flight UUID
6. User loads X-Plane and flies
7. Session ends → app auto-uploads JSON to `POST /api/flights/{uuid}/checkride`
8. Tray notification: "Flight uploaded — view report"

## Opt-in debug log upload
- Checkbox on flight picker: "Include debug log (helps improve detection)"
- When checked, uploads the `.log` file alongside the JSON
- Off by default — log is verbose (~3,000–5,000 lines per flight) and contains detailed flight data

## Data model
- Flight records are in the SimLetsFly DB with: origin airport, destination airport, unique UUID
- CheckRide JSON links to the flight via UUID
- UUID embedded in the report JSON at upload time

## What needs to be built

### Desktop app (CheckRide)
- Login form (WinForms) — auth mechanism TBD (JWT / session token)
- Stored token (persist login across sessions)
- Flight picker form — fetches and lists user's planned flights
- "Start Flight" button — arms monitoring, stores UUID
- Auto-upload at session end with UUID + optional log file

### Backend / API (SimLetsFly site)
- Auth endpoint (or confirm existing one is usable)
- `GET /api/flights` — returns user's planned flights (UUID, origin, destination, date, name)
- `POST /api/flights/{uuid}/checkride` — accepts JSON report body (+ optional log file)

### Web site
- CheckRide tab/section on flight detail page
- Display scored report: grade, score, event list, summary flags, track

## Auth — Supabase
- Supabase handles auth — email/password login via Supabase Auth REST API
- `POST https://<project>.supabase.co/auth/v1/token?grant_type=password` → returns `access_token` + `refresh_token`
- Desktop app stores refresh token persistently, uses access token for all API calls (`Authorization: Bearer <token>`)
- Refresh token when expired using `grant_type=refresh_token`
- Supabase JWT can be verified server-side via RLS or a backend function

## Open questions (resolve next session)
- Site stack: ASP.NET, Node, other?
- Does a flight list API endpoint already exist or need to be built?
- Supabase project URL (needed to wire up the auth calls in the desktop app)
