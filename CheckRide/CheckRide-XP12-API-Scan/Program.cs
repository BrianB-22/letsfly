using System.Management;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

const string BASE    = "http://localhost:8086/api/v3";
const string VERSION = "1.0";
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

// ── Dataref catalogue ─────────────────────────────────────────────────────────
// Each entry: (category, name, array index — -1 = scalar)
var DATAREFS = new (string Cat, string Name, int Idx)[]
{
    // ── Position ──
    ("Position",  "sim/flightmodel/position/latitude",                  -1),
    ("Position",  "sim/flightmodel/position/longitude",                 -1),
    ("Position",  "sim/flightmodel/position/elevation",                 -1),
    ("Position",  "sim/flightmodel/position/y_agl",                     -1),
    ("Position",  "sim/flightmodel/position/indicated_airspeed",        -1),
    ("Position",  "sim/flightmodel/position/groundspeed",               -1),
    ("Position",  "sim/flightmodel/position/vh_ind_fpm",                -1),
    ("Position",  "sim/flightmodel/position/mag_psi",                   -1),
    ("Position",  "sim/flightmodel/position/hpath",                     -1),
    ("Position",  "sim/flightmodel/position/phi",                       -1),
    ("Position",  "sim/flightmodel/position/theta",                     -1),
    ("Position",  "sim/flightmodel/position/alpha",                     -1),
    ("Position",  "sim/flightmodel/position/P",                         -1),
    ("Position",  "sim/flightmodel/position/Q",                         -1),
    ("Position",  "sim/flightmodel/position/R",                         -1),

    // ── Forces / G ──
    ("Forces",    "sim/flightmodel/forces/g_nrml",                      -1),
    ("Forces",    "sim/flightmodel/forces/g_side",                      -1),
    ("Forces",    "sim/flightmodel/forces/g_axil",                      -1),
    ("Forces",    "sim/flightmodel/forces/fnrml_gear",                  -1),

    // ── Gear ──
    ("Gear",      "sim/flightmodel2/gear/on_ground",                     0),
    ("Gear",      "sim/flightmodel2/gear/on_ground",                     1),
    ("Gear",      "sim/flightmodel2/gear/on_ground",                     2),
    ("Gear",      "sim/flightmodel2/gear/deploy_ratio",                  0),
    ("Gear",      "sim/flightmodel2/gear/deploy_ratio",                  1),
    ("Gear",      "sim/flightmodel2/gear/deploy_ratio",                  2),
    ("Gear",      "sim/flightmodel2/gear/tire_vertical_deflection_mtr",  0),
    ("Gear",      "sim/flightmodel2/gear/tire_vertical_deflection_mtr",  1),
    ("Gear",      "sim/flightmodel2/gear/tire_vertical_deflection_mtr",  2),

    // ── Engine ──
    ("Engine",    "sim/flightmodel/engine/ENGN_running",                 0),
    ("Engine",    "sim/flightmodel/engine/ENGN_running",                 1),
    ("Engine",    "sim/cockpit2/engine/indicators/N1_percent",           0),
    ("Engine",    "sim/cockpit2/engine/indicators/N1_percent",           1),
    ("Engine",    "sim/cockpit2/engine/indicators/N2_percent",           0),
    ("Engine",    "sim/cockpit2/engine/indicators/N2_percent",           1),
    ("Engine",    "sim/cockpit2/engine/indicators/ITT_deg_C",            0),
    ("Engine",    "sim/cockpit2/engine/indicators/ITT_deg_C",            1),
    ("Engine",    "sim/cockpit2/engine/indicators/engine_speed_rpm",     0),
    ("Engine",    "sim/cockpit2/engine/indicators/engine_speed_rpm",     1),
    ("Engine",    "sim/cockpit2/engine/actuators/throttle_ratio",        0),
    ("Engine",    "sim/cockpit2/engine/actuators/throttle_ratio",        1),

    // ── Torque (turboprop) — multiple paths to find the right one ──
    ("Torque",    "sim/cockpit2/engine/indicators/torque_n_mtr",         0),
    ("Torque",    "sim/cockpit2/engine/indicators/torque_n_mtr",         1),
    ("Torque",    "sim/cockpit/engine/torque",                           0),
    ("Torque",    "sim/cockpit/engine/torque",                           1),
    ("Torque",    "sim/flightmodel/engine/ENGN_TRQ",                     0),
    ("Torque",    "sim/flightmodel/engine/ENGN_TRQ",                     1),
    ("Torque",    "sim/flightmodel2/engines/torque_n_mtr",               0),
    ("Torque",    "sim/cockpit2/engine/indicators/torque_ft_lbf",        0),
    ("Torque",    "sim/cockpit2/engine/indicators/torque_ft_lbf",        1),

    // ── Fuel ──
    ("Fuel",      "sim/flightmodel/weight/m_fuel",                       0),
    ("Fuel",      "sim/flightmodel/weight/m_fuel",                       1),
    ("Fuel",      "sim/flightmodel/weight/m_fuel_total",                -1),
    ("Fuel",      "sim/cockpit2/fuel/fuel_quantity",                     0),
    ("Fuel",      "sim/cockpit2/fuel/fuel_quantity",                     1),
    ("Fuel",      "sim/cockpit2/engine/indicators/fuel_flow_kg_sec",     0),
    ("Fuel",      "sim/cockpit2/engine/indicators/fuel_flow_kg_sec",     1),

    // ── Oil ──
    ("Oil",       "sim/cockpit2/engine/indicators/oil_pressure_psi",     0),
    ("Oil",       "sim/cockpit2/engine/indicators/oil_pressure_psi",     1),
    ("Oil",       "sim/cockpit2/engine/indicators/oil_temperature_deg_C", 0),
    ("Oil",       "sim/cockpit2/engine/indicators/oil_temperature_deg_C", 1),

    // ── Prop ──
    ("Prop",      "sim/cockpit2/engine/indicators/prop_speed_rpm",       0),
    ("Prop",      "sim/cockpit2/engine/indicators/prop_speed_rpm",       1),
    ("Prop",      "sim/cockpit2/engine/actuators/prop_ratio",            0),
    ("Prop",      "sim/cockpit2/engine/actuators/prop_ratio",            1),

    // ── Controls ──
    ("Controls",  "sim/flightmodel/controls/parkbrake",                 -1),
    ("Controls",  "sim/flightmodel/controls/flaprat",                   -1),
    ("Controls",  "sim/flightmodel2/controls/flap_handle_deploy_ratio", -1),
    ("Controls",  "sim/flightmodel2/controls/speedbrake_ratio",         -1),
    ("Controls",  "sim/cockpit2/controls/elevator_trim",                -1),
    ("Controls",  "sim/cockpit2/controls/aileron_trim",                 -1),
    ("Controls",  "sim/cockpit2/controls/rudder_trim",                  -1),

    // ── Aircraft Performance Limits ──
    ("Limits",    "sim/aircraft/view/acf_Vso",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Vs",                           -1),
    ("Limits",    "sim/aircraft/view/acf_Vno",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Vne",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Vfe",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Vle",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Vmo",                          -1),
    ("Limits",    "sim/aircraft/view/acf_Mmo",                          -1),
    ("Limits",    "sim/aircraft/view/acf_max_G",                        -1),
    ("Limits",    "sim/aircraft/view/acf_min_G",                        -1),
    ("Limits",    "sim/aircraft/view/acf_WMaxGross",                    -1),
    ("Limits",    "sim/aircraft/limits/Vso",                            -1),  // known 404 path — verify per aircraft
    ("Limits",    "sim/aircraft/limits/Vle",                            -1),  // known 404 path — verify per aircraft

    // ── Stall Warning — multiple paths to find the right one ──
    ("Stall",     "sim/cockpit2/annunciators/stall_warning",            -1),
    ("Stall",     "sim/flightmodel/misc/stall_warning",                 -1),  // known 404
    ("Stall",     "sim/cockpit/warnings/stall_warning",                 -1),
    ("Stall",     "sim/flightmodel2/misc/stall_warning_on",             -1),
    ("Stall",     "sim/operation/warnings/warn_stall",                  -1),
    ("Stall",     "sim/flightmodel/position/stall_warning",             -1),

    // ── Overspeed ──
    ("Overspeed", "sim/flightmodel/failures/over_vne",                  -1),
    ("Overspeed", "sim/operation/warnings/warn_fast",                   -1),
    ("Overspeed", "sim/cockpit2/annunciators/over_vmo",                 -1),
    ("Overspeed", "sim/cockpit2/annunciators/airspeed_hi",              -1),

    // ── Systems ──
    ("Systems",   "sim/cockpit/switches/pitot_heat_on",                 -1),
    ("Systems",   "sim/cockpit2/switches/landing_lights_on",            -1),
    ("Systems",   "sim/cockpit/electrical/beacon_lights_on",            -1),
    ("Systems",   "sim/cockpit2/switches/strobe_lights_on",             -1),
    ("Systems",   "sim/cockpit/radios/transponder_mode",                -1),
    ("Systems",   "sim/cockpit2/autopilot/autopilot_on",                -1),
    ("Systems",   "sim/cockpit/switches/anti_ice_on",                   -1),
    ("Systems",   "sim/cockpit2/switches/anti_ice_on",                  -1),
    ("Systems",   "sim/cockpit2/switches/prop_heat_on",                 -1),
    ("Systems",   "sim/cockpit2/switches/window_heat_on",               -1),
    ("Systems",   "sim/cockpit2/gauges/indicators/slip_deg",            -1),
    ("Systems",   "sim/cockpit2/gauges/indicators/altitude_ft_pilot",   -1),
    ("Systems",   "sim/cockpit2/gauges/indicators/barometer_setting_in_hg_pilot", -1),

    // ── Annunciators / Failures ──
    ("Annunc",    "sim/cockpit2/annunciators/engine_fires",              0),
    ("Annunc",    "sim/cockpit2/annunciators/engine_fires",              1),
    ("Annunc",    "sim/cockpit2/annunciators/oil_pressure",             -1),
    ("Annunc",    "sim/cockpit2/annunciators/fuel_pressure",            -1),
    ("Annunc",    "sim/cockpit2/annunciators/hydraulic_pressure",       -1),
    ("Annunc",    "sim/cockpit2/annunciators/low_voltage",              -1),
    ("Annunc",    "sim/cockpit2/annunciators/master_caution",           -1),
    ("Annunc",    "sim/cockpit2/annunciators/master_warning",           -1),
    ("Annunc",    "sim/cockpit2/annunciators/gear_unsafe",              -1),
    ("Annunc",    "sim/flightmodel/failures/frm_ice",                   -1),
    ("Annunc",    "sim/flightmodel/failures/over_g",                    -1),
    ("Annunc",    "sim/operation/failures/rel_engfir0",                 -1),
    ("Annunc",    "sim/operation/failures/rel_engfir1",                 -1),

    // ── Weather ──
    ("Weather",   "sim/weather/aircraft/wind_speed_kts",                 0),
    ("Weather",   "sim/weather/aircraft/wind_direction_degt",            0),
    ("Weather",   "sim/weather/aircraft/temperature_ambient_deg_c",     -1),
    ("Weather",   "sim/weather/visibility_reported_m",                  -1),
    ("Weather",   "sim/weather/region/cloud_base_msl_m",                 0),
    ("Weather",   "sim/weather/region/cloud_coverage_percent",           0),

    // ── Time / Lighting ──
    ("Time",      "sim/time/local_time_sec",                            -1),
    ("Time",      "sim/graphics/scenery/sun_pitch_degrees",             -1),

    // ── Aircraft Info ──
    ("Aircraft",  "sim/aircraft/view/acf_ui_name",                      -1),
    ("Aircraft",  "sim/aircraft/view/acf_ICAO",                         -1),
    ("Aircraft",  "sim/aircraft/view/acf_tailnum",                      -1),
    ("Aircraft",  "sim/aircraft/view/acf_num_engines",                  -1),
    ("Aircraft",  "sim/aircraft/view/acf_en_type",                       0),
};

// ── Helpers ───────────────────────────────────────────────────────────────────

string Key(string name, int idx) => idx < 0 ? name : $"{name}[{idx}]";

async Task<ScanResult> Probe(string name, int idx)
{
    try
    {
        var listUrl = $"{BASE}/datarefs?filter[name]={Uri.EscapeDataString(name)}";
        using var listResp = await http.GetAsync(listUrl);
        if (!listResp.IsSuccessStatusCode)
            return new(name, idx, "HTTP-" + (int)listResp.StatusCode, "", "");

        var listJson = await listResp.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listJson);
        var arr = listDoc.RootElement.GetProperty("data");
        if (arr.GetArrayLength() == 0)
            return new(name, idx, "404", "", "");

        var entry = arr[0];
        long id   = entry.GetProperty("id").GetInt64();
        string typ = entry.TryGetProperty("value_type", out var vt) ? vt.GetString() ?? "" : "";

        var valUrl = $"{BASE}/datarefs/{id}/value";
        using var valResp = await http.GetAsync(valUrl);
        if (!valResp.IsSuccessStatusCode)
            return new(name, idx, "VAL-ERR-" + (int)valResp.StatusCode, "", typ);

        var valJson = await valResp.Content.ReadAsStringAsync();
        using var valDoc = JsonDocument.Parse(valJson);
        var data = valDoc.RootElement.GetProperty("data");

        string value;
        if (data.ValueKind == JsonValueKind.Array)
        {
            value = idx < 0
                ? "[" + string.Join(", ", data.EnumerateArray().Take(8).Select(FormatElem))
                      + (data.GetArrayLength() > 8 ? ", ..." : "") + "]"
                : idx < data.GetArrayLength()
                    ? FormatElem(data[idx])
                    : "(index out of range)";
        }
        else if (data.ValueKind == JsonValueKind.String)
        {
            try { value = Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!)).TrimEnd('\0').Trim(); }
            catch { value = data.GetString() ?? ""; }
        }
        else
        {
            value = FormatElem(data);
        }

        return new(name, idx, "OK", value, typ);
    }
    catch (TaskCanceledException) { return new(name, idx, "TIMEOUT", "", ""); }
    catch (HttpRequestException ex) { return new(name, idx, "CONN-ERR", ex.Message, ""); }
    catch (Exception ex)            { return new(name, idx, "ERR", $"{ex.GetType().Name}: {ex.Message}", ""); }
}

