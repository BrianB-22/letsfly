import { createClient } from 'https://esm.sh/@supabase/supabase-js@2';

const MIN_VERSION = [0, 3]; // major.minor — patch is ignored  *** TEMP TEST: revert to [0,2] after confirming block ***

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

  let body: { client_version?: string } = {};
  try { body = await req.json(); } catch { /* version is optional */ }

  const versionStr = body.client_version ?? '';
  const parts = versionStr.split('.').map(Number);
  const major = parts[0] ?? 0;
  const minor = parts[1] ?? 0;
  const allowed = major > MIN_VERSION[0] || (major === MIN_VERSION[0] && minor >= MIN_VERSION[1]);

  console.log(`verify-client: user=${user.id} version=${versionStr || 'unknown'} allowed=${allowed}`);

  if (!allowed) {
    return json({
      allowed: false,
      message: `This version of CheckRide is out of date. Please download the latest version at simletsfly.com/checkride.`,
    });
  }

  return json({ allowed: true, message: null });
});
