const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
};

const FPD_BASE = 'https://api.flightplandatabase.com';

function fpdAuth(): string {
  const key = Deno.env.get('FPD_API_KEY') ?? '';
  return 'Basic ' + btoa(key + ':');
}

// Build a readable IFR route string from FPD nodes array.
// Format: FIX AIRWAY FIX AIRWAY FIX (entry fix, then airway when it changes)
function buildRoute(nodes: any[]): string {
  const parts: string[] = [];

  for (let i = 0; i < nodes.length; i++) {
    const node = nodes[i];
    if (node.type === 'airport') continue;

    parts.push(node.ident);

    // If the next waypoint starts a different en-route airway, insert the airway name
    const next = nodes[i + 1];
    if (next && next.type !== 'airport') {
      const nextType = next.via?.type ?? '';
      const nextAwy  = next.via?.ident ?? '';
      const currAwy  = node.via?.ident  ?? '';
      if ((nextType === 'AWY-HI' || nextType === 'AWY-LO') && nextAwy !== currAwy) {
        parts.push(nextAwy);
      }
    }
  }

  return parts.join(' ');
}

async function fetchRoute(fromICAO: string, toICAO: string, high: boolean): Promise<string | null> {
  const body = {
    fromICAO,
    toICAO,
    useAWYLO:  !high,
    useAWYHI:   high,
    useNAT:    false,
    usePACOT:  false,
    cruiseAlt: high ? 35000 : 8000,
  };

  const res = await fetch(`${FPD_BASE}/auto/generate`, {
    method: 'POST',
    headers: {
      'Authorization': fpdAuth(),
      'Content-Type':  'application/json',
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`FPD ${res.status}: ${text}`);
  }

  const data = await res.json();
  const nodes = data?.route?.nodes ?? data?.nodes ?? [];
  if (!nodes.length) return null;

  return buildRoute(nodes);
}

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  const url    = new URL(req.url);
  const from   = (url.searchParams.get('from') ?? '').toUpperCase().trim();
  const to     = (url.searchParams.get('to')   ?? '').toUpperCase().trim();

  if (!from || !to) {
    return new Response(JSON.stringify({ error: 'Missing from or to parameter' }), {
      status: 400,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }

  const [lowResult, highResult] = await Promise.allSettled([
    fetchRoute(from, to, false),
    fetchRoute(from, to, true),
  ]);

  // If both failed, return error so the frontend can show a toast
  if (lowResult.status === 'rejected' && highResult.status === 'rejected') {
    return new Response(JSON.stringify({ error: 'Route fetch failed' }), {
      status: 502,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }

  return new Response(JSON.stringify({
    low:  lowResult.status  === 'fulfilled' ? lowResult.value  : null,
    high: highResult.status === 'fulfilled' ? highResult.value : null,
  }), {
    headers: { ...corsHeaders, 'Content-Type': 'application/json' },
  });
});
