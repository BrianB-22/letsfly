# SimCareer — Career Mode Flight Sim Game

> Separate site from simletsfly.com. Users surveyed want simletsfly to stay clean and anonymous.
> This is a companion product — shares data assets (airports.json, stack) but is its own thing.

---

## Concept

A casual, narrative-flavored career progression game for MSFS/X-Plane pilots. Pick a career path,
accept missions, log flights on the honor system, earn money, grow your fleet. More laid-back than
FSEconomy/OnAir — no punishing death spirals, no mandatory add-ons, no ten-page tutorials to start.

**Core vibe:** _You're a pilot, not an accountant. The game should feel rewarding after a 45-minute
session, not after 40 hours of grinding._

---

## Why a Separate Site

- simletsfly users want it to stay fast and anonymous — login friction is already a pain point
- Career mode needs persistent state (money, fleet, missions, history) — fundamentally different session model
- Separate brand/URL keeps the tools distinct and lets each grow independently
- Possible future: simletsfly "Generate Flight" button that optionally creates a career mission

---

## Career Types

Each type has its own mission pool, flavor text, and aircraft recommendations.
A user picks one at signup but could eventually unlock others.

| Type | Description | Typical Aircraft |
|---|---|---|
| **Passenger Charter** | Move people point-to-point, build client reputation | C172, TBM, Citation, ERJ, A320 |
| **Cargo / Freight** | Haul packages and freight, time bonuses available | Caravan, DC-3, ATR-72, Twin Otter |
| **Ferry Pilot** | Deliver aircraft for owners/dealers, one-way legs | Anything on the master list |
| **Emergency Services** | Medevac, SAR, firefighting drops, coast guard | PC-12, King Air, Kodiak |
| **Bush / Outback** | Remote strips, unpaved runways, challenging terrain | Beaver, Kodiak, C208, Twin Otter, PC-6 |
| **Scenic / Tourism** | Sightseeing flights, less pressure, lower pay | C172, C182, TBM, SR22 |

No helicopters at launch.

---

## Core Game Loop

1. **Pick a mission** from your available board (3–6 at a time, refreshes daily or on completion)
2. **Review the brief** — route, aircraft requirement, pay, bonus conditions, time window
3. **Fly it** in your sim (honor system — you launch your sim, fly the flight)
4. **Log the flight** — mark complete, optionally add notes/screenshot
5. **Earn money + reputation** — unlocks better missions, bigger aircraft, new career locations
6. **Repeat** — special missions appear as rep grows; career events keep things fresh

---

## Mission Structure

### Location Tracking
- Current location = last logged arrival airport
- Players set a home base airport on initial profile setup
- If no missions available at current airport, a "Nearby Jobs" view shows airports within 50nm
  that have missions — with the actual missions listed so players can decide if the trip is worth it
- Repositioning to a nearby airport is free (no fuel cost at launch) — flying there is the cost
- Fuel/expense modeling is deferred to a future update

### Mission Board
- 6 mission slots per player at all times
- Missions are scoped to the player's current aircraft range and career type by default
- Board refreshes at UTC midnight — expired missions drop, new ones fill back to 6

### Mission Expiry (unaccepted)
- Each mission has a 3-night lifespan from when it was generated
- At midnight, oldest missions expire first (age-based FIFO)
- Flavor option (future): an about-to-expire mission occasionally gets an "urgency bump" —
  bonus payout added, one extra day granted

### Accepted Missions
- Once accepted, the mission is locked to the player and removed from the general board
- Player has **7 days** to log the flight before the mission expires
- Can hold multiple accepted missions simultaneously (up to board limit)

### Standard Missions
- Generated from the airports.json pool (same 38k airports simletsfly uses)
- Distance-filtered to match current aircraft range
- Pay scales with distance, terrain difficulty, runway length
- Example: _"Medevac pickup at PAKD Adak Island. Patient needs transfer to PAAK Kodiak. Time-sensitive: +20% if wheels down within 2 hours real time."_

### Special Missions
- Curated or algorithmically special — rare airports, challenging weather region, milestone routes
- Appear at reputation thresholds or calendar events
- Examples:
  - _"VIP charter: fly a tech exec from KSQL to KMRY. No diversions."_
  - _"Relief cargo: three legs into remote Haiti strips after storm damage."_
  - _"Ferry a brand-new TBM 960 from Tarbes, France (LFBT) to its new owner in Calgary (CYYC)."_

