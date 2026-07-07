namespace CheckRide;

internal record AircraftType(
    string IcaoCode,
    string Manufacturer,
    string Model,
    string EngineClass,
    string WeightClass,
    double ApproachSpeedKt,
    double ApproachSpeedMinKt,
    double ApproachSpeedMaxKt,
    string WakeTurbulenceCategory
)
{
    public static readonly AircraftType Other = new(
        "OTHER", "Other / Not Listed", "", "", "", 0, 0, 0, "");

    public bool IsOther => IcaoCode == "OTHER";

    public string InfoLine => IsOther
        ? "⚠  Generic scoring thresholds will be used — results may be less accurate"
        : BuildInfoLine();

    private string BuildInfoLine()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(EngineClass)) parts.Add(EngineClass);
        if (!string.IsNullOrEmpty(WeightClass)) parts.Add(WeightClass);
        if (ApproachSpeedKt > 0)
            parts.Add($"Vref ~{(int)ApproachSpeedKt} kt");
        else if (ApproachSpeedMinKt > 0 && ApproachSpeedMaxKt > 0)
            parts.Add($"Vref {(int)ApproachSpeedMinKt}–{(int)ApproachSpeedMaxKt} kt");
        if (!string.IsNullOrEmpty(WakeTurbulenceCategory))
            parts.Add($"WTC {WakeTurbulenceCategory}");
        return string.Join("  ·  ", parts);
    }

    public override string ToString() => IsOther
        ? "⚠  Other / Not Listed — generic thresholds"
        : $"{IcaoCode}  —  {Manufacturer}  {Model}";
}
