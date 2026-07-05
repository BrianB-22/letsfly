namespace CheckRide.Models;

public class FlightDataSnapshot
{
    // Position
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeMslFt { get; init; }
    public double AltitudeAglFt { get; init; }

    // Speed / vertical
    public double GroundspeedKts { get; init; }
    public double IndicatedAirspeedKts { get; init; }
    public double VerticalSpeedFpm { get; init; }

    // Attitude
    public double BankAngleDeg { get; init; }       // + = right bank
    public double PitchAngleDeg { get; init; }      // + = nose up
    public double MagHeadingDeg { get; init; }
    public double GpsTrackDeg { get; init; }        // actual path over ground
    public double AngleOfAttackDeg { get; init; }

    // Forces
    public double GForceNormal { get; init; }       // vertical
    public double GForceLateral { get; init; }      // side — crooked landings
    public double GForceAxial { get; init; }        // fore/aft — braking / acceleration

    // Rates
    public double RollRateDegSec { get; init; }
    public double PitchRateDegSec { get; init; }
    public double YawRateDegSec { get; init; }

    // Gear / ground
    public bool OnGround { get; init; }
    public double GearDeployRatio { get; init; }
    public double TireSinkDepthM { get; init; }     // main gear sink into surface, m; >0.05 = off pavement


    // Controls
    public double FlapRatio { get; init; }
    public double SpeedbrakeRatio { get; init; }
    public double ThrottleRatio { get; init; }
    public bool ParkingBrakeSet { get; init; }

    // Systems
    public bool PitotHeatOn { get; init; }
    public bool LandingLightsOn { get; init; }
    public bool BeaconOn { get; init; }
    public bool StrobeLightsOn { get; init; }
    public int TransponderMode { get; init; }       // 0=off 1=stby 2=on 3=test 4=ALT
    public bool AutopilotOn { get; init; }
    public bool AntiIceOn { get; init; }
    public double BarometerInHg { get; init; }

    // Engine state
    public bool Engine1Running { get; init; }
    public bool Engine2Running { get; init; }
    public double Eng1N1Pct { get; init; }    // N1% — universal: >100 = over-speed regardless of aircraft
    public double Eng2N1Pct { get; init; }
    public double Eng1N2Pct { get; init; }
    public double Eng2N2Pct { get; init; }
    public double Eng1IttC { get; init; }     // ITT °C — aircraft-specific limits, logged for visibility
    public double Eng2IttC { get; init; }

    // Annunciators / failure flags
    public bool EngineFire { get; init; }           // either engine fire annunciator lit
    public bool OilPressureLow { get; init; }
    public bool FuelPressureLow { get; init; }
    public bool HydraulicPressureLow { get; init; }
    public bool LowVoltage { get; init; }
    public bool MasterCaution { get; init; }
    public bool MasterWarning { get; init; }
    public double IceDamage { get; init; }          // sim/flightmodel/failures/frm_ice, 0=none
    public bool OverGFailure { get; init; }         // XP12 structural over-G flag

    // Navigation
    public double GlideslopeDevDots { get; init; } // ILS GS, -2.5 to +2.5 dots
    public double LocalizerDevDots { get; init; }  // ILS LOC, -2.5 to +2.5 dots

    // Flags
    public double StallWarning { get; init; }
    public bool HasCrashed { get; init; }
    public bool Overspeed { get; init; }

    // Weather (at aircraft altitude)
    public double WindSpeedKt { get; init; }
    public double WindDirectionDeg { get; init; }
    public double RainPercent { get; init; }       // 0–1; sim/weather/rain_percent

    // Aircraft performance limits (kias, static per aircraft)
    public double VsoKts { get; init; }   // stall speed, landing config
    public double VnoKts { get; init; }   // normal operating speed
    public double VneKts { get; init; }   // never exceed
    public double VfeKts { get; init; }   // max flaps extended
    public double VsCleanKts { get; init; }  // clean-config stall speed (XP12 exposes no Vle)

    // Sim state
    public bool IsSimPaused { get; init; }

    // Time of day / conditions
    public double LocalTimeSec { get; init; }    // seconds since midnight (local sim time)
    public double SunPitchDeg { get; init; }     // sun elevation; < 0 = below horizon (night)
    public double VisibilityM { get; init; }     // horizontal visibility in metres
    public double CloudBaseAglM { get; init; }   // lowest cloud base AGL in metres
    public double CloudCoverage { get; init; }   // 0–1 fraction cloud cover
    public double OutsideAirTempC { get; init; } // OAT in °C; 0 to -20 + cloud = icing risk

    // Meta
    public string AircraftName { get; init; } = "";
    public DateTime Timestamp { get; init; }
}
