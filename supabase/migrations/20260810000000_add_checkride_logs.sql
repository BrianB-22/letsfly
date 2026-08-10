-- Opt-in raw flight log storage for CheckRide, kept in its own table (rather
-- than growing log_text on checkride_results) so logs can be purged on a
-- retention schedule without touching scored results. Populated only when
-- the user checks "Upload flight log" in the CheckRide client.
--
-- Same privacy posture as the log_text column fix (H-1, see
-- 20260706000000_security_h1_h3_fixes.sql): raw logs can contain local file
-- paths, Windows usernames, and machine details, so this table is NOT
-- readable by anon/authenticated -- service_role only (no SELECT policy).
--
-- checkride_results.id is generated client-side (CheckRide/SupabaseClient.cs)
-- so it can be reused here as the FK without a round trip.

create table checkride_logs (
  id         uuid default gen_random_uuid() primary key,
  result_id  uuid references checkride_results(id) on delete cascade not null,
  user_id    uuid references auth.users not null,
  log_text   text not null,
  created_at timestamptz default now()
);

alter table checkride_logs enable row level security;

-- Client uploads its own log right after uploading the matching
-- checkride_results row. No SELECT policy for anon/authenticated -- logs are
-- read via the Supabase dashboard or service-role key when debugging.
create policy "Users insert own logs"
  on checkride_logs for insert
  to authenticated
  with check (auth.uid() = user_id);

-- Purge periodically, independent of checkride_results, e.g.:
--   delete from checkride_logs where created_at < now() - interval '30 days';
