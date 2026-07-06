using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CheckRide.Models;

namespace CheckRide;

internal class SupabaseClient
{
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _readOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions _enumOpts = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private SupabaseSession _session;
    public  SupabaseSession Session => _session;

    public SupabaseClient(SupabaseSession session) => _session = session;

    // ── Auth ─────────────────────────────────────────────────────────────────

    public static async Task<SupabaseSession> LoginAsync(string email, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{Config.SupabaseUrl}/auth/v1/token?grant_type=password");
        req.Headers.Add("apikey", Config.SupabaseAnonKey);
        req.Content = JsonContent.Create(new { email, password });

        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception(TryParseError(body) ?? $"Login failed ({(int)resp.StatusCode})");

        var data = JsonSerializer.Deserialize<AuthResponse>(body, _readOpts)
                   ?? throw new Exception("Unexpected response from auth server.");

        return new SupabaseSession
        {
            AccessToken  = data.AccessToken,
            RefreshToken = data.RefreshToken,
            UserId       = data.User.Id,
            Email        = data.User.Email,
            ExpiresAt    = DateTime.UtcNow.AddSeconds(data.ExpiresIn),
        };
    }

    // Calls the verify-client edge function after login.
    // Currently always passes — add subscription/version gating server-side without touching this code.
    public static async Task VerifyClientAsync(string accessToken)
    {
        var clientVersion = System.Reflection.Assembly.GetExecutingAssembly()
                                .GetName().Version?.ToString() ?? "unknown";

        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{Config.SupabaseUrl}/functions/v1/verify-client");
        req.Headers.Add("apikey",         Config.SupabaseAnonKey);
        req.Headers.Add("Authorization",  $"Bearer {accessToken}");
        req.Content = JsonContent.Create(new { client_version = clientVersion });

        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<VerifyClientResponse>(body, _readOpts);
        if (result is not null && !result.Allowed)
            throw new Exception(result.Message ?? "Access denied. Please check your subscription at simletsfly.com.");

        TrackEvent("checkride_login");
    }

    public async Task SignOutAsync()
    {
        try
        {
            var req = AuthPost($"{Config.SupabaseUrl}/auth/v1/logout");
            await _http.SendAsync(req);
        }
        catch { }
        SessionStore.Clear();
    }

