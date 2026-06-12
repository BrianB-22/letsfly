alter table profiles
  add column if not exists aircraft_preset text default null;
