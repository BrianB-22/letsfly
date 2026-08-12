# CheckRide for SimLetsFly

## Voice System Design & Roadmap

Version: Draft 1

------------------------------------------------------------------------

# Vision

The voice system should make CheckRide feel like you're flying with the
same instructor every flight.

The instructor is:

-   Friendly
-   Professional
-   Encouraging
-   Occasionally witty
-   Never mean-spirited

The goal is to make the app memorable while still reinforcing good
flying habits.

------------------------------------------------------------------------

# Voice Style

**Tone** - Friendly but authoritative - Calm - Confident - Experienced
CFI - Conversational

Think:

> "I'm rooting for you, but I'm also going to call out your mistakes."

------------------------------------------------------------------------

# V1 Event Flow

Engine Start ↓ Ready Message ↓ Flight Evaluation ↓ Landing Callout
(short) ↓ Taxi ↓ Parking Brake Set ↓ Full Debrief

------------------------------------------------------------------------

# Initial WAV Scripts

## ready.wav

> Welcome to CheckRide for SimLetsFly. Your flight is now being
> evaluated. To complete your checkride, land at your planned
> destination airport and set the parking brake after coming to a
> complete stop. Good luck, and have a great flight.

------------------------------------------------------------------------

## engine_start.wav

> Engine start confirmed. Your checkride is underway. Fly the aircraft,
> follow your procedures, and enjoy your flight.

------------------------------------------------------------------------

# Landing Callouts

These should be short, immediate, and focused only on the landing.

## landing_excellent.wav

Examples

-   Outstanding landing.
-   Beautiful touchdown.
-   Excellent landing.
-   Butter.
-   That's how it's done.

------------------------------------------------------------------------

## landing_good.wav

Examples

-   Nice landing.
-   Well done.
-   Good landing.
-   Smooth arrival.
-   Nicely flown.

------------------------------------------------------------------------

## landing_poor.wav

Examples

-   That landing could use a little more finesse.
-   I've definitely seen smoother landings.
-   Let's call that... an arrival.
-   The runway survived. That's the important part.
-   A little less excitement next time.
-   The passengers are applauding because it's over.

Funny variants

-   Maybe boating is more your style.
-   You ever think about taking up a hobby that stays on the ground?
-   I can't wait to get out of this airplane.
-   Gravity definitely won that round.
-   That landing had a lot of enthusiasm.
-   The runway had absolutely nothing to do with that.
-   I'm pretty sure the tires just filed a complaint.
-   I've seen shopping carts parked more smoothly.
-   Well... it stopped eventually.

------------------------------------------------------------------------

## landing_crash.wav

Examples

-   Crash detected.
-   The aircraft has crashed.
-   Well... that escalated quickly.
-   Good news. We're no longer flying.
-   Let's get a fresh airplane and try again.

------------------------------------------------------------------------

# Flight Complete (Parking Brake)

These are longer comments played after the parking brake is set.

## parking_brake_excellent.wav

> Excellent work. You demonstrated solid airmanship and sound decision
> making throughout the flight. Congratulations on a successful
> checkride.

------------------------------------------------------------------------

## parking_brake_good.wav

> Nice job. You completed the checkride successfully. Review your
> debrief for a few recommendations that can make your next flight even
> better.

------------------------------------------------------------------------

## parking_brake_poor.wav

> Your flight has been evaluated. Today's performance did not meet
> checkride standards. Review your debrief, practice the highlighted
> areas, and you'll be ready for another attempt. Every flight is an
> opportunity to improve.

------------------------------------------------------------------------

## parking_brake_crash.wav

> This checkride has ended due to an accident. Review the debrief to
> understand what happened, make the necessary corrections, and then
> give it another try.

------------------------------------------------------------------------

# Future Roadmap

## Voice Packs

-   Friendly Instructor (Default)
-   Professional Examiner
-   Snarky CFI
-   Military Instructor
-   British Examiner
-   Community voice packs

------------------------------------------------------------------------

## Random Voice Pools

