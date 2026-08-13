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
    ApproachTooSlow,
    UnstableApproach,

    // Gear
    GearUpLanding,

    // Approach
    FlapOverspeed,
    SpeedLimitViolation,    // 250kt below 10,000ft

    // Landing
    FastLanding,            // high IAS at touchdown
    NoseWheelFirst,         // nose gear touched before main gear
    NoFlapLanding,          // flap ratio < threshold at touchdown

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
    TakeoffParkingBrake,    // parking brake set at moment of liftoff
    WrongDepartureAirport,
    WrongArrivalAirport,
    DivertedToAlternate,    // landed away from planned arrival after a declared diversion — no penalty

    // Milestones (no scoring impact — timeline markers only)
    RecordingStarted,
    EngineStart,
    DiversionDeclared,      // pilot committed to landing somewhere other than the planned destination — reason in Description
    RunupCheckCompleted,    // sustained high-N1 power check while stationary, pre-takeoff
    Turbulence,              // retired 2026-08-13 (unreliable detection) — kept so historical event logs still deserialize
    LowVisCruise,            // visibility dropped below 1SM while in cruise — "I Can't See Nothing" bonus
    Takeoff,
    CruiseReached,
    ApproachStarted,
    GoAround,
    Touchdown,
    ParkingBrakeSet,

    // Fuel
    LowFuel,
    FuelExhausted,

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
