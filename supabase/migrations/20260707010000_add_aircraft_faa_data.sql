-- Add FAA reference data columns for the aircraft type selected in the picker
-- (populated by CheckRide client >= 0.3), so a run's aircraft class/Vref/WTC
-- survive independent of AircraftDb.csv lookups.
ALTER TABLE checkride_results ADD COLUMN aircraft_manufacturer TEXT;
ALTER TABLE checkride_results ADD COLUMN aircraft_model        TEXT;
ALTER TABLE checkride_results ADD COLUMN aircraft_engine_class TEXT;
ALTER TABLE checkride_results ADD COLUMN aircraft_weight_class TEXT;
ALTER TABLE checkride_results ADD COLUMN aircraft_vref_kt      NUMERIC;
ALTER TABLE checkride_results ADD COLUMN aircraft_wtc          TEXT;

-- Column-level grant required because H-1 fix revoked SELECT on the whole table
GRANT SELECT (
    aircraft_manufacturer, aircraft_model, aircraft_engine_class,
    aircraft_weight_class, aircraft_vref_kt, aircraft_wtc
) ON checkride_results TO anon, authenticated;
