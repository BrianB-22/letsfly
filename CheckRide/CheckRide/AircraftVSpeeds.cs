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
    string Source = ""
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

    public static void Load(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;

        var entries = JsonSerializer.Deserialize<List<AircraftOverrideEntry>>(File.ReadAllText(jsonPath));
        if (entries == null) return;

        _byIcao = entries
            .Where(e => e.VSpeeds != null && !string.IsNullOrWhiteSpace(e.Icao))
            .ToDictionary(
                e => e.Icao,
                e => new VSpeedOverride(e.VSpeeds!.VsoKts, e.VSpeeds.VnoKts, e.VSpeeds.VneKts, e.VSpeeds.VfeKts, e.VSpeeds.Source),
                StringComparer.OrdinalIgnoreCase);
    }

    // Resolves an override from whichever aircraft ICAO is known: the user's manual
    // dropdown pick (most reliable) falling back to a fuzzy match on the live X-Plane
    // aircraft name.
    public static VSpeedOverride? Find(string? selectedIcao, string xplaneAircraftName)
    {
        if (!string.IsNullOrWhiteSpace(selectedIcao) && _byIcao.TryGetValue(selectedIcao, out var byPick))
            return byPick;

        var autoMatch = AircraftDb.AutoMatch(xplaneAircraftName);
        if (autoMatch != null && _byIcao.TryGetValue(autoMatch.IcaoCode, out var byAuto))
            return byAuto;

        return null;
    }
}
