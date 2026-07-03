namespace CheckRide.Models;

public enum FlightEventType
{
    // Speed / structural
    Overspeed,
    Stall,

    // G-force
    HighG,
    VeryHighG,

    // Bank
    HighBank,
    VeryHighBank,

    // Crash / excursion
    Crash,
    RunwayExcursion,

    // Landing quality
    HardLanding,
    FirmLanding,
    SideloadLanding,

    // Approach
    HighDescentRate,
    ExcessiveApproachSpeed,
    UnstableApproach,

    // Gear
    GearUpLanding,

    // Approach
    FlapOverspeed,
    SpeedLimitViolation,    // 250kt below 10,000ft

    // Landing
    FastLanding,            // high IAS at touchdown

    // Systems
    SystemPitotHeat,
    SystemFlapsCruise,
    SystemGearCruise,
    SystemGearApproach,
    SystemLandingLights,
    SystemBeacon,
    SystemStrobes,
    SystemTransponder,
    SystemAntiIce,
    SystemBarometer,
    SystemIMC,              // flight into IMC conditions (vis < 3SM or OVC)

    // Taxi
    TaxiFastSpeed,
    TaxiAggressiveTurn,

    // Takeoff
    TakeoffLowPower,
    TakeoffHeadingDeviation,
    TakeoffDirectionalControl,

    // Aircraft failures
    FailureEngineFire,
    FailureEngineOut,
    FailureEngineOverspeed,
    FailureOilPressure,
    FailureFuelPressure,
    FailureHydraulic,
    FailureLowVoltage,
    FailureOverG,
    FailureIcingDamage,
}

public class FlightEvent
{
    public FlightEventType Type { get; init; }
    public FlightPhase Phase { get; init; }
    public int TimestampSec { get; init; }
    public string Description { get; init; } = "";
}
