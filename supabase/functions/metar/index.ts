Deno.serve(async (req) => {
  const corsHeaders = {
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
  };

  if (req.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  const url = new URL(req.url);
  const icao = url.searchParams.get('icao')?.toUpperCase().trim();

  if (!icao) {
    return new Response(JSON.stringify({ error: 'Missing icao parameter' }), {
      status: 400,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }

  try {
    const resp = await fetch(
      `https://aviationweather.gov/api/data/metar?ids=${icao}&format=raw`
    );
    const text = (await resp.text()).trim();
    const valid = resp.ok && text.length > 5 && !text.startsWith('<');
    return new Response(JSON.stringify({ metar: valid ? text : null }), {
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  } catch (e) {
    return new Response(JSON.stringify({ error: 'Fetch failed' }), {
      status: 500,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }
});
