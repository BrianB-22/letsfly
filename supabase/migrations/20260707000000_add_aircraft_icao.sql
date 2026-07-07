-- Add aircraft ICAO type code column populated by CheckRide client >= 0.3
ALTER TABLE checkride_results ADD COLUMN aircraft_icao TEXT;

-- Column-level grant required because H-1 fix revoked SELECT on the whole table
GRANT SELECT (aircraft_icao) ON checkride_results TO anon, authenticated;
