-- Free-text note on a single checkride_results attempt, distinct from
-- saved_flights.pilot_notes (which is per-flight-plan and shared across every
-- attempt flown on that route). Used for the diversion feature: when a pilot
-- lands away from the planned destination after declaring a diversion, the
-- client records a plain-language note plus the raw landing GPS coords here,
-- instead of touching the shared flight plan's arrival airport.
ALTER TABLE checkride_results ADD COLUMN notes TEXT;

-- Column-level grant required because H-1 fix revoked SELECT on the whole table
GRANT SELECT (notes) ON checkride_results TO anon, authenticated;