Instead of a single WAV per event:

landing_good/ - landing_good_01.wav - landing_good_02.wav -
landing_good_03.wav

Randomly choose one.

This keeps the instructor from becoming repetitive.

------------------------------------------------------------------------

## Context-Aware Commentary

Future versions should know WHY points were lost.

Examples

-   Hard landing
-   Bounce
-   Float
-   Long landing
-   Short landing
-   Fast taxi
-   Failure to clear runway
-   Drift off centerline
-   Excessive bank
-   Poor flare
-   Crosswind technique
-   Pattern spacing
-   Checklist compliance

Example:

> Easy there, the landing gear has feelings too.

or

> You paid for the whole runway, but you don't have to use all of it.

------------------------------------------------------------------------

## Long-Term Flight Memory

The instructor should remember previous flights.

Examples

> Much better than your last few landings.

> Your flare has really improved.

> Crosswind landings are becoming one of your strengths.

> Let's keep working on maintaining the centerline.

This should make the instructor feel like a real CFI who knows the
student.

------------------------------------------------------------------------

## Achievement Commentary

10 Flights

> Ten checkrides complete. Nice work.

50 Flights

> Fifty flights already? You're putting in the hours.

100 Flights

> One hundred flights. You're becoming a regular around here.

Perfect Flight

> Now that's a checkride to be proud of.

------------------------------------------------------------------------

## Airport Awareness

Examples

> Welcome to Oshkosh.

> Busy airport today.

> Nice work getting into a shorter runway.

> Mountain flying always adds a challenge.

------------------------------------------------------------------------

## Weather Awareness

> Those crosswinds kept things interesting.

> Nice work flying the approach in IMC.

> Strong headwind today.

------------------------------------------------------------------------

## Route Progress Awareness (idea, 2026-08-10)

Destination airport is already known — it's the `arr_id` from the SimLetsFly-selected
flight plan that seeds `_expectedArrId`/`_expectedArrLat`/`_expectedArrLon` in
`FlightMonitor`. Distance-to-destination is a straight Haversine from live GPS position
(`sim/flightmodel/position/latitude/longitude`) against that lat/lon — same math already
used for the wrong-arrival-airport check, just computed continuously instead of once at
landing. No dependency on the addon's own FMS (which turned out to be unreliable for
this — see the King Air 350 FMS findings above: stock ETA/distance-to-TOD read 0, and the
addon's own per-leg distance field goes stale between updates).

Possible uses:
- Periodic callout, e.g. "50 miles from Yuma, time to start thinking about the approach."
- UI: a live "NM to destination" / progress-bar readout in the flight-in-progress view,
  next to the existing log path / flight-in-progress status line.

Examples

> Forty miles out. Might be a good time to start your descent planning.

> Closing in on Yuma.

------------------------------------------------------------------------

## Taxi Commentary

Taxi too fast

> Easy there, we're taxiing---not qualifying for pole position.

Stopped on runway

> Let's clear the runway before we celebrate.

Forgot parking brake

> I'm still waiting on that parking brake.

Forgot lights

> Looks like a checklist item got away from us.

------------------------------------------------------------------------

## Rare Easter Eggs

These should be extremely uncommon.

Perfect landing

-   I'm framing that landing.
-   I barely felt that one.
-   Someone's been practicing.
-   I checked the vertical speed twice just to be sure.
-   That landing was so smooth I thought we were still flying.

------------------------------------------------------------------------

## Closing Lines

Randomly play one after the debrief.

-   See you on the next checkride.
-   Keep the blue side up.
-   Fly safe.
-   Until next flight.
-   Thanks for flying with CheckRide.
-   See you back in the pattern.
-   Another flight in the logbook.

------------------------------------------------------------------------

# Guiding Principles

1.  Teach first.
2.  Entertain second.
3.  Keep comments short.
4.  Don't repeat the same phrases.
5.  Reward improvement.
6.  Make the instructor feel like a real person.
7.  Give the app a personality that users remember.
