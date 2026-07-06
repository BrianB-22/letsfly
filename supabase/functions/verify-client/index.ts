import { createClient } from 'https://esm.sh/@supabase/supabase-js@2';

// Stub: always allows access. Add subscription / version-gate logic here in the future.
// The C# client sends { client_version } and a valid JWT — no client changes needed to add gating.

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

  // Future: check body.client_version against a minimum, check user subscription, etc.
  // Example:
  //   const { data: sub } = await sbAdmin.from('subscriptions').select('status').eq('user_id', user.id).single();
  //   if (!sub || sub.status !== 'active') return json({ allowed: false, message: 'Your subscription has expired. Renew at simletsfly.com' });

  console.log(`verify-client: user=${user.id} version=${body.client_version ?? 'unknown'}`);

  return json({ allowed: true, message: null });
});