### Mission Generation — Technical Approach

Missions are generated **per player**, not per airport. No need to pre-populate all 38k airports —
only generate for players who have fewer than 6 active missions on their board.

**Production:** Supabase pg_cron (preferred) — schedule a SQL function inside Supabase itself
at UTC midnight. No external compute needed. GitHub Actions (free scheduled workflow) is the
fallback if pg_cron proves limiting.

**Development:** local script to seed test data on demand.

**Generation logic:**
1. Find all players with fewer than 6 active missions
2. Read their current location, aircraft range, and career type
3. Pick random airports from airports.json within range, matching career type
4. Insert new mission rows with 3-day expiry timestamp

### Career Events (future)
- Time-limited events for all users — "Wildfire Season" adds firefighting missions in the PNW
- Leaderboard by most missions completed or most earnings during event window

---

## Progression

### Money
- Start with a small bankroll and a cheap aircraft chosen from the career's starter tier
- Missions pay based on distance, difficulty, aircraft size, and bonus conditions
- No bankruptcy mechanic — if you're low, easier missions stay available
- Upgrades: buy better aircraft from the curated master list — purchase price only, no maintenance
- Pacing target: casual player flying 3–4 missions a week reaches first upgrade in 2–3 weeks

### Reputation & Ranks
Named ranks per career type — five tiers. No raw number shown to the player.
Display: rank name + progress bar + hint of missions needed to reach next rank.

```
Regional Freight        ████████░░  80%
                        → 2 missions to Freight Captain
```

**Rank ladders:**
- Cargo: Local Hauler → Regional Freight → National Carrier → International Cargo → Freight Captain
- Passenger: Student Charter → Private Charter → Executive Charter → Regional Airline → Airline Captain
- Ferry: Delivery Driver → Route Runner → Cross-Country Pilot → Transcontinental → Master Ferry
- Emergency: First Responder → Relief Pilot → Crisis Veteran → Senior Medic Pilot → Rescue Ace
- Bush: Weekend Flyer → Bush Hopper → Outback Pilot → Wilderness Veteran → Legend
- Scenic: Sightseeing Guide → Tour Pilot → Scenic Captain → Prestige Tour → Elite Guide

**Each rank unlocks:**
| Rank | Unlocks |
|---|---|
| 1 (start) | Starter aircraft tier, basic short-range missions |
| 2 | Mid-tier aircraft, slightly longer range missions |
| 3 | High-performance singles/twins, regional missions |
| 4 | Regional jets / large turboprops, international missions |
| 5 (top) | Widebodies, special missions, career events |

**Reputation goes up:**
- Mission completed — base gain, scales with distance and difficulty
- Smooth landing — small bonus
- On-time bonus condition met — small bonus
- Weekly streak — small multiplier

**Reputation goes down:**
- Crash — significant hit (≈ cost of 2 completed missions worth of rep)
- Abandoned accepted mission — moderate hit
- Accepted mission expired (7 days, not flown) — moderate hit
- Very hard landing (>600 fpm) — small hit

One crash sets you back but doesn't undo a week of work — the math stays forgiving.

### Fleet
- Start: one aircraft chosen from the career's starter tier
- Earn enough money → buy an upgrade from the next tier
- No maintenance costs — purchase price only, keep it casual

---

## Honor System (Launch)

Users self-report flights. No verification. Works on trust + community norms.

**Log entry captures:**
- Mission ID (pre-filled from the mission they accepted)
- Departure / arrival ICAO (pre-filled)
- Actual departure / arrival (user confirms or corrects)
- Flight time (user enters)
- Notes / screenshot (optional)
- Self-reported bonuses (on time, smooth landing, etc.)

**Why this works at launch:**
- Audience is flight sim enthusiasts who _want_ a realistic career experience — cheating defeats the purpose
- No real-money stakes, so motivation to cheat is low
- Can add soft trust signals later (streak, community profile, etc.)

---

## Sim Tracker App

A C# Windows system tray app that connects to the sim and auto-captures flight data.
Windows first (covers ~80% of MSFS users). X-Plane UDP support can be added later.

