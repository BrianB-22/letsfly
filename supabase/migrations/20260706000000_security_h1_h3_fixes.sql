-- Security fixes from security-review.md (July 2026)
--
-- H-1: log_text (raw client debug logs) was readable by anyone via the public
--      SELECT policy on checkride_results. Logs can contain local file paths,
--      Windows usernames, and machine details. Fix: column-level grants — anon
--      and authenticated can select every column EXCEPT log_text. Only the
--      service role (support/debugging, edge functions) can read it.
--
--      Safe because every query in the codebase uses an explicit column list
--      (report.html, flights.html, C# GetLastScoresAsync) — nothing selects *.
--      NOTE: any future ALTER TABLE ... ADD COLUMN on checkride_results needs a
--      matching GRANT SELECT (new_col) TO anon, authenticated, or re-run this DO
--      block. select('*') from PostgREST will NOT work on this table anymore.
--
-- H-3: share_token default used md5(random()) — Postgres random() is not a
--      CSPRNG, so share links were predictable. Fix: pgcrypto gen_random_bytes.
--      Existing tokens are left in place (links already shared would break);
--      rotation SQL is included below, commented out.

-- ── H-1: hide log_text from public/authenticated reads ──────────────────────

REVOKE SELECT ON public.checkride_results FROM anon, authenticated;

DO $$
DECLARE cols text;
BEGIN
  SELECT string_agg(quote_ident(column_name), ', ')
    INTO cols
    FROM information_schema.columns
   WHERE table_schema = 'public'
     AND table_name   = 'checkride_results'
     AND column_name <> 'log_text';

  EXECUTE format(
    'GRANT SELECT (%s) ON public.checkride_results TO anon, authenticated',
    cols
  );
END $$;

-- ── H-3: cryptographically strong share tokens for new flights ──────────────

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA extensions;

ALTER TABLE public.saved_flights
  ALTER COLUMN share_token
  SET DEFAULT encode(extensions.gen_random_bytes(8), 'hex');  -- 16 chars, 64 bits

-- Optional: rotate ALL existing tokens. This invalidates every share link
-- users have already sent out — only run if that tradeoff is acceptable.
-- UPDATE public.saved_flights
--    SET share_token = encode(extensions.gen_random_bytes(8), 'hex');