static string FormatElem(JsonElement e) => e.ValueKind == JsonValueKind.Number
    ? (e.TryGetDouble(out var d) ? d.ToString("G6") : e.GetRawText())
    : e.GetRawText();

async Task<bool> CheckXP12()
{
    try { var r = await http.GetAsync("http://localhost:8086/api/capabilities"); return r.IsSuccessStatusCode; }
    catch { return false; }
}

async Task<List<ScanResult>> RunScan(string label, StringBuilder log)
{
    var results    = new List<ScanResult>();
    int total      = DATAREFS.Length;
    int done       = 0;
    string? curCat = null;

    log.AppendLine($"=== {label} ===");
    log.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    log.AppendLine();

    foreach (var (cat, name, idx) in DATAREFS)
    {
        if (cat != curCat)
        {
            if (curCat != null) log.AppendLine();
            log.AppendLine($"--- {cat} ---");
            curCat = cat;
        }

        var r = await Probe(name, idx);
        results.Add(r);
        done++;

        log.AppendLine(r.Status == "OK"
            ? $"  {Key(r.Name, r.Idx),-72} = {r.Value}  [{r.RawType}]"
            : string.IsNullOrEmpty(r.Value)
                ? $"  {Key(r.Name, r.Idx),-72}   [{r.Status}]"
                : $"  {Key(r.Name, r.Idx),-72}   [{r.Status}] {r.Value}");

        Console.Write($"\r  {done}/{total}  {Key(name, idx),-65}");
    }

    Console.WriteLine();
    int found  = results.Count(r => r.Status == "OK");
    int nf     = results.Count(r => r.Status == "404");
    int errs   = results.Count(r => r.Status != "OK" && r.Status != "404");
    log.AppendLine();
    log.AppendLine($"Summary: {found} found / {nf} not found / {errs} errors");
    log.AppendLine();
    return results;
}

