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

function fmtTime(s: number): string {
  if (!s) return '—';
  return `${Math.floor(s / 60)}m ${s % 60}s`;
}

function buildPrompt(
  run: Record<string, unknown>,
  prevRun: Record<string, unknown> | null,
  depId: string,
  arrId: string,
): string {
  const stats   = (run.stats   as Record<string, number>) || {};
  const summary = (run.summary as Record<string, string>) || {};
  const breakdown = (run.breakdown as Array<{ Label: string; Pts: number; Count: number }>) || [];
  const events    = (run.events  as Array<{ Type: string }>) || [];

  const date = new Date(run.recorded_at as string).toLocaleDateString('en-US', {
    month: 'long', day: 'numeric', year: 'numeric',
  });
  const sim   = run.sim === 'xplane12' ? 'X-Plane 12' : run.sim === 'xplane11' ? 'X-Plane 11' : String(run.sim || 'flight sim');
  const route = depId && arrId ? `${depId} → ${arrId}` : depId || arrId || 'unknown route';

  const penalties = breakdown.filter(b => b.Pts < 0);
  const bonuses   = breakdown.filter(b => b.Pts > 0);

  const badEventTypes = new Set([
    'Crash','RunwayExcursion','GearUpLanding','Overspeed','Stall',
    'VeryHighG','VeryHighBank','HardLanding','HighDescentRate',
    'UnstableApproach','ExcessiveApproachSpeed',
  ]);
  const badEvents = events.filter(e => badEventTypes.has(e.Type));

  let prevSection = '';
  if (prevRun) {
    const scoreDiff = ((run.score as number) ?? 0) - ((prevRun.score as number) ?? 0);
    const diffStr   = scoreDiff > 0 ? `+${scoreDiff}` : String(scoreDiff);
    const prevDate  = new Date(prevRun.recorded_at as string).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });

    prevSection = `\nPrevious run (${prevDate}): ${prevRun.score}/100 (${prevRun.grade}) — ${diffStr} pts.`;

    const prevBreakdown = (prevRun.breakdown as Array<{ Label: string; Pts: number }>) || [];
    if (prevBreakdown.length) {
      const prevMap = Object.fromEntries(prevBreakdown.map(b => [b.Label, b.Pts]));
      const changes = breakdown
        .filter(b => prevMap[b.Label] !== undefined && prevMap[b.Label] !== b.Pts)
        .map(b => {
          const delta = b.Pts - prevMap[b.Label];
          return `${b.Label} ${delta > 0 ? 'improved' : 'worsened'} (${delta > 0 ? '+' : ''}${delta} pts)`;
        })
        .slice(0, 4);
      if (changes.length) prevSection += ` Changes: ${changes.join('; ')}.`;
    }
  }

  const penaltyLines = penalties.length
    ? '\n' + penalties.map(b => `- ${b.Label}: ${b.Pts} pts (×${b.Count})`).join('\n')
    : ' None';
  const bonusLines = bonuses.length
    ? '\n' + bonuses.map(b => `+ ${b.Label}: +${b.Pts} pts`).join('\n')
    : ' None';
  const badEventLine = badEvents.length
    ? `\nNotable events: ${badEvents.map(e => e.Type).join(', ')}`
    : '';

  return `You are a flight instructor writing a concise debrief for a simulated flight.

Flight: ${route} | ${date} | ${sim} | ${run.aircraft || 'unknown aircraft'}
Score: ${run.score ?? '—'}/100 | Grade: ${run.grade || '?'}
Flight time: ${fmtTime(stats.FlightTimeSec)} | Distance: ${stats.DistanceNm ? stats.DistanceNm.toFixed(1) + ' nm' : '—'}
Landing: ${summary.LandingQuality || '—'} at ${stats.LandingVsFpm || '—'} fpm | Crosswind: ${stats.CrosswindAtLandingKt != null ? stats.CrosswindAtLandingKt.toFixed(0) + ' kt' : '—'}
Max G: ${stats.MaxGForceNormal != null ? stats.MaxGForceNormal.toFixed(2) : '—'}G | Max IAS: ${stats.MaxIasKts != null ? stats.MaxIasKts.toFixed(0) + ' kt' : '—'} | Max Bank: ${stats.MaxBankAngleDeg != null ? Math.abs(stats.MaxBankAngleDeg).toFixed(1) + '°' : '—'}
Autopilot: ${stats.AutopilotPct != null ? stats.AutopilotPct.toFixed(0) + '%' : '—'}

Penalties:${penaltyLines}
Bonuses:${bonusLines}${badEventLine}${prevSection}

Write a 2–3 paragraph flight debrief. Cover: overall assessment, what went well, what needs improvement (be specific about the penalty items).${prevRun ? ' Include a sentence comparing to the previous run.' : ''} Write in the style of a real CFI — professional but conversational. Start directly with the first sentence of the debrief. No title, no headers, no bullet points — plain paragraphs only.`;
}

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: corsHeaders });

  const authHeader = req.headers.get('Authorization');
  if (!authHeader) return json({ error: 'Unauthorized' }, 401);

  // Verify the caller's JWT and get their user record
  const sbUser = createClient(
    Deno.env.get('SUPABASE_URL')!,
    Deno.env.get('SUPABASE_ANON_KEY')!,
    { global: { headers: { Authorization: authHeader } } },
  );
  const { data: { user }, error: authError } = await sbUser.auth.getUser();
  if (authError || !user) return json({ error: 'Unauthorized' }, 401);

  // Admin client for unrestricted data operations
  const sb = createClient(
    Deno.env.get('SUPABASE_URL')!,
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,
  );

  let body: { run_id?: string };
  try { body = await req.json(); } catch { return json({ error: 'Invalid JSON' }, 400); }
  const { run_id } = body;
  if (!run_id) return json({ error: 'Missing run_id' }, 400);

  // Fetch the run
  const { data: run, error: runError } = await sb
    .from('checkride_results')
    .select('id,user_id,flight_id,score,grade,aircraft,sim,recorded_at,events,summary,stats,breakdown,conditions,debrief')
    .eq('id', run_id)
    .single();

  if (runError || !run) return json({ error: 'Run not found' }, 404);
  if (run.user_id !== user.id) return json({ error: 'Forbidden' }, 403);

  // Return existing debrief — handles concurrent calls and double-loads
  if (run.debrief) return json({ debrief: run.debrief });

  // Fetch previous run for score comparison
  const { data: prevRun } = await sb
    .from('checkride_results')
    .select('score,grade,breakdown,recorded_at')
    .eq('user_id', user.id)
    .lt('recorded_at', run.recorded_at)
    .order('recorded_at', { ascending: false })
    .limit(1)
    .maybeSingle();

  // Fetch flight route
  let depId = '', arrId = '';
  if (run.flight_id) {
    const { data: flight } = await sb
      .from('saved_flights')
      .select('dep_id,arr_id')
      .eq('id', run.flight_id)
      .single();
    if (flight) { depId = flight.dep_id || ''; arrId = flight.arr_id || ''; }
  }

  const prompt = buildPrompt(run, prevRun, depId, arrId);

  const anthropicResp = await fetch('https://api.anthropic.com/v1/messages', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-api-key': Deno.env.get('ANTHROPIC_API_KEY')!,
      'anthropic-version': '2023-06-01',
    },
    body: JSON.stringify({
      model: 'claude-haiku-4-5-20251001',
      max_tokens: 600,
      messages: [{ role: 'user', content: prompt }],
    }),
  });

  if (!anthropicResp.ok) {
    const errText = await anthropicResp.text();
    console.error('Anthropic error:', errText);
    return json({ error: 'AI generation failed' }, 500);
  }

  const ai = await anthropicResp.json();
  const raw = (ai.content?.[0]?.text as string | undefined)?.trim();
  if (!raw) return json({ error: 'Empty response from AI' }, 500);

  // Strip any leading markdown header line the model occasionally adds
  const debrief = raw.replace(/^#{1,3}\s+[^\n]*\n+/, '').trim();

  // Persist so every future load is free
  await sb.from('checkride_results').update({ debrief }).eq('id', run_id);

  return json({ debrief });
});
