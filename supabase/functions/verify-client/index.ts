import { createClient } from 'https://esm.sh/@supabase/supabase-js@2';

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
};

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...corsHeaders, 'Content-Type': 'application/json' },
  });
}

function parseVersion(v: string): [number, number, number] {
  const parts = (v ?? '').split('.').map(Number);
  return [parts[0] ?? 0, parts[1] ?? 0, parts[2] ?? 0];
}

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: corsHeaders });

  const authHeader = req.headers.get('Authorization');
  if (!authHeader) return json({ allowed: false, message: 'Not authenticated.' }, 401);

  // Verify the caller's JWT
  const sb = createClient(
    Deno.env.get('SUPABASE_URL')!,
    Deno.env.get('SUPABASE_ANON_KEY')!,
    { global: { headers: { Authorization: authHeader } } },
  );
  const { data: { user }, error } = await sb.auth.getUser();
  if (error || !user) return json({ allowed: false, message: 'Not authenticated.' }, 401);

  // Read minimum version from config table using service role (bypasses RLS)
  const sbAdmin = createClient(
    Deno.env.get('SUPABASE_URL')!,
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,
  );
  const { data: config } = await sbAdmin
    .from('app_config')
    .select('min_client_version')
    .eq('id', 1)
    .single();

  const minVersion = parseVersion(config?.min_client_version ?? '0.0');

  let body: { client_version?: string } = {};
  try { body = await req.json(); } catch { /* version field is optional */ }

  const clientVersion = parseVersion(body.client_version ?? '');
  const [cMaj, cMin, cPatch] = clientVersion;
  const [mMaj, mMin, mPatch] = minVersion;
  const allowed =
    cMaj > mMaj ||
    (cMaj === mMaj && cMin > mMin) ||
    (cMaj === mMaj && cMin === mMin && cPatch >= mPatch);

  console.log(`verify-client: user=${user.id} client=${body.client_version ?? 'unknown'} min=${config?.min_client_version ?? '?'} allowed=${allowed}`);

  if (!allowed) {
    return json({
      allowed: false,
      message: `This version of CheckRide is out of date. Please download the latest version at simletsfly.com/checkride.`,
    });
  }

  return json({ allowed: true, message: null });
});
