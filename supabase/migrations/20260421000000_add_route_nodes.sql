-- Add route_nodes JSONB column to store pulled IFR route for export
alter table saved_flights
  add column if not exists route_nodes jsonb default null;