void WriteDiff(List<ScanResult> idle, List<ScanResult> eng, StringBuilder log)
{
    log.AppendLine("=== DIFF — Values that changed from IDLE to ENGINES RUNNING ===");
    log.AppendLine();

    bool any = false;
    foreach (var ir in idle.Where(r => r.Status == "OK"))
    {
        var er = eng.FirstOrDefault(r => r.Name == ir.Name && r.Idx == ir.Idx);
        if (er is null || er.Status != "OK" || ir.Value == er.Value) continue;
        any = true;
        string delta = "";
        if (double.TryParse(ir.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var vi) &&
            double.TryParse(er.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ve))
        {
            var d = ve - vi;
            delta = $"  (Δ {(d >= 0 ? "+" : "")}{d:G4})";
        }

        log.AppendLine($"  {Key(ir.Name, ir.Idx)}");
        log.AppendLine($"    IDLE:    {ir.Value}");
        log.AppendLine($"    ENGINES: {er.Value}{delta}");
    }
    if (!any) log.AppendLine("  (no scalar changes detected)");

    var appeared = eng.Where(e => e.Status == "OK" &&
                                   idle.Any(i => i.Name == e.Name && i.Idx == e.Idx && i.Status == "404"))
                      .ToList();
    if (appeared.Any())
    {
        log.AppendLine();
        log.AppendLine("  -- Appeared with engines running (were 404 at idle) --");
        foreach (var r in appeared)
            log.AppendLine($"  {Key(r.Name, r.Idx),-72} = {r.Value}  [{r.RawType}]");
    }

    log.AppendLine();
}

