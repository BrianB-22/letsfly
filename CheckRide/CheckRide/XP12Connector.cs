using System.Text;
using System.Text.Json;
using CheckRide.Models;

namespace CheckRide;

// X-Plane 12 Web API (v3) — https://developer.x-plane.com/article/x-plane-web-api/
// Step 1: resolve dataref names → numeric IDs via GET /api/v3/datarefs?filter[name]=...
// Step 2: poll values each second via GET /api/v3/datarefs/{id}/value
public class XP12Connector
{
    private const string BaseUrl = "http://localhost:8086";
    private const string Api     = "/api/v3";

    private static readonly string[] Datarefs = new string[]
    {
        "sim/flightmodel/position/latitude",
        "sim/flightmodel/position/longitude",
        "sim/flightmodel/position/groundspeed",         // m/s
        "sim/flightmodel/position/indicated_airspeed",  // m/s
        "sim/flightmodel/position/vh_ind_fpm",
        "sim/flightmodel/position/elevation",           // m MSL
        "sim/flightmodel/position/y_agl",               // m AGL
        "sim/flightmodel/position/alpha",
        "sim/flightmodel/forces/g_nrml",
        "sim/flightmodel2/gear/on_ground",              // int_array → index 0
        "sim/flightmodel2/gear/deploy_ratio",           // float_array → index 0
        "sim/flightmodel/controls/parkbrake",
        "sim/cockpit2/annunciators/stall_warning",  // XP12: was sim/flightmodel/misc/stall_warning
        "sim/flightmodel2/misc/has_crashed",
        "sim/flightmodel/failures/over_vne",
        "sim/flightmodel/controls/flaprat",
        "sim/cockpit/switches/pitot_heat_on",
        "sim/cockpit2/switches/landing_lights_on",
        "sim/cockpit/electrical/beacon_lights_on",
        "sim/aircraft/view/acf_ui_name",               // data type → base64 string
        "sim/flightmodel2/gear/tire_vertical_deflection_mtr", // XP12: replaces tire_sink_depth; float_array [0]=nose [1]=L-main [2]=R-main
        // Attitude
        "sim/flightmodel/position/phi",                // bank angle, deg (+ = right)
        "sim/flightmodel/position/theta",              // pitch angle, deg (+ = nose up)
        "sim/flightmodel/position/mag_psi",            // magnetic heading, deg
        "sim/flightmodel/position/hpath",              // GPS track angle, deg true
        // Lateral / longitudinal forces
        "sim/flightmodel/forces/g_side",               // lateral G
        "sim/flightmodel/forces/g_axil",               // longitudinal G (fore/aft)
        // Angular rates
        "sim/flightmodel/position/P",                  // roll rate, deg/sec
        "sim/flightmodel/position/Q",                  // pitch rate, deg/sec
        "sim/flightmodel/position/R",                  // yaw rate, deg/sec
        // Systems
        "sim/cockpit/radios/transponder_mode",         // 0=off 1=stby 2=on 3=test 4=ALT
        "sim/cockpit2/autopilot/servos_on",             // XP12: autopilot_on=system powered; servos_on=AP actually flying
        "sim/cockpit2/switches/strobe_lights_on",
        "sim/cockpit/switches/anti_ice_on",
        // Engine state
        "sim/flightmodel/engine/ENGN_running",          // int_array; [0]=eng1 [1]=eng2; 1=running
        "sim/cockpit2/engine/indicators/N1_percent",    // float_array; [0]=eng1 [1]=eng2; >100=over-speed
        "sim/cockpit2/engine/indicators/N2_percent",    // float_array; gas generator speed %
        "sim/cockpit2/engine/indicators/ITT_deg_C",     // float_array; inter-turbine temp °C (logged, not scored — aircraft-specific limits)
        // Failure annunciators
        "sim/cockpit2/annunciators/engine_fires",       // int_array; [0]=eng1 [1]=eng2
        "sim/cockpit2/annunciators/oil_pressure",
        "sim/cockpit2/annunciators/fuel_pressure",
        "sim/cockpit2/annunciators/hydraulic_pressure",
        "sim/cockpit2/annunciators/low_voltage",
        "sim/cockpit2/annunciators/master_caution",
        "sim/cockpit2/annunciators/master_warning",
        "sim/flightmodel/failures/frm_ice",             // float; 0=none, >0=ice damage to airframe
        "sim/flightmodel/failures/over_g",              // int; 1=XP12 registered structural over-G
        // OAT
        "sim/weather/aircraft/temperature_ambient_deg_c",
        // Navigation
        "sim/cockpit/radios/nav1_vdef_dot",            // ILS glideslope deviation, dots
        "sim/cockpit/radios/nav1_hdef_dot",            // ILS localizer deviation, dots
        // Weather at aircraft (XP12 moved these to sim/weather/aircraft/ namespace)
        "sim/weather/aircraft/wind_speed_kts",
        "sim/weather/aircraft/wind_direction_degt",
        // Controls
        "sim/cockpit2/engine/actuators/throttle_ratio", // float_array → index 0
        "sim/flightmodel2/controls/speedbrake_ratio",
        "sim/cockpit2/gauges/actuators/barometer_setting_in_hg_pilot",
        // Gear
        // Aircraft performance limits (kias) — XP12 path: sim/aircraft/view/acf_*
        "sim/aircraft/view/acf_Vso",                   // stall speed, landing config
        "sim/aircraft/view/acf_Vno",                   // normal operating speed
        "sim/aircraft/view/acf_Vne",                   // never exceed (may be placeholder — check acf_Vno)
        "sim/aircraft/view/acf_Vfe",                   // max flaps extended
        "sim/aircraft/view/acf_Vs",                    // stall speed, clean config (Vle not found in XP12)
        // Sim state
        "sim/time/paused",                             // 1 = paused, 0 = running
        // Time of day
        "sim/time/local_time_sec",                     // seconds since midnight (local)
        "sim/graphics/scenery/sun_pitch_degrees",      // sun elevation; negative = night
        // Visibility / clouds (best-effort — XP12 regional weather arrays)
        "sim/weather/visibility_reported_m",
        "sim/weather/region/cloud_base_msl_m",         // float_array, index 0 = lowest layer
        "sim/weather/region/cloud_coverage_percent",   // float_array, index 0 = lowest layer, 0–100
        "sim/weather/rain_percent"                     // 0–1 precipitation intensity
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Dictionary<string, long> _ids = new();
    private readonly HashSet<string> _notFound = new();  // datarefs confirmed 404 — never retry
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public Action<string>? Log { get; set; }
    public Action? Connected { get; set; }
    public Action? Disconnected { get; set; }

    public event Action<FlightDataSnapshot>? FlightDataReceived;

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _pollTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private static readonly HttpClient _probeHttp = new() { Timeout = TimeSpan.FromSeconds(3) };

    // Returns true when XP12 has a flight loaded (on ground or airborne)
    public static async Task<bool> ProbeAsync()
    {
        try
        {
            // Always re-resolve by name — never use cached ID (XP12 IDs change on every restart)
            var id = await ResolveProbeIdAsync("sim/flightmodel/position/latitude");
            if (id == 0) return false;

            var json = await _probeHttp.GetStringAsync($"http://localhost:8086/api/v3/datarefs/{id}/value");
            using var doc = JsonDocument.Parse(json);
            var lat = doc.RootElement.GetProperty("data").GetDouble();
            return Math.Abs(lat) > 0.01;
        }
        catch { return false; }
    }

    private static async Task<long> ResolveProbeIdAsync(string name)
    {
        try
        {
            var url  = $"http://localhost:8086/api/v3/datarefs?filter[name]={Uri.EscapeDataString(name)}";
            var json = await _probeHttp.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("data");
            return arr.GetArrayLength() > 0 ? arr[0].GetProperty("id").GetInt64() : 0;
        }
        catch { return 0; }
    }

    private static async Task<int> GetProbeIntAsync(long id)
    {
        var json = await _probeHttp.GetStringAsync($"http://localhost:8086/api/v3/datarefs/{id}/value");
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement.GetProperty("data");
        return d.ValueKind == JsonValueKind.Array ? d[0].GetInt32() : d.GetInt32();
    }

    private static async Task<int> GetProbeIntArrayAsync(long id)
    {
        var json = await _probeHttp.GetStringAsync($"http://localhost:8086/api/v3/datarefs/{id}/value");
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement.GetProperty("data");
        return d.ValueKind == JsonValueKind.Array ? d[0].GetInt32() : d.GetInt32();
    }

    private static async Task<double> GetProbeDoubleAsync(long id)
    {
        var json = await _probeHttp.GetStringAsync($"http://localhost:8086/api/v3/datarefs/{id}/value");
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement.GetProperty("data");
        return d.ValueKind == JsonValueKind.Array ? d[0].GetDouble() : d.GetDouble();
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_pollTask is not null)
        {
            try { await _pollTask; } catch { }
            _pollTask = null;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        Log?.Invoke("Connector started — waiting for XP12…");

        // Phase 1: wait silently until the API responds at all
        while (!ct.IsCancellationRequested)
        {
            if (await IsApiReachableAsync(ct)) break;
            try { await Task.Delay(5000, ct); } catch { return; }
        }

        // Phase 2: resolve dataref IDs — XP12 is running, flight may still be loading
        Log?.Invoke("XP12 detected — resolving dataref IDs…");
        while (!ct.IsCancellationRequested)
        {
            // Phase 2: never blacklist 404s — sim may still be loading, any 404 is temporary
            await ResolveIdsAsync(ct, blacklist404s: false);
            Log?.Invoke($"ID resolution: {_ids.Count}/{Datarefs.Length} resolved");
            if (_ids.ContainsKey("sim/flightmodel/position/latitude")) break;
            Log?.Invoke("Flight not loaded yet — retrying in 3s…");
            try { await Task.Delay(3000, ct); } catch { return; }
        }

        Connected?.Invoke();

        // Phase 3: poll snapshots — fill in any IDs still missing from partial resolution
        int nullStreak = 0;
        const int MaxNullStreak = 10; // ~10s of no data = sim gone

        while (!ct.IsCancellationRequested)
        {
            if (_ids.Count + _notFound.Count < Datarefs.Length)
                await ResolveIdsAsync(ct);

            try
            {
                var snap = await BuildSnapshotAsync(ct);
                if (snap is not null)
                {
                    nullStreak = 0;
                    FlightDataReceived?.Invoke(snap);
                }
                else
                {
                    nullStreak++;
                    Log?.Invoke($"No data from XP12 ({nullStreak}/{MaxNullStreak})…");
                    if (nullStreak >= MaxNullStreak)
                    {
                        Log?.Invoke("XP12 disconnected — ending session.");
                        Disconnected?.Invoke();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }

            try { await Task.Delay(1000, ct); } catch { break; }
        }
    }

    // Quick check — any HTTP response (even 404) means XP12 is running
    private async Task<bool> IsApiReachableAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}{Api}/datarefs?filter[name]={Uri.EscapeDataString("sim/flightmodel/position/latitude")}";
            await _http.GetAsync(url, ct);
            return true;
        }
        catch { return false; }
    }