**MSFS integration:** SimConnect SDK (native C# support — first-party Microsoft API)
**X-Plane future:** reads UDP broadcast on localhost

### What it captures
- Departure airport — identified at takeoff roll
- Arrival airport — identified at touchdown
- Flight time — wheels up → parking brake set
- Landing vertical speed — `VERTICAL_SPEED` at the moment `SIM_ON_GROUND` goes true
- Landing quality classification: Smooth (<100 fpm) · Normal (100–300) · Hard (300–600) · Very hard (>600)
- Crash — via SimConnect `Crashed` system event subscription
- Gear-up landing — `GEAR_HANDLE_POSITION` at touchdown
- Overspeed warnings — `OVERSPEED_WARNING` variable

### Flight ended trigger
Parking brake set (`BRAKE_PARKING_INDICATOR` = true) + on ground + ground speed < 1 knot.
Prevents false endings during taxi.

### Damage detection
MSFS damage model variables are inconsistent depending on user settings — treat as
informational only, do not penalize based on damage state.

### Flow
1. User launches app before sim session (runs in system tray)
2. User authenticates once with career site login — token stored locally
3. App detects takeoff and landing automatically
4. On flight end: submits dep/arr/aircraft/time/landing data to career API
5. Player confirms mission completion on the career site

### Landing data → progression
Landing quality feeds directly into reputation and mission bonuses:
- Passenger charter clients care most about smooth landings
- Cargo and bush careers care less — weight it accordingly
- Crash = mission failed, no pay

---

## Aircraft Master List

Players pick from this list — must match what they have in their sim. Each entry has cruise speed,
range, and capacity so mission generation can scope routes correctly. Flagged by sim availability.

> **Sim tags:** M = MSFS stock · X = X-Plane stock · A = popular add-on (free or paid)

| Aircraft | Cruise (kts) | Range (nm) | Seats/Cargo | Sim | Notes |
|---|---|---|---|---|---|
| Cessna 152 | 90 | 350 | 2 / — | M, X | Trainer, scenic only |
| Cessna 172 Skyhawk | 110 | 500 | 4 / — | M, X | Most common starter |
| Cessna 182 Skylane | 145 | 800 | 4 / — | M | Step up from 172 |
| Cirrus SR22 | 185 | 1,000 | 4 / — | X | Fast GA |
| Piper PA-28 Cherokee | 110 | 500 | 4 / — | M | GA workhorse |
| Beechcraft Bonanza G36 | 165 | 900 | 6 / — | M | Classic GA |
| Beechcraft Baron 58 | 180 | 1,000 | 6 / — | X | Twin GA |
| Daher TBM 930/960 | 330 | 1,700 | 6 / — | M | Fast turboprop, popular |
| Pilatus PC-12 | 270 | 1,800 | 9 / cargo | M, A | Emergency + cargo workhorse |
| Kodiak 100 | 170 | 900 | 10 / cargo | A | Bush + cargo |
| DHC-2 Beaver | 100 | 450 | 7 / cargo | A | Classic bush |
| PC-6 Porter | 120 | 600 | 10 / cargo | A | Bush + STOL |
| Cessna 208B Caravan | 175 | 1,000 | 14 / cargo | M | Cargo + bush staple |
| DHC-6 Twin Otter | 160 | 800 | 19 / cargo | A | Bush + cargo |
| Beechcraft King Air C90 | 220 | 1,100 | 7 / — | X | Regional turboprop |
| Beechcraft King Air 350 | 290 | 1,800 | 11 / cargo | M | Regional + emergency |
| Douglas DC-3 | 150 | 1,200 | 28 / cargo | A | Classic cargo, flavor |
| Aerosoft CRJ 700 | 430 | 1,700 | 70 / — | M, A | Regional jet |
| Embraer ERJ-145 | 420 | 1,500 | 50 / — | A | Regional jet |
| ATR 72 | 270 | 1,000 | 70 / cargo | A | Regional turboprop |
| McDonnell Douglas MD-82 | 460 | 2,000 | 155 / — | X | Narrowbody |
| Airbus A320neo | 450 | 3,400 | 180 / — | M, A | Narrowbody (FBW free add-on) |
| Boeing 737-800 | 450 | 3,000 | 175 / — | M, A | Narrowbody (ZIBO free on X-Plane) |
| Boeing 757-200 | 460 | 3,900 | 200 / cargo | A | Mid-range + cargo |
| Boeing 767-300 | 450 | 5,500 | 260 / — | A | Long-range |
| Boeing 787-10 | 485 | 6,000 | 330 / — | M | Long-haul |
| Boeing 747-8 | 490 | 8,000 | 410 / cargo | M, X | Heavy, high-rep only |
| Airbus A380 | 488 | 8,200 | 555 / — | A | Heavy, high-rep only |

**Curated defaults by career type:**
- **Passenger Charter** — C172 → TBM → PC-12 → King Air 350 → CRJ → A320 → B787
- **Cargo** — C208 → Kodiak → Twin Otter → King Air 350 → DC-3 → ATR-72 → B757 → B747
- **Ferry** — full list (you fly whatever you're ferrying)
- **Emergency Services** — PC-12 → Kodiak → King Air 350 → C208
- **Bush / Outback** — Beaver → C208 → PC-6 → Kodiak → Twin Otter
- **Scenic / Tourism** — C152 → C172 → C182 → SR22 → TBM

Players can override and pick any plane from the master list — career type gates the default view,
not the actual selection.

---

## Tech Stack

Reuse as much from simletsfly as possible:

| Layer | Choice | Notes |
|---|---|---|
| Frontend | Pure HTML/CSS/JS | Same pattern — no build step, fast, cacheable |
| Auth | Supabase Auth | Same Supabase project or a new one |
| Database | Supabase Postgres | New tables (see below) |
| Airports | airports.json | Same file — same 38k airport pool |
| Hosting | Same host as simletsfly | Different subdomain or new domain |
| PWA | Service worker | Same pattern as simletsfly |

---

## Data Model (rough)

```
profiles
  user_id, career_type, display_name, money, reputation, current_aircraft_id, created_at

aircraft
  id, user_id, make_model, cruise_speed_kts, range_nm, purchased_at, active

missions
  id, user_id, type, dep_icao, arr_icao, distance_nm, payload, pay, bonus_pay,
  bonus_condition, status (available/accepted/completed/expired), expires_at, created_at

completed_flights
  id, user_id, mission_id, dep_icao, arr_icao, flight_time_min, notes, screenshot_url,
  bonus_claimed, earnings, logged_at

career_events (future)
  id, name, description, start_at, end_at, mission_template, leaderboard
```

---

## Differentiation from Competitors

| | FSEconomy | OnAir | Air Hauler 2 | **SimCareer** |
|---|---|---|---|---|
| Setup friction | High | Very high | Medium | Low |
| Sim add-on required | No | Yes | Yes | No (optional) |
| Learning curve | Steep | Steep | Moderate | Gentle |
| Economic punishing | Yes | Yes | Yes | No |
| Career types | Limited | Yes | Limited | Yes |
| Web-based | Yes | Yes | No | Yes |
| Mobile-friendly | No | No | No | Yes |
| Cost | Free | Paid | Paid | Free (initially) |

---

## Open Questions

- **Domain:** New standalone site — name/domain TBD.
- **Multi-career:** One career per user — pick at signup, commit to a path. Can "retire and restart" as a different type later. Keeps identity strong and progression meaningful.
- **Social:** Yes — public leaderboards (earnings, missions completed, reputation).
- **Aircraft list:** Curated list of 20–30 common planes with fixed specs (no honor system on specs).
- **Mission expiry:** Yes — missions expire; a fresh batch is generated each night.
- **Monetization:** Not at launch — revisit later (sim integration app is a natural paid tier).

---

## Planning — Still To Talk Through

1. **Onboarding flow** — what signup looks like, how they pick career, set home base, choose starting aircraft
2. **Site pages** — what screens exist (dashboard, mission board, profile, leaderboard, logbook)
3. **Leaderboards** — global vs. per career type, all-time vs. weekly, what stats are shown
4. **Public profile** — what other players can see on your career page
5. **Logbook** — flight history, public or private, what's recorded per entry
6. **Milestones / achievements** — first flight, 100 hours, first international, etc.
7. **Mission pay numbers** — actual dollar amounts, starting bankroll, aircraft prices
8. **Special missions** — how they're triggered, how they're written/curated
9. **Name / domain** — still TBD
10. **Launch strategy** — open signup, invite-only beta, relationship to simletsfly

---

## Possible Names

- SimCareer
- FlightCareer
- VirtualPilot
- Hangar (too generic)
- career.simletsfly.com (leverages existing brand)
