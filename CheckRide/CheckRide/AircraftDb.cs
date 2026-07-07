using System.Globalization;

namespace CheckRide;

internal static class AircraftDb
{
    private static List<AircraftType> _all = new();

    public static void Load(string csvPath)
    {
        if (!File.Exists(csvPath)) return;
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2) return;

        var headers = ParseRow(lines[0]);
        int Col(string name) => Array.IndexOf(headers, name);

        int icaoIdx    = Col("ICAO_Code");
        int mfgIdx     = Col("Manufacturer");
        int modelIdx   = Col("Model_FAA");
        int engIdx     = Col("Physical_Class_Engine");
        int wtIdx      = Col("FAA_Weight");
        int vrefIdx    = Col("Approach_Speed_knot");
        int vrefMinIdx = Col("Approach_Speed_minimum_knot");
        int vrefMaxIdx = Col("Approach_Speed_maximum_knot");
        int wtcIdx     = Col("ICAO_WTC");

        var list = new List<AircraftType>(lines.Length);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseRow(line);
            if (icaoIdx >= cols.Length) continue;
            var icao = cols[icaoIdx].Trim();
            if (string.IsNullOrEmpty(icao)) continue;

            string S(int idx) => idx >= 0 && idx < cols.Length ? cols[idx].Trim() : "";
            double V(int idx) =>
                idx >= 0 && idx < cols.Length &&
                double.TryParse(cols[idx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                ? v : 0;

            list.Add(new AircraftType(
                IcaoCode:               icao,
                Manufacturer:           S(mfgIdx),
                Model:                  S(modelIdx),
                EngineClass:            S(engIdx),
                WeightClass:            S(wtIdx),
                ApproachSpeedKt:        V(vrefIdx),
                ApproachSpeedMinKt:     V(vrefMinIdx),
                ApproachSpeedMaxKt:     V(vrefMaxIdx),
                WakeTurbulenceCategory: S(wtcIdx)
            ));
        }
        _all = list;
    }

    // Returns up to 30 matches; empty query returns first 50.
    public static IEnumerable<AircraftType> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _all.Take(50);
        return _all
            .Where(a =>
                a.IcaoCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Manufacturer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Model.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30);
    }

    // Attempts to match an X-Plane aircraft name string to a known ICAO type.
    public static AircraftType? AutoMatch(string xplaneName)
    {
        if (string.IsNullOrWhiteSpace(xplaneName) || _all.Count == 0) return null;

        var byCode = _all.FirstOrDefault(a =>
            xplaneName.Contains(a.IcaoCode, StringComparison.OrdinalIgnoreCase));
        if (byCode != null) return byCode;

        return _all.FirstOrDefault(a =>
            !string.IsNullOrEmpty(a.Manufacturer) &&
            xplaneName.Contains(a.Manufacturer, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(a.Model) &&
            xplaneName.Contains(a.Model.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
    }

    private static string[] ParseRow(string line)
    {
        var result  = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQ    = false;
        foreach (char c in line)
        {
            if      (c == '"')          inQ = !inQ;
            else if (c == ',' && !inQ) { result.Add(current.ToString()); current.Clear(); }
            else                        current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
