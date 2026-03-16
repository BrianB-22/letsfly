Deno.serve(async (req) => {
  const corsHeaders = {
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
  };

  if (req.method === 'OPTIONS') {
    return new Response('ok', { headers: corsHeaders });
  }

  const url  = new URL(req.url);
  const icao = url.searchParams.get('icao')?.toUpperCase().trim();
  const type = url.searchParams.get('type') ?? 'metar'; // 'metar' or 'taf'

  if (!icao) {
    return new Response(JSON.stringify({ error: 'Missing icao parameter' }), {
      status: 400,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }

  try {
    const endpoint = type === 'taf'
      ? `https://aviationweather.gov/api/data/taf?ids=${icao}&format=raw`
      : `https://aviationweather.gov/api/data/metar?ids=${icao}&format=raw`;

    const resp = await fetch(endpoint);
    const text = (await resp.text()).trim();
    const valid = resp.ok && text.length > 5 && !text.startsWith('<') && !text.toLowerCase().startsWith('no ');

    if (type === 'taf') {
      return new Response(JSON.stringify({ taf: valid ? text : null }), {
        headers: { ...corsHeaders, 'Content-Type': 'application/json' },
      });
    } else {
      return new Response(JSON.stringify({ metar: valid ? text : null }), {
        headers: { ...corsHeaders, 'Content-Type': 'application/json' },
      });
    }
  } catch (e) {
    return new Response(JSON.stringify({ error: 'Fetch failed' }), {
      status: 500,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' },
    });
  }
});