    // Serialize refreshes: Supabase rotates refresh tokens, so two concurrent
    // callers (e.g. GetFlightsAsync + GetLastScoresAsync via Task.WhenAll) firing
    // the same refresh token would invalidate the session for the loser.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private async Task EnsureTokenAsync()
    {
        if (!_session.IsExpired) return;

        await _refreshLock.WaitAsync();
        try
        {
            // Another caller may have refreshed while we waited
            if (!_session.IsExpired) return;

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{Config.SupabaseUrl}/auth/v1/token?grant_type=refresh_token");
            req.Headers.Add("apikey", Config.SupabaseAnonKey);
            req.Content = JsonContent.Create(new { refresh_token = _session.RefreshToken });

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Session expired — please sign in again.");

            var body = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<AuthResponse>(body, _readOpts)!;
            _session.AccessToken  = data.AccessToken;
            _session.RefreshToken = data.RefreshToken;
            _session.ExpiresAt    = DateTime.UtcNow.AddSeconds(data.ExpiresIn);
            SessionStore.Save(_session);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpRequestMessage AuthGet(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("apikey",         Config.SupabaseAnonKey);
        req.Headers.Add("Authorization",  $"Bearer {_session.AccessToken}");
        return req;
    }

    private HttpRequestMessage AuthPost(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("apikey",        Config.SupabaseAnonKey);
        req.Headers.Add("Authorization", $"Bearer {_session.AccessToken}");
        return req;
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    public async Task<List<SavedFlight>> GetFlightsAsync()
    {
        await EnsureTokenAsync();
        var url = $"{Config.SupabaseUrl}/rest/v1/saved_flights" +
                  "?select=id,dep_id,dep_name,dep_lat,dep_lon,arr_id,arr_name,arr_lat,arr_lon,created_at" +
                  $"&user_id=eq.{_session.UserId}" +
                  "&order=created_at.desc";
        var resp = await _http.SendAsync(AuthGet(url));
        resp.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<List<SavedFlight>>(
                   await resp.Content.ReadAsStringAsync(), _readOpts) ?? new();
    }

    // Returns latest (score, grade, resultId) per flight_id — used to show last run in the flight list
    public async Task<Dictionary<string, (int Score, string Grade, string ResultId)>> GetLastScoresAsync()
    {
        await EnsureTokenAsync();
        var url = $"{Config.SupabaseUrl}/rest/v1/checkride_results" +
                  "?select=id,flight_id,score,grade" +
                  $"&user_id=eq.{_session.UserId}" +
                  "&order=recorded_at.desc";
        var resp = await _http.SendAsync(AuthGet(url));
        resp.EnsureSuccessStatusCode();
        var rows = JsonSerializer.Deserialize<List<ScoreRow>>(
                       await resp.Content.ReadAsStringAsync(), _readOpts) ?? new();

        var dict = new Dictionary<string, (int, string, string)>();
        foreach (var r in rows)
            if (!dict.ContainsKey(r.FlightId))
                dict[r.FlightId] = (r.Score, r.Grade, r.Id);
        return dict;
    }

    public async Task UploadCheckRideAsync(string flightId, CheckRideReport report, string? logPath)
    {
        await EnsureTokenAsync();

        string? logText = null;
        if (logPath is not null && File.Exists(logPath))
        {
            try { logText = await File.ReadAllTextAsync(logPath); } catch { }
        }

        var payload = new Dictionary<string, object?>
        {
            ["flight_id"]        = flightId,
            ["user_id"]          = _session.UserId,
            ["score"]            = report.Score,
            ["grade"]            = report.Grade,
            ["aircraft"]         = report.Aircraft,
            ["sim"]              = report.Sim,
            ["scoring_version"]  = report.ScoringVersion,
            ["recorded_at"]      = report.RecordedAt,
            // Flat convenience columns
            ["distance_nm"]      = report.Stats.DistanceNm > 0 ? (object?)Math.Round(report.Stats.DistanceNm, 1) : null,
            ["flight_time_sec"]  = report.Stats.FlightTimeSec > 0 ? report.Stats.FlightTimeSec : (int?)null,
            ["landing_quality"]  = string.IsNullOrEmpty(report.Summary.LandingQuality) ? null : report.Summary.LandingQuality,
            ["crashed"]          = report.Summary.Crashed,
            ["imc_flight"]       = report.Summary.ImcFlight,
            ["log_text"]         = logText,
            // Full JSONB blobs
            ["events"]           = JsonSerializer.SerializeToElement(report.Events,     _enumOpts),
            ["summary"]          = JsonSerializer.SerializeToElement(report.Summary,    _enumOpts),
            ["stats"]            = JsonSerializer.SerializeToElement(report.Stats,      _enumOpts),
            ["breakdown"]        = JsonSerializer.SerializeToElement(report.Breakdown,  _enumOpts),
            ["track"]            = JsonSerializer.SerializeToElement(report.Track,      _enumOpts),
            ["conditions"]       = JsonSerializer.SerializeToElement(report.Conditions, _enumOpts),
        };

        var req = AuthPost($"{Config.SupabaseUrl}/rest/v1/checkride_results");
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = JsonContent.Create(payload);

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Upload failed: {body}");
        }

        TrackEvent("checkride_upload");
    }

    // ── Analytics ────────────────────────────────────────────────────────────

    // Fire-and-forget; mirrors the website's _sb.rpc('increment_page_view', { page_name })
    public static void TrackEvent(string pageName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"{Config.SupabaseUrl}/rest/v1/rpc/increment_page_view");
                req.Headers.Add("apikey", Config.SupabaseAnonKey);
                req.Content = JsonContent.Create(new { page_name = pageName });
                await _http.SendAsync(req);
            }
            catch { /* analytics must never crash the app */ }
        });
    }

    // ── Error parsing ─────────────────────────────────────────────────────────

    private static string? TryParseError(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var d) && d.GetString() is { } desc) return desc;
            if (doc.RootElement.TryGetProperty("message",           out var m) && m.GetString() is { } msg)  return msg;
        }
        catch { }
        return null;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private record AuthResponse(
        [property: JsonPropertyName("access_token")]  string   AccessToken,
        [property: JsonPropertyName("refresh_token")] string   RefreshToken,
        [property: JsonPropertyName("expires_in")]    int      ExpiresIn,
        [property: JsonPropertyName("user")]          AuthUser User);

    private record AuthUser(
        [property: JsonPropertyName("id")]    string Id,
        [property: JsonPropertyName("email")] string Email);

    private record ScoreRow(
        [property: JsonPropertyName("id")]        string Id,
        [property: JsonPropertyName("flight_id")] string FlightId,
        [property: JsonPropertyName("score")]     int    Score,
        [property: JsonPropertyName("grade")]     string Grade);

    private record VerifyClientResponse(
        [property: JsonPropertyName("allowed")]  bool    Allowed,
        [property: JsonPropertyName("message")]  string? Message);
}