// ── System info ───────────────────────────────────────────────────────────────

static string WmiFirst(string query, string prop)
{
    try
    {
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject obj in searcher.Get())
            return obj[prop]?.ToString()?.Trim() ?? "—";
    }
    catch { }
    return "—";
}

static string WmiAll(string query, string prop)
{
    try
    {
        var results = new List<string>();
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject obj in searcher.Get())
        {
            var val = obj[prop]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val)) results.Add(val);
        }
        return results.Count > 0 ? string.Join(", ", results) : "—";
    }
    catch { }
    return "—";
}

async Task<string> GetXP12Version()
{
    try
    {
        var resp = await http.GetAsync("http://localhost:8086/api/capabilities");
        if (!resp.IsSuccessStatusCode) return "unknown";
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        // Try common paths
        if (root.TryGetProperty("data", out var data))
        {
            if (data.TryGetProperty("xplane_version_number", out var v)) return v.ToString();
            if (data.TryGetProperty("version", out var v2))              return v2.GetString() ?? "unknown";
        }
        // Return raw if we can't parse it
        return json[..Math.Min(200, json.Length)];
    }
    catch { return "unknown"; }
}

void AppendSystemInfo(StringBuilder log, string xp12Version)
{
    log.AppendLine("=== SYSTEM INFORMATION ===");

    // OS
    log.AppendLine($"OS:          {WmiFirst("SELECT * FROM Win32_OperatingSystem", "Caption")} " +
                   $"(Build {WmiFirst("SELECT * FROM Win32_OperatingSystem", "BuildNumber")})");

    // CPU
    log.AppendLine($"CPU:         {WmiFirst("SELECT * FROM Win32_Processor", "Name")}");
    log.AppendLine($"CPU Cores:   {WmiFirst("SELECT * FROM Win32_Processor", "NumberOfCores")} cores / " +
                   $"{WmiFirst("SELECT * FROM Win32_Processor", "NumberOfLogicalProcessors")} logical");
    log.AppendLine($"CPU Speed:   {WmiFirst("SELECT * FROM Win32_Processor", "MaxClockSpeed")} MHz");

    // RAM
    try
    {
        long totalBytes = 0;
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
        foreach (ManagementObject obj in searcher.Get())
            totalBytes += Convert.ToInt64(obj["Capacity"]);
        log.AppendLine($"RAM:         {totalBytes / (1024L * 1024 * 1024)} GB installed");
    }
    catch { log.AppendLine("RAM:         —"); }

    // GPU(s)
    log.AppendLine($"GPU:         {WmiAll("SELECT * FROM Win32_VideoController", "Name")}");
    log.AppendLine($"VRAM:        {WmiAll("SELECT * FROM Win32_VideoController", "AdapterRAM")} bytes");
    log.AppendLine($"Driver:      {WmiAll("SELECT * FROM Win32_VideoController", "DriverVersion")}");

    // Display
    log.AppendLine($"Resolution:  {WmiFirst("SELECT * FROM Win32_VideoController", "CurrentHorizontalResolution")}" +
                   $" × {WmiFirst("SELECT * FROM Win32_VideoController", "CurrentVerticalResolution")}");

    // XP12
    log.AppendLine($"XP12 API:    {xp12Version}");

    log.AppendLine(new string('=', 78));
    log.AppendLine();
}

