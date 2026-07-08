-- Records the CheckRide client app version (e.g. "0.2.3") on each upload,
-- so support/debugging can tell which build produced a given run.
ALTER TABLE checkride_results ADD COLUMN client_version TEXT;

-- Column-level grant required because H-1 fix revoked SELECT on the whole table
GRANT SELECT (client_version) ON checkride_results TO anon, authenticated;
