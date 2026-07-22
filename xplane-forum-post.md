# X-Plane.org Forum Post — SimLetsFly + CheckRide

> Suggested forum section: **General Discussion** or **Freeware** (X-Plane.org doesn't have a
> plugin install for CheckRide — it talks to XP12's built-in Web API — so General Discussion /
> Freeware Addons is the right fit, not the Plugins section).

---

**Title:** Free flight planner + a "CheckRide" app that grades how you actually fly in XP12 (no plugin — uses the Web API)

---

**Body:**

Hey everyone — long-time lurker, first-time poster with something to share. This has been a side project of mine for a while and it's reached the point where I'd genuinely like feedback from people who fly more seriously than I do.

**SimLetsFly** (free, no ads, no account required to use it) is a flight discovery/planning tool:

- Generates a random flight from a departure airport based on your own filters — range, runway length/surface, fuel availability, nav aids, airport size
- Live METAR/TAF for both ends, full flight brief with suggested runway and a plain-English weather decode
- Direct export to X-Plane 11/12, MSFS 2020/2024, and SimBrief
- Challenge Mode — deliberately finds short/high/unpaved/no-ILS airports if you want something harder
- Logbook with a visited-airports world map if you sign up (free) to save flights

**CheckRide** is the part I think this crowd will find more interesting. It's a free Windows companion app built specifically around X-Plane 12's Web API (`localhost:8086`) — no plugin to install, nothing dropped into your `Resources/plugins` folder. Enable the Web API in XP12's network settings, run CheckRide alongside the sim, and it grades your flight like a CFI/DPE would:

- **Landing quality** — touchdown VS, crosswind component, side-load; greasers earn bonus points, hard landings cost you
- **Speed discipline** — Vno/Vfe overspeeds, the 250kt/10,000ft restriction, excessive approach speed, all logged with a timestamp
- **Airmanship** — G-forces, bank angle, descent rates, unstable approaches
- **System checks tied to phase of flight** — gear, flaps, landing lights, strobes, transponder, pitot heat — checked against what phase you're actually in, not a static checklist (so it won't dock you for landing lights during a low approach, only during actual approach/landing)
- **Engine health** — N1/N2/ITT monitored continuously, failures (fire, oil/fuel pressure, overspeed, icing) all scored
- **Weather-aware bonuses** — crosswind landings, landing in heavy rain, low-visibility landings all earn extra credit instead of just being ignored
- Full flight report — interactive track map, altitude/speed chart with event markers, event log, and an AI-written debrief that references your actual flight, not generic feedback

It's still early — I'm treating it as an open beta and I know the scoring won't be perfect for every aircraft type yet (jets doing a flex/derated-thrust takeoff are one thing I'm actively working around, since a legitimately reduced-thrust takeoff can look like a "weak" takeoff to the current N1 check). That's exactly why I want more real pilots flying more real aircraft against it — the more edge cases I see, the better the scoring gets.

A couple of practical notes since this is a fresh, unsigned .exe:
- It's a single-file download, no installer, no admin rights needed
- Windows SmartScreen will warn you the first run since it isn't code-signed yet — "More info → Run anyway." I publish a SHA-256 hash with every release so you can verify the download yourself before running it
- New builds are going out frequently right now while I chase down scoring bugs — if it's been a few days, grab the latest before you fly

Both are completely free, no ads, no paid tier planned. This is genuinely just a gift to the community — I built it because I wanted it to exist.

**https://simletsfly.com/** — flight planner
**https://simletsfly.com/checkride.html** — CheckRide (Windows, requires a free SimLetsFly account)

Would love to hear what you think — especially if you fly something exotic (helicopters, gliders, warbirds) where I'm sure the scoring assumptions will need work. Happy to answer questions.
