# XP12 Web API v3 Lessons

**API version:** `/api/v3/` only. v1/v2 use different naming and will 404.

**Dataref IDs are Int64 (64-bit).** e.g. `2254628761384`. Int32 silently overflows. Use `Dictionary<string, long>` and `GetInt64()`.

**IDs are session-stable only.** Re-resolve all names → IDs on every session start. Never cache across XP12 restarts.

**Resolve datarefs one at a time.** Batch resolution silently fails if any dataref is missing. Loop individually.

**Array datarefs** (`int_array`, `float_array`): fetch full array, parse element in C#. The `?index=N` URL param was unreliable in testing.

**Unit gotchas:**
- `groundspeed` — m/s → convert to kts
- `indicated_airspeed` — already kias, do NOT convert
- `elevation`, `y_agl` — metres → convert to feet
- `vh_ind_fpm` — already fpm, do NOT convert
- `aircraft/limits/Vso`, `Vno`, `Vne`, `Vfe`, `Vle` — kias, do NOT convert
- `g_nrml`, `g_side`, `g_axil` — already G units, no conversion

**`has_crashed`** does not fire for water impact or runway excursion. Detect separately: AGL < −2ft at speed, or OnGround=false after landing at speed below 10ft AGL.

**`sim/flightmodel/misc/stall_warning`** — 404s in XP12. Correct dataref unknown; needs investigation.

**Aircraft name** (`aircraft/view/acf_ui_name`) — `data` type, base64-encoded UTF-8 bytes. Decode with `Convert.FromBase64String` + `Encoding.UTF8.GetString`.

**Aircraft limits** (`aircraft/limits/Vso` etc.) — static per aircraft, capture once on first valid snapshot, log them.

**WebSocket API** at `ws://localhost:8086/api/v3` — 10Hz push. Better than REST polling long-term, not yet implemented.
