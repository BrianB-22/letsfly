using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheckRide;

// POH-sourced V-speed overrides, keyed by ICAO type. Addon .acf files often report
// incorrect or mislabeled Vno/Vne (e.g. the stock King Air 350's acf_Vno is actually
// its maneuvering speed, not a cruise ceiling) — these values replace the live sim
// dataref for aircraft we've verified against a real POH/AFM. Unmatched aircraft
// keep using the live dataref with a wider safety buffer (see ScoringConfig).
internal record VSpeedOverride(
    double VsoKts,
    double VnoKts,
    double VneKts,
    double VfeKts,
    string Source = "",
    string Xp12Name = ""
);

file record AircraftOverrideEntry(
    [property: JsonPropertyName("xp12Name")] string Xp12Name,
    [property: JsonPropertyName("icao")]     string Icao,
    [property: JsonPropertyName("vSpeeds")]  VSpeedOverrideJson? VSpeeds
);

file record VSpeedOverrideJson(
    [property: JsonPropertyName("vsoKts")] double VsoKts,
    [property: JsonPropertyName("vnoKts")] double VnoKts,
    [property: JsonPropertyName("vneKts")] double VneKts,
    [property: JsonPropertyName("vfeKts")] double VfeKts,
    [property: JsonPropertyName("source")] string Source = ""
);

internal static class AircraftVSpeeds
{
    private static Dictionary<string, VSpeedOverride> _byIcao = new(StringComparer.OrdinalIgnoreCase);
    private static List<VSpeedOverride> _all = new();

    public static void Load(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;

        var entries = JsonSerializer.Deserialize<List<AircraftOverrideEntry>>(File.ReadAllText(jsonPath));
        if (entries == null) return;

        _byIcao = entries
            .Where(e => e.VSpeeds != null && !string.IsNullOrWhiteSpace(e.Icao))
            .ToDictionary(
                e => e.Icao,
                e => new VSpeedOverride(e.VSpeeds!.VsoKts, e.VSpeeds.VnoKts, e.VSpeeds.VneKts, e.VSpeeds.VfeKts, e.VSpeeds.Source, e.Xp12Name),
                StringComparer.OrdinalIgnoreCase);
        _all = _byIcao.Values.ToList();
    }

    // Resolves an override for the currently-loaded aircraft. The live X-Plane aircraft name
    // always wins over a remembered dropdown pick: the pick auto-restores from the last flight
    // and goes stale if the user forgets to change it after switching aircraft (confirmed live
    // 2026-08-22 — a King Air 350 pick silently applied to an X-Crafts ERJ 145 flight). The live
    // name can't go stale like that, so an exact catalog match on it takes priority; the pick is
    // only used as a fallback when nothing in the catalog matches what's actually loaded.
    public static VSpeedOverride? Find(string? selectedIcao, string xplaneAircraftName)
    {
        var byLiveName = FindByLiveName(xplaneAircraftName);
        if (byLiveName != null) return byLiveName;

        if (!string.IsNullOrWhiteSpace(selectedIcao) && _byIcao.TryGetValue(selectedIcao, out var byPick))
            return byPick;

        var autoMatch = AircraftDb.AutoMatch(xplaneAircraftName);
        if (autoMatch != null && _byIcao.TryGetValue(autoMatch.IcaoCode, out var byAuto))
            return byAuto;

        return null;
    }

    private static VSpeedOverride? FindByLiveName(string xplaneAircraftName) =>
        string.IsNullOrWhiteSpace(xplaneAircraftName)
            ? null
            : _all.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.Xp12Name) &&
                                        xplaneAircraftName.Contains(v.Xp12Name, StringComparison.OrdinalIgnoreCase));

    // Diagnostic only — does the dropdown pick's own catalog entry (if it has one) actually
    // match what's loaded live? True means the pick is stale. This can be true even when
    // FindByLiveName above didn't need to step in (e.g. flying a brand-new addon that also
    // isn't catalogued yet, but the old stale pick is) — used to log a warning either way so a
    // silent mismatch never ships without a trace in the log again.
    public static bool SelectedIcaoMismatchesLiveName(string? selectedIcao, string xplaneAircraftName)
    {
        if (string.IsNullOrWhiteSpace(selectedIcao) || string.IsNullOrWhiteSpace(xplaneAircraftName)) return false;
        if (!_byIcao.TryGetValue(selectedIcao, out var pick) || string.IsNullOrWhiteSpace(pick.Xp12Name)) return false;
        return !xplaneAircraftName.Contains(pick.Xp12Name, StringComparison.OrdinalIgnoreCase);
    }
}
