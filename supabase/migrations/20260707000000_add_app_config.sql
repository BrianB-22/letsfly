-- App-wide config table. One row, updated via SQL to change runtime settings
-- without touching edge function code or requiring a redeploy.

CREATE TABLE IF NOT EXISTS app_config (
    id                 int  PRIMARY KEY DEFAULT 1,
    min_client_version text NOT NULL DEFAULT '0.0',
    CONSTRAINT single_row CHECK (id = 1)
);

-- Seed initial value — 0.2 is the first enforced release
INSERT INTO app_config (id, min_client_version)
VALUES (1, '0.2')
ON CONFLICT (id) DO NOTHING;

-- Only service-role can write; no direct client access needed
ALTER TABLE app_config ENABLE ROW LEVEL SECURITY;