// ── Entry point ───────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine($"║   CheckRide  XP12 API Scanner  v{VERSION,-21}║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();

Console.Write("Connecting to X-Plane 12 API on localhost:8086 ... ");
if (!await CheckXP12())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("FAILED");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Could not reach http://localhost:8086");
    Console.WriteLine("Make sure X-Plane 12 is running with a flight loaded.");
    Console.WriteLine("Check Settings → Network → 'Allow Incoming Connections' is enabled.");
    Console.WriteLine();
    Console.WriteLine("Press any key to exit.");
    Console.ReadKey();
    return;
}
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Connected.");
Console.ResetColor();
Console.WriteLine();

// ── Diagnostic: trace full probe for one known dataref ──
Console.WriteLine("Diagnostic — tracing full probe for 'sim/flightmodel/position/latitude':");
try
{
    // Step 1: list request
    var listUrl  = $"{BASE}/datarefs?filter[name]={Uri.EscapeDataString("sim/flightmodel/position/latitude")}";
    Console.WriteLine($"  LIST  GET {listUrl}");
    var listResp = await http.GetAsync(listUrl);
    var listBody = await listResp.Content.ReadAsStringAsync();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  LIST  HTTP {(int)listResp.StatusCode}");
    Console.WriteLine($"  LIST  {listBody[..Math.Min(300, listBody.Length)]}");
    Console.ResetColor();

    // Step 2: parse ID
    using var listDoc = JsonDocument.Parse(listBody);
    var arr = listDoc.RootElement.GetProperty("data");
    Console.WriteLine($"  LIST  array length = {arr.GetArrayLength()}");
    if (arr.GetArrayLength() > 0)
    {
        var entry = arr[0];
        Console.WriteLine($"  LIST  id raw text = {entry.GetProperty("id").GetRawText()}");
        long id = entry.GetProperty("id").GetInt64();
        Console.WriteLine($"  LIST  id as long  = {id}");

        // Step 3: value request
        var valUrl = $"{BASE}/datarefs/{id}/value";
        Console.WriteLine($"  VALUE GET {valUrl}");
        var valResp = await http.GetAsync(valUrl);
        var valBody = await valResp.Content.ReadAsStringAsync();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  VALUE HTTP {(int)valResp.StatusCode}");
        Console.WriteLine($"  VALUE {valBody[..Math.Min(300, valBody.Length)]}");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  FAILED ({ex.GetType().Name}): {ex.Message}");
    Console.ResetColor();
}
Console.WriteLine();

var log = new StringBuilder();
log.AppendLine("CheckRide XP12 API Debug Scan");
log.AppendLine($"Version:   {VERSION}");
log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
log.AppendLine($"Datarefs:  {DATAREFS.Length} probed");
log.AppendLine(new string('=', 78));
log.AppendLine();

Console.Write("Collecting system information... ");
var xp12Ver = await GetXP12Version();
AppendSystemInfo(log, xp12Ver);
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Done.");
Console.ResetColor();
Console.WriteLine();

// ── Scan 1 — Idle ──
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("SCAN 1 — IDLE");
Console.ResetColor();
Console.WriteLine("Aircraft should be loaded, sitting on the ramp, engines OFF.");
Console.Write("Press Enter to begin...");
Console.ReadLine();
Console.WriteLine();

var idleResults = await RunScan("SCAN 1 — IDLE (engines off)", log);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Done. {idleResults.Count(r => r.Status == "OK")} datarefs found.");
Console.ResetColor();
Console.WriteLine();

// ── Scan 2 — Engines running ──
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("SCAN 2 — ENGINES RUNNING");
Console.ResetColor();
Console.WriteLine("Start all engines. Start both engines and get the aircraft ready for flight.");
Console.WriteLine("Avionics on, systems running — whatever 'ready for departure' means for this aircraft.");
Console.Write("Press Enter when ready...");
Console.ReadLine();
Console.WriteLine();

var engResults = await RunScan("SCAN 2 — ENGINES RUNNING (ready for flight)", log);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Done. {engResults.Count(r => r.Status == "OK")} datarefs found.");
Console.ResetColor();
Console.WriteLine();

// ── Diff ──
WriteDiff(idleResults, engResults, log);

// ── Write log ──
string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
string logFile  = Path.Combine(docsPath, $"xp12-api-scan-{DateTime.Now:yyyyMMdd-HHmmss}.log");
await File.WriteAllTextAsync(logFile, log.ToString(), Encoding.UTF8);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Log saved to:");
Console.WriteLine($"  {logFile}");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("Send that file to the developer. Press any key to exit.");
Console.ReadKey();

record ScanResult(string Name, int Idx, string Status, string Value, string RawType);
