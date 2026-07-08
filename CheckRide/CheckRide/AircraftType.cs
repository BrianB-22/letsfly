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
    string WakeTurbulenceCategory,
    int    NumEngines = 0,
    string ApproachCategory = "",
    double MtowLb = 0
)
{
    public static readonly AircraftType Other = new(
        "OTHER", "Other / Not Listed", "", "", "", 0, 0, 0, "");

    public bool IsOther => IcaoCode == "OTHER";

    // Single Vref value for storage; midpoint when only a min/max range is known
    public double EffectiveVrefKt => ApproachSpeedKt > 0
        ? ApproachSpeedKt
        : ApproachSpeedMinKt > 0 && ApproachSpeedMaxKt > 0
            ? (ApproachSpeedMinKt + ApproachSpeedMaxKt) / 2
            : 0;

    public string InfoLine => IsOther
        ? "⚠  Generic scoring thresholds will be used — results may be less accurate"
        : BuildInfoLine();

    private string BuildInfoLine()
    {
        var parts = new List<string>();
        var engine = EngineDescriptor();
        if (!string.IsNullOrEmpty(engine)) parts.Add(engine);
        if (!string.IsNullOrEmpty(WeightClass)) parts.Add(WeightClass);
        if (ApproachSpeedKt > 0)
            parts.Add($"Vref ~{(int)ApproachSpeedKt} kt");
        else if (ApproachSpeedMinKt > 0 && ApproachSpeedMaxKt > 0)
            parts.Add($"Vref {(int)ApproachSpeedMinKt}–{(int)ApproachSpeedMaxKt} kt");
        if (!string.IsNullOrEmpty(ApproachCategory)) parts.Add($"Cat {ApproachCategory}");
        if (MtowLb > 0) parts.Add($"MTOW {MtowLb:N0} lb");
        if (!string.IsNullOrEmpty(WakeTurbulenceCategory))
            parts.Add($"WTC {WakeTurbulenceCategory}");
        return string.Join("  ·  ", parts);
    }

    private string EngineDescriptor()
    {
        if (string.IsNullOrEmpty(EngineClass)) return "";
        var prefix = NumEngines switch
        {
            1     => "Single ",
            2     => "Twin ",
            3     => "Tri ",
            4     => "Quad ",
            > 4   => $"{NumEngines}-Engine ",
            _     => "",
        };
        return prefix + EngineClass;
    }

    public override string ToString() => IsOther
        ? "⚠  Other / Not Listed — generic thresholds"
        : $"{IcaoCode}  —  {Manufacturer}  {Model}";
}
