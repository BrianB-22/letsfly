-- SimLetsFly: saved flights table
create table saved_flights (
  id            uuid default gen_random_uuid() primary key,
  user_id       uuid references auth.users not null,
  dep_id        text not null,
  dep_name      text,
  dep_lat       double precision,
  dep_lon       double precision,
  arr_id        text not null,
  arr_name      text,
  arr_lat       double precision,
  arr_lon       double precision,
  distance_nm   double precision,
  bearing_deg   double precision,
  speed_kts     double precision,
  pilot_notes   text,
  is_public     boolean default false,
  share_token   text unique default substr(md5(random()::text), 1, 12),
  created_at    timestamptz default now()
);

-- Row Level Security
alter table saved_flights enable row level security;

-- Signed-in users can read/write their own flights
create policy "Users manage own flights"
  on saved_flights for all
  using (auth.uid() = user_id)
  with check (auth.uid() = user_id);

-- Anyone can view public/shared flights
create policy "Anyone views public flights"
  on saved_flights for select
  using (is_public = true);
