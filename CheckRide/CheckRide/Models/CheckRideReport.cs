namespace CheckRide.Models;

public class FlightConditionSample
{
    public int T { get; set; }                // seconds from recording start
    public int LocalTimeSec { get; set; }      // sim local time, seconds since midnight
    public bool IsNight { get; set; }          // sun below horizon
    public double WindSpeedKt { get; set; }
    public double WindDirectionDeg { get; set; }
    public double VisibilityM { get; set; }    // 0 if not available from sim
    public double CloudBaseAglFt { get; set; } // 0 if not available
    public double CloudCoverage { get; set; }  // 0–1 fraction, 0 if not available
    public double OatC { get; set; }           // outside air temp °C; 0 to -20 + cloud = icing risk
    public int AltMslFt { get; set; }
}

public class FlightTrackPoint
{
    public int T { get; set; }          // seconds from recording start
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int AltMslFt { get; set; }
    public int AltAglFt { get; set; }
    public int IasKts { get; set; }
    public int GsKts { get; set; }
    public int VsFpm { get; set; }
    public int HdgDeg { get; set; }
    public string Phase { get; set; } = "";
}

public class CheckRideSummary
{
    public int OverspeedCount { get; set; }
    public int StallCount { get; set; }
    public int HighGCount { get; set; }
    public int SystemFlags { get; set; }
    public int TakeoffFlags { get; set; }
    public int TaxiFlags { get; set; }
    public int FailureCount { get; set; }
    public bool Crashed { get; set; }
    public bool RunwayExcursion { get; set; }
    public bool ImcFlight { get; set; }
    public bool NightFlight { get; set; }
    public double CrosswindAtLandingKt { get; set; }
    public string LandingQuality { get; set; } = "Unknown";
}

public class FlightStats
{
    // Distance
    public double DistanceNm { get; set; }

    // Time
    public int FlightTimeSec { get; set; }
    public int AutopilotTimeSec { get; set; }
    public double AutopilotPct { get; set; }        // % of airborne time with AP engaged

    // Altitude
    public double MaxAltitudeMslFt { get; set; }
    public double MaxAltitudeAglFt { get; set; }

    // Speed
    public double MaxIasKts { get; set; }

    // Attitude / forces
    public double MaxBankAngleDeg { get; set; }
    public double MaxGForceNormal { get; set; }
    public double MaxGForceLateral { get; set; }

    // Approach
    public double AvgFinalDescentRateFpm { get; set; }  // average VS below 1,500ft AGL descending

    // Landing
    public double LandingVsFpm { get; set; }
    public double LandingLateralG { get; set; }
    public double WindSpeedAtLandingKt { get; set; }
    public double WindDirectionAtLandingDeg { get; set; }
    public double CrosswindAtLandingKt { get; set; }

    // Night tracking
    public double NightFlightPct { get; set; }          // % of airborne time at night

    // Fuel
    public double FuelAtTakeoffKg { get; set; }
    public double FuelAtLandingKg { get; set; }
    public double FuelBurnedKg    { get; set; }

    // Aircraft limits used for scoring (kias)
    public double VsoKts { get; set; }
    public double VrefKts { get; set; }   // 1.3 × Vso
    public double VnoKts { get; set; }
    public double VneKts { get; set; }
    public double VfeKts { get; set; }
    public double VsCleanKts { get; set; }  // clean-config stall speed (XP12 exposes no Vle)
}

public class ScoreLineItem
{
    public string Label { get; init; } = "";
    public int    Count { get; init; } = 1;  // >1 only for per-occurrence items
    public int    Pts   { get; init; }        // negative = deduction, positive = bonus
}

public class CheckRideReport
{
    public const string ScoringVersionConst = "xp12-1.6";

    public string Sim { get; init; } = "xplane12";
    public string ScoringVersion { get; init; } = ScoringVersionConst;
    public string Aircraft { get; set; } = "";
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
    public int Score { get; set; }
    public string Grade { get; set; } = "";
    public List<FlightEvent> Events { get; set; } = new();
    public CheckRideSummary Summary { get; set; } = new();
    public FlightStats Stats { get; set; } = new();
    public List<ScoreLineItem> Breakdown { get; set; } = new();
    public List<FlightConditionSample> Conditions { get; set; } = new();
    public List<FlightTrackPoint> Track { get; set; } = new();
}
