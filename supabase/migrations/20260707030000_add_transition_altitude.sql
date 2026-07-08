-- Records the transition altitude (ft) selected in the CheckRide client for
-- each run, so the altimeter-setting scoring check can use a country-specific
-- value instead of the hardcoded FAR/US default of FL180.
ALTER TABLE checkride_results ADD COLUMN transition_altitude_ft INT;

-- Column-level grant required because H-1 fix revoked SELECT on the whole table
GRANT SELECT (transition_altitude_ft) ON checkride_results TO anon, authenticated;
