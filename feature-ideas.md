# Feature Ideas — from July 5, 2026 code review

New ideas surfaced while reviewing the codebase. Deliberately excludes what's already
tracked in todo.md (My Stats, CSV export, fuel tracking, aircraft profiles, SimBrief),
nextgen-features.md (challenges/leaderboard, achievements, tours, weather filtering),
and career-mode.md.

## CheckRide-data features (data already uploaded, zero client changes)

1. **Run comparison view** — pick two runs of the same flight and see a side-by-side
   diff: score deltas per breakdown line, both tracks overlaid on one map, landing
   stats compared. The "did I actually improve?" answer. *Effort: medium.*

2. **Score progression chart** — a sparkline per flight card (already have the trend
   arrow — this is its big brother) and a global chart on the future My Stats page.
   Score vs. date, colored by scoring version so cross-version jumps are explained.
   *Effort: low–medium.*

3. **Personal records panel** — best landing VS, longest flight, highest score,
   biggest crosswind landed, total CheckRide distance/hours. One aggregation query.
   Natural first section of My Stats. *Effort: low.*

4. **Landing analysis page** — every landing across all runs plotted: VS vs.
   crosswind, quality distribution (Greaser/Smooth/Normal/Firm/Hard pie), trend over
   time. The single stat pilots care most about. *Effort: medium.*

5. **Text debrief on the report** — generate a short narrative from events + summary:
   "Night departure from KEQY. Clean climb. One overspeed in cruise. Stable approach,
   smooth touchdown at 92 fpm in an 8 kt crosswind." Pure template logic over
   existing data; makes reports feel human. *Effort: low–medium.*

## Client-assisted features (need C# changes)

6. **ILS approach scoring** — glideslope/localizer deviation is already polled every
   second but never used. Score "kept within ½ dot below 1,000 ft" as a bonus; upload
   the deviation trace for a mini approach chart on the report. *Effort: medium.*

7. **Pattern mode** — detect closed-traffic flights (multiple touch-and-goes at the
   same field) and score each circuit: consistent pattern altitude, landing count,
   per-landing quality list. Currently pattern flights miss approach checks entirely.
   *Effort: medium–high.*

8. **Weather difficulty context** — the report already knows wind, gusts, visibility,
   cloud, night. Show a "conditions difficulty" chip (Calm / Moderate / Challenging)
   next to the score so a 78 in gusting crosswind IMC reads differently than a 78 on
   a calm day. Could later feed a score multiplier. *Effort: low (display only).*

## Sharing / growth

9. **Score card image export** — render a compact PNG card (grade, score, route,
   landing quality, track thumbnail) via canvas for posting on Discord/forums where
   a link preview isn't enough. Save HTML already proves the canvas pipeline.
   *Effort: medium.*

10. **"Fly it again" quick action** — one click on a flight card duplicates the
    flight as a new pending entry (or just re-opens CheckRide deep-link) to
    encourage re-flying for a better grade. Pairs with the run comparison view.
    *Effort: low.*

## Operational

11. **Client version check** — a `client_versions` row in Supabase checked at app
    launch; status bar shows "New version available" with a download link. Cheap
    insurance now that scoring versions matter. *Effort: low.*

12. **Scoring changelog page** — a public page listing what changed in each scoring
    version (xp12-1.3 → 1.4: pause handling, landing-lights gate…). Builds trust
    when scores shift between runs. *Effort: low.*