    private async Task ResolveIdsAsync(CancellationToken ct, bool blacklist404s = true)
    {
        foreach (var name in Datarefs)
        {
            if (_ids.ContainsKey(name) || _notFound.Contains(name)) continue;
            try
            {
                var url = $"{BaseUrl}{Api}/datarefs?filter[name]={Uri.EscapeDataString(name)}";
                var json = await _http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement.GetProperty("data");
                if (arr.GetArrayLength() == 0)
                {
                    // Empty = sim still loading — retry next cycle
                    continue;
                }
                foreach (var item in arr.EnumerateArray())
                {
                    var id   = item.GetProperty("id").GetInt64();
                    var n    = item.GetProperty("name").GetString() ?? "";
                    if (!string.IsNullOrEmpty(n)) _ids[n] = id;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                if (blacklist404s)
                {
                    _notFound.Add(name);
                    Log?.Invoke($"[XP12] Dataref not found: '{name}' (suppressing future retries)");
                }
                // else: phase 2 — sim still loading, skip silently and retry next cycle
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[XP12] Resolve error for '{name}': {ex.GetType().Name} — {ex.Message}");
                break; // one error per cycle is enough noise — retry whole batch next tick
            }
        }
    }

    private async Task<FlightDataSnapshot?> BuildSnapshotAsync(CancellationToken ct)
    {
        // Position / speed
        var latTask   = D("sim/flightmodel/position/latitude", ct);
        var lonTask   = D("sim/flightmodel/position/longitude", ct);
        var gsTask    = D("sim/flightmodel/position/groundspeed", ct);
        var iasTask   = D("sim/flightmodel/position/indicated_airspeed", ct);
        var vsTask    = D("sim/flightmodel/position/vh_ind_fpm", ct);
        var mslTask   = D("sim/flightmodel/position/elevation", ct);
        var aglTask   = D("sim/flightmodel/position/y_agl", ct);
        // Attitude
        var aoaTask   = D("sim/flightmodel/position/alpha", ct);
        var bankTask  = D("sim/flightmodel/position/phi", ct);
        var pitchTask = D("sim/flightmodel/position/theta", ct);
        var hdgTask   = D("sim/flightmodel/position/mag_psi", ct);
        var trkTask   = D("sim/flightmodel/position/hpath", ct);
        // Forces
        var gnTask    = D("sim/flightmodel/forces/g_nrml", ct);
        var glTask    = D("sim/flightmodel/forces/g_side", ct);
        var gaTask    = D("sim/flightmodel/forces/g_axil", ct);
        // Angular rates
        var rollRTask = D("sim/flightmodel/position/P", ct);
        var pitRTask  = D("sim/flightmodel/position/Q", ct);
        var yawRTask  = D("sim/flightmodel/position/R", ct);
        // Gear / ground
        var ogTask    = D("sim/flightmodel2/gear/on_ground", ct, index: 0);
        var gearTask  = D("sim/flightmodel2/gear/deploy_ratio", ct, index: 0);
        var sinkTask  = D("sim/flightmodel2/gear/tire_vertical_deflection_mtr", ct, index: 1);
        // Controls
        var pbTask    = D("sim/flightmodel/controls/parkbrake", ct);
        var flapTask  = D("sim/flightmodel/controls/flaprat", ct);
        var sbrakeTask= D("sim/flightmodel2/controls/speedbrake_ratio", ct);
        var thrTask   = D("sim/cockpit2/engine/actuators/throttle_ratio", ct, index: 0);
        // Flags
        var stallTask = D("sim/cockpit2/annunciators/stall_warning", ct);
        var crashTask = D("sim/flightmodel2/misc/has_crashed", ct);
        var ovTask    = D("sim/flightmodel/failures/over_vne", ct);
        // Systems
        var pitotTask    = D("sim/cockpit/switches/pitot_heat_on", ct);
        var llTask       = D("sim/cockpit2/switches/landing_lights_on", ct);
        var beacTask     = D("sim/cockpit/electrical/beacon_lights_on", ct);
        var strbTask     = D("sim/cockpit2/switches/strobe_lights_on", ct);
        var xpdrTask     = D("sim/cockpit/radios/transponder_mode", ct);
        var apTask       = D("sim/cockpit2/autopilot/servos_on", ct);
        var antiIceTask  = D("sim/cockpit/switches/anti_ice_on", ct);
        var baroTask     = D("sim/cockpit2/gauges/actuators/barometer_setting_in_hg_pilot", ct);
        // Engine state
        var eng1Task     = D("sim/flightmodel/engine/ENGN_running", ct, index: 0);
        var eng2Task     = D("sim/flightmodel/engine/ENGN_running", ct, index: 1);
        var n1Eng1Task   = D("sim/cockpit2/engine/indicators/N1_percent", ct, index: 0);
        var n1Eng2Task   = D("sim/cockpit2/engine/indicators/N1_percent", ct, index: 1);
        var n2Eng1Task   = D("sim/cockpit2/engine/indicators/N2_percent", ct, index: 0);
        var n2Eng2Task   = D("sim/cockpit2/engine/indicators/N2_percent", ct, index: 1);
        var ittEng1Task  = D("sim/cockpit2/engine/indicators/ITT_deg_C", ct, index: 0);
        var ittEng2Task  = D("sim/cockpit2/engine/indicators/ITT_deg_C", ct, index: 1);
        // Failure annunciators
        var engFireTask  = D("sim/cockpit2/annunciators/engine_fires", ct, index: 0);
        var engFire2Task = D("sim/cockpit2/annunciators/engine_fires", ct, index: 1);
        var oilTask      = D("sim/cockpit2/annunciators/oil_pressure", ct);
        var fuelPrsTask  = D("sim/cockpit2/annunciators/fuel_pressure", ct);
        var hydTask      = D("sim/cockpit2/annunciators/hydraulic_pressure", ct);
        var voltTask     = D("sim/cockpit2/annunciators/low_voltage", ct);
        var cautTask     = D("sim/cockpit2/annunciators/master_caution", ct);
        var warnTask     = D("sim/cockpit2/annunciators/master_warning", ct);
        var iceTask      = D("sim/flightmodel/failures/frm_ice", ct);
        var overGTask    = D("sim/flightmodel/failures/over_g", ct);
        var oatTask      = D("sim/weather/aircraft/temperature_ambient_deg_c", ct);
        // Navigation
        var gsDevTask = D("sim/cockpit/radios/nav1_vdef_dot", ct);
        var locDevTask= D("sim/cockpit/radios/nav1_hdef_dot", ct);
        // Weather
        var windSpdTask= D("sim/weather/aircraft/wind_speed_kts", ct);
        var windDirTask= D("sim/weather/aircraft/wind_direction_degt", ct);
        // Aircraft performance limits (XP12: sim/aircraft/view/acf_* not sim/aircraft/limits/*)
        var vsoTask   = D("sim/aircraft/view/acf_Vso", ct);
        var vnoTask   = D("sim/aircraft/view/acf_Vno", ct);
        var vneTask   = D("sim/aircraft/view/acf_Vne", ct);
        var vfeTask   = D("sim/aircraft/view/acf_Vfe", ct);
        var vleTask   = D("sim/aircraft/view/acf_Vs",  ct); // Vle not found; using Vs (clean stall) as fallback
        // Aircraft name
        var acfTask    = S("sim/aircraft/view/acf_ui_name", ct);
        // Sim state
        var pausedTask = I("sim/time/paused", ct);
        // Time of day / conditions
        var localTimeTask   = D("sim/time/local_time_sec", ct);
        var sunPitchTask    = D("sim/graphics/scenery/sun_pitch_degrees", ct);
        var visTask         = D("sim/weather/visibility_reported_m", ct);
        var cloudBaseTask   = D("sim/weather/region/cloud_base_msl_m", ct, index: 0);
        var cloudCovTask    = D("sim/weather/region/cloud_coverage_percent", ct, index: 0);
        var rainTask        = D("sim/weather/rain_percent", ct);

        await Task.WhenAll(
            latTask, lonTask, gsTask, iasTask, vsTask, mslTask, aglTask,
            aoaTask, bankTask, pitchTask, hdgTask, trkTask,
            gnTask, glTask, gaTask,
            rollRTask, pitRTask, yawRTask,
            ogTask, gearTask, sinkTask,
            pbTask, flapTask, sbrakeTask, thrTask,
            stallTask, crashTask, ovTask,
            pitotTask, llTask, beacTask, strbTask, xpdrTask, apTask,
            antiIceTask, baroTask, eng1Task, eng2Task,
            n1Eng1Task, n1Eng2Task, n2Eng1Task, n2Eng2Task, ittEng1Task, ittEng2Task,
            engFireTask, engFire2Task, oilTask, fuelPrsTask, hydTask, voltTask, cautTask, warnTask,
            iceTask, overGTask, oatTask,
            gsDevTask, locDevTask,
            windSpdTask, windDirTask,
            vsoTask, vnoTask, vneTask, vfeTask, vleTask,
            acfTask, pausedTask,
            localTimeTask, sunPitchTask, visTask, cloudBaseTask, cloudCovTask, rainTask);

        if (latTask.Result == 0 && lonTask.Result == 0) return null;

        return new FlightDataSnapshot
        {
            Latitude             = latTask.Result,
            Longitude            = lonTask.Result,
            AltitudeMslFt        = MetersToFeet(mslTask.Result),
            AltitudeAglFt        = MetersToFeet(aglTask.Result),
            GroundspeedKts       = MsToKts(gsTask.Result),
            IndicatedAirspeedKts = Math.Max(0, iasTask.Result), // clamp: headwind can make IAS negative on ground
            VerticalSpeedFpm     = vsTask.Result,
            AngleOfAttackDeg     = aoaTask.Result,
            BankAngleDeg         = bankTask.Result,
            PitchAngleDeg        = pitchTask.Result,
            MagHeadingDeg        = hdgTask.Result,
            GpsTrackDeg          = trkTask.Result,
            GForceNormal         = gnTask.Result,
            GForceLateral        = glTask.Result,
            GForceAxial          = gaTask.Result,
            RollRateDegSec       = rollRTask.Result,
            PitchRateDegSec      = pitRTask.Result,
            YawRateDegSec        = yawRTask.Result,
            OnGround             = ogTask.Result > 0.5,
            GearDeployRatio      = gearTask.Result,
            TireSinkDepthM       = sinkTask.Result,
            ParkingBrakeSet      = pbTask.Result > 0.5,
            FlapRatio            = flapTask.Result,
            SpeedbrakeRatio      = sbrakeTask.Result,
            ThrottleRatio        = thrTask.Result,
            StallWarning         = stallTask.Result,
            HasCrashed           = crashTask.Result > 0.5,
            Overspeed            = ovTask.Result > 0.5,
            PitotHeatOn          = pitotTask.Result > 0.5,
            LandingLightsOn      = llTask.Result > 0.5,
            BeaconOn             = beacTask.Result > 0.5,
            StrobeLightsOn       = strbTask.Result > 0.5,
            TransponderMode      = (int)xpdrTask.Result,
            AutopilotOn          = apTask.Result > 0.5,
            AntiIceOn            = antiIceTask.Result > 0.5,
            BarometerInHg        = baroTask.Result,
            Engine1Running       = eng1Task.Result > 0.5,
            Engine2Running       = eng2Task.Result > 0.5,
            Eng1N1Pct            = n1Eng1Task.Result,
            Eng2N1Pct            = n1Eng2Task.Result,
            Eng1N2Pct            = n2Eng1Task.Result,
            Eng2N2Pct            = n2Eng2Task.Result,
            Eng1IttC             = ittEng1Task.Result,
            Eng2IttC             = ittEng2Task.Result,
            EngineFire           = engFireTask.Result > 0.5 || engFire2Task.Result > 0.5,
            OilPressureLow       = oilTask.Result > 0.5,
            FuelPressureLow      = fuelPrsTask.Result > 0.5,
            HydraulicPressureLow = hydTask.Result > 0.5,
            LowVoltage           = voltTask.Result > 0.5,
            MasterCaution        = cautTask.Result > 0.5,
            MasterWarning        = warnTask.Result > 0.5,
            IceDamage            = iceTask.Result,
            OverGFailure         = overGTask.Result > 0.5,
            OutsideAirTempC      = oatTask.Result,
            GlideslopeDevDots    = gsDevTask.Result,
            LocalizerDevDots     = locDevTask.Result,
            WindSpeedKt          = windSpdTask.Result,
            WindDirectionDeg     = windDirTask.Result,
            VsoKts               = vsoTask.Result,
            VnoKts               = vnoTask.Result,
            VneKts               = vneTask.Result,
            VfeKts               = vfeTask.Result,
            VleKts               = vleTask.Result,
            AircraftName         = acfTask.Result,
            IsSimPaused          = pausedTask.Result != 0,
            LocalTimeSec         = localTimeTask.Result,
            SunPitchDeg          = sunPitchTask.Result,
            VisibilityM          = visTask.Result,
            CloudBaseAglM        = cloudBaseTask.Result,
            CloudCoverage        = cloudCovTask.Result,
            RainPercent          = rainTask.Result,
            Timestamp            = DateTime.UtcNow
        };
    }

    // Fetch a scalar or array-element dataref value by name
    private Task<double> D(string name, CancellationToken ct, int? index = null) =>
        _ids.TryGetValue(name, out var id)
            ? GetDoubleByIdAsync(id, index, ct)
            : Task.FromResult(0.0);

    // Fetch an int dataref by name
    private Task<int> I(string name, CancellationToken ct) =>
        _ids.TryGetValue(name, out var id)
            ? GetIntByIdAsync(id, ct)
            : Task.FromResult(0);

    // Fetch a string (data-type) dataref by name
    private Task<string> S(string name, CancellationToken ct) =>
        _ids.TryGetValue(name, out var id)
            ? GetStringByIdAsync(id, ct)
            : Task.FromResult("");

    private async Task<int> GetIntByIdAsync(long id, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}{Api}/datarefs/{id}/value";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)  return data[0].GetInt32();
            if (data.ValueKind == JsonValueKind.Number) return data.GetInt32();
            return 0;
        }
        catch { return 0; }
    }

    private async Task<double> GetDoubleByIdAsync(long id, int? index, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}{Api}/datarefs/{id}/value";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)
                return data[(index ?? 0)].GetDouble();
            if (data.ValueKind == JsonValueKind.Number)
                return data.GetDouble();
            return 0;
        }
        catch { return 0; }
    }

    private async Task<string> GetStringByIdAsync(long id, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}{Api}/datarefs/{id}/value";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.ValueKind != JsonValueKind.String) return "";

            var raw = data.GetString() ?? "";
            try
            {
                var bytes = Convert.FromBase64String(raw);
                return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            }
            catch
            {
                return raw; // not base64 — return as-is
            }
        }
        catch { return ""; }
    }

    private static double MsToKts(double ms) => ms * 1.94384;
    private static double MetersToFeet(double m) => m * 3.28084;
}
