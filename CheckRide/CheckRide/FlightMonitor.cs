using CheckRide.Models;

namespace CheckRide;

// All tunable detection thresholds and scoring weights in one place.
// Detection thresholds control when an event is created.
// Scoring weights control how much each event costs/earns.
internal static class ScoringConfig
{
    // --- Detection thresholds ---
    public const double TaxiingGsKts         = 5.0;   // GS above this while on ground = taxiing
    public const double AirborneAglFt        = 50.0;  // AGL above this = airborne
    public const double LandedAglFt          = 2.0;   // AGL below this while descending = landed

    public const double HighBankDeg          = 45.0;
    public const double VeryHighBankDeg      = 60.0;
    public const double HighGForce           = 2.5;
    public const double VeryHighGForce       = 3.5;

    public const double HardLandingFpm       = 600.0;
    public const double FirmLandingFpm       = 300.0;
    public const double SideloadLandingG     = 0.3;

    public const double UnstableSpeedFactor  = 1.3;   // Vref × this = unstable threshold below 500ft AGL
    public const double ExcessSpeedFactor    = 1.5;   // Vref × this = excessive threshold below 1000ft AGL
    public const double HighDescentFpm       = 1500.0; // VS below this on approach
    public const double FinalDescentAglFt    = 1500.0; // track average VS below this altitude

    public const double ImcVisibilityM       = 4828.0; // 3 statute miles — below this = IMC
    public const double ImcCloudCoverage     = 0.75;   // OVC/heavy BKN — secondary IMC indicator

    public const double AntiIceTempLowC     = -20.0; // lower bound of icing window
    public const double AntiIceTempHighC    = 0.0;   // upper bound of icing window
    public const double AntiIceCloudMin     = 0.1;   // minimum cloud coverage to flag icing
    public const double IceDamageThreshold  = 0.01;  // frm_ice above this = icing damage event
    public const double N1OverspeedPct        = 100.0; // N1% above this = prop/turbine over-speed (universal %)
    public const double N1OverspeedBufferPct  = 2.0;  // grace buffer before flagging
    public const double VnoOverspeedBufferKts = 5.0;  // kt above Vno before flagging

    public const int    CruiseStableCount   = 30;    // consecutive polls above cruise altitude to transition
    public const double CruiseAglFt         = 3000.0; // AGL above this = cruise phase candidate

    // Taxi detection
    public const double FastTaxiGsKts         = 25.0;  // GS above this while turning = fast taxi
    public const double FastTaxiLateralG      = 0.10;  // lateral G gate — excludes straight takeoff roll
    public const double FastTaxiHdgRateDps    = 5.0;   // heading change rate (°/s) gate
    public const double AggrTaxiLateralG      = 0.30;  // lateral G for aggressive turn at any speed

    // Takeoff detection
    public const double TakeoffMinN1Pct       = 90.0;  // N1% below this at liftoff = low power takeoff
    public const double TakeoffHdgDevDeg      = 20.0;  // heading deviation from departure below 500ft AGL
    public const double TakeoffSideloadG      = 0.40;  // lateral G during initial climb (below 200ft AGL)

    // --- Scoring penalties (negative) ---
    public const int PenaltyOverspeed          = -5;   // per occurrence
    public const int PenaltySpeedLimit         = -10;  // 250kt below 10,000ft
    public const int PenaltyStall              = -5;   // per occurrence
    public const int PenaltyHighG              = -3;   // per occurrence
    public const int PenaltyVeryHighG          = -8;   // per occurrence
    public const int PenaltyHighBank           = -3;   // per occurrence
    public const int PenaltyVeryHighBank       = -8;   // per occurrence
    public const int PenaltyCrash              = -40;
    public const int PenaltyRunwayExcursion    = -35;
    public const int PenaltyExcessApproachSpd  = -15;
    public const int PenaltyUnstableApproach   = -15;
    public const int PenaltyFlapOverspeed      = -10;
    public const int PenaltyHardLanding        = -15;
    public const int PenaltyFirmLanding        = -5;
    public const int PenaltyFastLanding       = -10;
    public const int PenaltyHighDescentRate    = -5;   // per occurrence
    public const int PenaltyGearUpLanding      = -25;
    public const int PenaltySideloadLanding    = -10;
    public const int PenaltySystemFlag         = -3;   // per system check failure
    public const int PenaltyTaxiFastSpeed      = -5;
    public const int PenaltyTaxiAggrTurn       = -3;
    public const int PenaltyTakeoffLowPower         = -5;
    public const int PenaltyTakeoffHdgDev           = -5;
    public const int PenaltyTakeoffSideload         = -5;
    public const int PenaltyWrongDepartureAirport   = -20;
    public const int PenaltyEngineFire         = -30;
    public const int PenaltyEngineOut          = -25;
    public const int PenaltyEngineOverspeed    = -20;
    public const int PenaltyOverGFailure       = -20;
    public const int PenaltyIcingDamage        = -20;
    public const int PenaltySystemFailure      = -10;  // per oil/fuel/hydraulic/voltage event

    // --- Scoring bonuses (positive) ---
    public const int BonusGreaser              = 10;   // VS < 75 fpm
    public const int BonusSmooth               = 10;   // VS < 150 fpm
    public const int BonusCleanSystems         = 5;    // zero system flags
    public const int BonusCleanFlight          = 5;    // no overspeed and no stall
}

public class FlightMonitor
{
    private FlightPhase _phase = FlightPhase.Idle;
    private readonly List<FlightEvent> _events = new();

    private DateTime _startTime;
    private DateTime _airborneTime;
    private DateTime _parkedTime;
    private int _elapsedSec;

    private double _landingVsFpm;
    private double _landingLateralG;
    private double _windSpeedAtLanding;
    private double _windDirAtLanding;
    private string _aircraft = "";

    // Aircraft performance limits — captured from first valid snapshot
    private bool _limitsCaptured;
    private double _vsoKts;
    private double _vnoKts;
    private double _vneKts;
    private double _vfeKts;
    private double _vleKts;
    private double _vrefKts;        // 1.3 × Vso
    private bool _flapOverspeedFlagged;

    // Running stats
    private double _prevLat;
    private double _prevLon;
    private double _distanceNm;
    private int _apTimeSec;
    private double _maxAltMslFt;
    private double _maxAltAglFt;
    private double _maxIasKts;
    private double _maxBankDeg;
    private double _maxGNormal;
    private double _maxGLateral;
    private double _finalDescentSamples;
    private double _finalDescentSum;
    private int _goArounds;

    // Flight track (30-second samples)
    private readonly List<FlightTrackPoint> _track = new();
    private DateTime _lastTrackSample = DateTime.MinValue;
    private const int TrackIntervalSec = 30;

    // Condition samples — initial capture + every 5 minutes
    private readonly List<FlightConditionSample> _conditions = new();
    private DateTime _lastConditionSample = DateTime.MinValue;
    private const int ConditionIntervalSec = 300;

    // Edge-trigger state — fire once per rising edge
    private bool _prevOverspeed;
    private bool _prevStall;
    private bool _prevHighG;
    private bool _prevVeryHighG;
    private bool _prevHighBank;
    private bool _prevVeryHighBank;
    private bool _prevCrashed;
    private bool _prevSpeedLimit;
    private bool _excursionFlagged;

    // System state checks — one flag per flight
    private bool _pitotFlagged;
    private bool _flapsCruiseFlagged;
    private bool _gearCruiseFlagged;
    private bool _gearApproachFlagged;
    private bool _landingLightsFlagged;
    private bool _beaconFlagged;
    private bool _strobeFlagged;
    private bool _transponderFlagged;
    private bool _highDescentFlagged;
    private bool _approachSpeedFlagged;
    private bool _unstableApproachFlagged;
    private bool _antiIceFlagged;
    private bool _imcFlagged;
    private double _baroOnTakeoff;
    private bool _baroEverChanged;
    private bool _baroEverAt2992WhileHigh;
    private double _maxAltMsl;
    private bool _baroCheckedLanding;
    private double _peakAglFt;

    // Expected departure (from saved flight)
    private readonly string _expectedDepId;
    private readonly double _expectedDepLat;
    private readonly double _expectedDepLon;
    private bool _wrongDepFlagged;

    // Fires at liftoff (initial takeoff only, not go-arounds)
    public event Action? AfterTakeoffCallout;

    // Fires at touchdown with quality: "excellent", "good", or "poor"
    public event Action<string>? TouchdownCallout;

    // In-flight personality callouts — fire once per flight, no scoring impact
    public event Action? CalloutRain;
    public event Action? CalloutIcing;
    public event Action? CalloutTurbulence;

    // Scoring-linked callouts — fire each time the event occurs
    public event Action? CalloutOverspeed;
    public event Action? CalloutHighBank;

    // Flight completion
    public event Action? FlightCompleted;
    private bool   _completionFired;
    private double _depLat, _depLon;
    private bool   _depSet;

    // Personality callout flags — fire once per flight
    private bool _rainCalloutFired;
    private bool _icingCalloutFired;
    private bool _turbulenceCalloutFired;
    private int  _turbulentSampleCount;

    // Taxi detection
    private bool _fastTaxiFlagged;
    private bool _aggressiveTurnFlagged;
    private double _prevHdgDeg = -1;

    // Takeoff detection
    private double _departureHdgDeg;
    private bool _hdgDeviationFlagged;
    private bool _takeoffSideloadFlagged;

    // Failure detection flags
    private bool _engineFireFlagged;
    private bool _eng1OutFlagged;
    private bool _eng2OutFlagged;
    private bool _prevEng1Running;
    private bool _prevEng2Running;
    private bool _oilFlagged;
    private bool _fuelPrsFlagged;
    private bool _hydFlagged;
    private bool _voltFlagged;
    private bool _overGFlagFlagged;
    private bool _iceDamageFlagged;
    private bool _engOverspeedFlagged;

    // Stable-conditions counter for Airborne → Cruise transition
    private int _stableCount;
    private const int StableRequired = 30; // ~30 consecutive 1-second polls

    public FlightPhase CurrentPhase => _phase;

    public FlightMonitor(string depId = "", double depLat = 0, double depLon = 0)
    {
        _expectedDepId  = depId;
        _expectedDepLat = depLat;
        _expectedDepLon = depLon;
    }

    public void OnSnapshot(FlightDataSnapshot snap, EventLogger logger)
    {
        if (_startTime == default) _startTime = snap.Timestamp;
        _elapsedSec = (int)(snap.Timestamp - _startTime).TotalSeconds;

        SampleTrack(snap);
        SampleConditions(snap);

        if (string.IsNullOrEmpty(_aircraft) && !string.IsNullOrEmpty(snap.AircraftName))
        {
            _aircraft = snap.AircraftName;
            logger.Log($"Aircraft: {_aircraft}");
        }

        if (!_limitsCaptured && snap.VsoKts > 0)
        {
            _vsoKts = snap.VsoKts;
            _vnoKts = snap.VnoKts;
            _vneKts = snap.VneKts > snap.VnoKts * 2 ? snap.VnoKts * 1.4 : snap.VneKts;
            _vfeKts = snap.VfeKts;
            _vleKts = snap.VleKts;
            _vrefKts = snap.VsoKts * 1.3;
            _limitsCaptured = true;
            logger.Log($"Aircraft limits — Vso={_vsoKts:F0}kt Vno={_vnoKts:F0}kt Vne={_vneKts:F0}kt " +
                       $"Vfe={_vfeKts:F0}kt Vle={_vleKts:F0}kt → Vref={_vrefKts:F0}kt");
        }

        if (_phase is >= FlightPhase.Airborne and <= FlightPhase.Approach)
        {
            _maxGNormal  = Math.Max(_maxGNormal,  Math.Abs(snap.GForceNormal));
            _maxGLateral = Math.Max(_maxGLateral, Math.Abs(snap.GForceLateral));
            _maxAltMslFt = Math.Max(_maxAltMslFt, snap.AltitudeMslFt);
            _maxAltAglFt = Math.Max(_maxAltAglFt, snap.AltitudeAglFt);
            _maxIasKts   = Math.Max(_maxIasKts,   snap.IndicatedAirspeedKts);
            _maxBankDeg  = Math.Max(_maxBankDeg,  Math.Abs(snap.BankAngleDeg));

            // Distance — skip first sample (no previous position)
            if (_prevLat != 0 || _prevLon != 0)
                _distanceNm += HaversineNm(_prevLat, _prevLon, snap.Latitude, snap.Longitude);
            _prevLat = snap.Latitude;
            _prevLon = snap.Longitude;

            // Autopilot time
            if (snap.AutopilotOn) _apTimeSec++;

            // Final descent samples — descending below 1,500ft AGL
            if (snap.AltitudeAglFt < 1500 && snap.VerticalSpeedFpm < -100)
            {
                _finalDescentSum += Math.Abs(snap.VerticalSpeedFpm);
                _finalDescentSamples++;
            }
        }

        // Flight completion: parking brake set after landing at a different airport
        if (!_completionFired && snap.ParkingBrakeSet && _phase == FlightPhase.Landed && _depSet)
        {
            var distNm = HaversineNm(_depLat, _depLon, snap.Latitude, snap.Longitude);
            logger.Log($"[DEBUG] Parking brake set — {distNm:F1}nm from departure, GPS=({snap.Latitude:F5},{snap.Longitude:F5})");
            if (distNm >= Config.CompletionDistanceNm)
            {
                _completionFired = true;
                LogEvent(FlightEventType.ParkingBrakeSet, snap.Timestamp, "Parking brake set — flight complete");
                FlightCompleted?.Invoke();
            }
        }

        TransitionPhase(snap, logger);
        DetectEvents(snap, logger);
        DetectFailures(snap, logger);

        // Crash triggers completion (after DetectFailures so crash event is logged first)
        if (!_completionFired && snap.HasCrashed)
        {
            _completionFired = true;
            FlightCompleted?.Invoke();
        }

        logger.Log($"[{_phase}] " +
                   $"GS={snap.GroundspeedKts:F1}kt IAS={snap.IndicatedAirspeedKts:F1}kt VS={snap.VerticalSpeedFpm:F0}fpm AGL={snap.AltitudeAglFt:F0}ft " +
                   $"Bnk={snap.BankAngleDeg:F1}° Pit={snap.PitchAngleDeg:F1}° Hdg={snap.MagHeadingDeg:F0}° " +
                   $"Gn={snap.GForceNormal:F2} Gl={snap.GForceLateral:F2} Ga={snap.GForceAxial:F2} " +
                   $"Sink={snap.TireSinkDepthM:F3}m " +
                   $"Thr={snap.ThrottleRatio:F2} Flp={snap.FlapRatio:F2} Sbk={snap.SpeedbrakeRatio:F2} " +
                   $"Wind={snap.WindSpeedKt:F0}kt@{snap.WindDirectionDeg:F0}° " +
                   $"Xpdr={snap.TransponderMode} AP={snap.AutopilotOn} " +
                   $"GS={snap.GlideslopeDevDots:F2}dot LOC={snap.LocalizerDevDots:F2}dot " +
                   $"N1={snap.Eng1N1Pct:F1}%/{snap.Eng2N1Pct:F1}% ITT={snap.Eng1IttC:F0}°C/{snap.Eng2IttC:F0}°C OAT={snap.OutsideAirTempC:F1}°C " +
                   $"AoA={snap.AngleOfAttackDeg:F1}° StallW={snap.StallWarning:F2} MstrW={snap.MasterWarning}");
    }

    private void TransitionPhase(FlightDataSnapshot snap, EventLogger logger)
    {
        if (_phase == FlightPhase.Idle)
        {
            if (snap.OnGround && snap.GroundspeedKts > 2)
            {
                _phase = FlightPhase.Taxiing;
                if (!_depSet)
                {
                    _depLat = snap.Latitude; _depLon = snap.Longitude; _depSet = true;
                    if (_expectedDepLat != 0 && !_wrongDepFlagged)
                    {
                        var distNm = HaversineNm(_expectedDepLat, _expectedDepLon, snap.Latitude, snap.Longitude);
                        logger.Log($"Departure check — {distNm:F1}nm from {_expectedDepId}");
                        if (distNm > 3.0)
                        {
                            LogEvent(FlightEventType.WrongDepartureAirport, snap.Timestamp,
                                $"Wrong departure airport — {distNm:F0}nm from {_expectedDepId}");
                            logger.Log($"EVENT: Wrong departure airport ({distNm:F0}nm from {_expectedDepId})");
                            _wrongDepFlagged = true;
                        }
                    }
                }
                logger.Log($"Phase → Taxiing  GPS=({snap.Latitude:F5},{snap.Longitude:F5})");
            }
        }
        else if (_phase == FlightPhase.Taxiing)
        {
            if (!snap.OnGround)
            {
                _airborneTime = snap.Timestamp;
                _stableCount = 0;
                _phase = FlightPhase.Airborne;
                _departureHdgDeg = snap.MagHeadingDeg;
                _hdgDeviationFlagged = false;
                _takeoffSideloadFlagged = false;
                SampleTrack(snap, forced: true);
                logger.Log($"Phase → Airborne  GPS=({snap.Latitude:F5},{snap.Longitude:F5})");
                LogEvent(FlightEventType.Takeoff, snap.Timestamp, $"Takeoff — {snap.GroundspeedKts:F0} kt GS");
                AfterTakeoffCallout?.Invoke();

                var liftoffN1 = Math.Max(snap.Eng1N1Pct, snap.Eng2N1Pct);
                if (liftoffN1 < ScoringConfig.TakeoffMinN1Pct)
                {
                    LogEvent(FlightEventType.TakeoffLowPower, snap.Timestamp,
                        $"Low power takeoff — N1={snap.Eng1N1Pct:F0}%/{snap.Eng2N1Pct:F0}% at liftoff");
                    logger.Log($"EVENT: Low power takeoff (N1={snap.Eng1N1Pct:F0}%/{snap.Eng2N1Pct:F0}%)");
                }
            }
        }
        else if (_phase == FlightPhase.Airborne)
        {
            if (snap.OnGround)
            {
                RecordLanding(snap, logger);
                _phase = FlightPhase.Landed;
                logger.Log("Phase → Landed");
            }
            else if (snap.AltitudeAglFt > 1000
                  && Math.Abs(snap.VerticalSpeedFpm) < 500
                  && snap.GroundspeedKts > 40)
            {
                if (++_stableCount >= StableRequired)
                {
                    _phase = FlightPhase.Cruise;
                    logger.Log("Phase → Cruise");
                    LogEvent(FlightEventType.CruiseReached, snap.Timestamp, $"Cruise — {snap.AltitudeMslFt:F0} ft");
                }
            }
            else
            {
                _stableCount = 0;
            }
        }
        else if (_phase == FlightPhase.Cruise)
        {
            if (snap.OnGround)
            {
                RecordLanding(snap, logger);
                _phase = FlightPhase.Landed;
                logger.Log("Phase → Landed");
            }
            else if (snap.AltitudeAglFt < 5000 && snap.VerticalSpeedFpm < -300)
            {
                _phase = FlightPhase.Approach;
                _highDescentFlagged = false;
                logger.Log("Phase → Approach");
                LogEvent(FlightEventType.ApproachStarted, snap.Timestamp, $"Approach — {snap.AltitudeAglFt:F0} ft AGL");
            }
        }
        else if (_phase == FlightPhase.Approach)
        {
            if (snap.OnGround)
            {
                RecordLanding(snap, logger);
                _phase = FlightPhase.Landed;
                logger.Log("Phase → Landed");
            }
            else if (snap.VerticalSpeedFpm > 500 && snap.AltitudeAglFt > 1000)
            {
                _stableCount = 0;
                _goArounds++;
                _phase = FlightPhase.Airborne;
                logger.Log("Phase → Airborne (go-around)");
                LogEvent(FlightEventType.GoAround, snap.Timestamp, $"Go-around — {snap.AltitudeAglFt:F0} ft AGL");
            }
        }
        else if (_phase == FlightPhase.Landed)
        {
            // Require >10ft AGL to avoid triggering on runway bumps/terrain mesh errors
            if (!snap.OnGround && snap.AltitudeAglFt > 10)
            {
                // Touch-and-go — don't reset _airborneTime so total flight time accumulates
                _stableCount = 0;
                _phase = FlightPhase.Airborne;
                _departureHdgDeg = snap.MagHeadingDeg;
                _hdgDeviationFlagged = false;
                _takeoffSideloadFlagged = false;
                logger.Log("Phase → Airborne (touch-and-go)");
            }
            else
            {
                _parkedTime = snap.Timestamp;

                // Runway excursion: off the surface at speed without climbing, or below terrain (lake/ditch)
                if (!_excursionFlagged && snap.GroundspeedKts > 10
                    && (!snap.OnGround || snap.AltitudeAglFt < -2))
                {
                    LogEvent(FlightEventType.RunwayExcursion, snap.Timestamp, "Runway excursion — off-runway at speed");
                    logger.Log("EVENT: Runway excursion");
                    _excursionFlagged = true;
                }
            }
        }
    }

    private void RecordLanding(FlightDataSnapshot snap, EventLogger logger)
    {
        SampleTrack(snap, forced: true);
        logger.Log($"Landing  GPS=({snap.Latitude:F5},{snap.Longitude:F5})  VS={snap.VerticalSpeedFpm:F0}fpm");
        _landingVsFpm       = snap.VerticalSpeedFpm;
        _landingLateralG    = snap.GForceLateral;
        _windSpeedAtLanding = snap.WindSpeedKt;
        _windDirAtLanding   = snap.WindDirectionDeg;
        var absVs  = Math.Abs(_landingVsFpm);
        var absLat = Math.Abs(snap.GForceLateral);
        var fast   = _vrefKts > 0 && snap.IndicatedAirspeedKts > _vrefKts * 1.15;

        var quality = "excellent";

        if (snap.GearDeployRatio < 0.9)
        {
            LogEvent(FlightEventType.GearUpLanding, snap.Timestamp, "Gear-up landing");
            logger.Log("EVENT: Gear-up landing");
            quality = "poor";
        }
        else
        {
            if (absVs > ScoringConfig.HardLandingFpm)
            {
                LogEvent(FlightEventType.HardLanding, snap.Timestamp, $"Hard landing — {absVs:F0} fpm");
                logger.Log($"EVENT: Hard landing ({absVs:F0} fpm)");
                quality = "poor";
            }
            else if (absVs > ScoringConfig.FirmLandingFpm)
            {
                LogEvent(FlightEventType.FirmLanding, snap.Timestamp, $"Firm landing — {absVs:F0} fpm");
                logger.Log($"EVENT: Firm landing ({absVs:F0} fpm)");
                quality = "good";
            }
            else
            {
                logger.Log($"Landing: {absVs:F0} fpm");
            }

            if (fast)
            {
                LogEvent(FlightEventType.FastLanding, snap.Timestamp,
                    $"High speed landing — {snap.IndicatedAirspeedKts:F0}kt (Vref={_vrefKts:F0}kt)");
                logger.Log($"EVENT: High speed landing ({snap.IndicatedAirspeedKts:F0}kt, Vref={_vrefKts:F0}kt)");
                if (quality == "excellent") quality = "good";
            }

            if (absLat > ScoringConfig.SideloadLandingG)
            {
                LogEvent(FlightEventType.SideloadLanding, snap.Timestamp, $"Side-load landing — {absLat:F2}G lateral");
                logger.Log($"EVENT: Side-load landing ({absLat:F2}G lateral)");
                quality = "poor";
            }
        }

        logger.Log($"Touchdown quality: {quality}");
        var qualityLabel = quality == "excellent" ? "Greaser" : quality == "good" ? "Smooth" : "Hard";
        LogEvent(FlightEventType.Touchdown, snap.Timestamp, $"Landing — {qualityLabel} ({(int)Math.Abs(_landingVsFpm)} fpm)");
        TouchdownCallout?.Invoke(quality);
    }

    private void DetectEvents(FlightDataSnapshot snap, EventLogger logger)
    {
        if (_phase == FlightPhase.Idle) return;

        // Crash (once)
        if (snap.HasCrashed && !_prevCrashed)
        {
            LogEvent(FlightEventType.Crash, snap.Timestamp, "Aircraft damage / crash");
            logger.Log("EVENT: Crash");
        }
        _prevCrashed = snap.HasCrashed;

        // Overspeed — IAS exceeds Vno + 5kt buffer; rising edge only
        var overVno = _vnoKts > 0 && snap.IndicatedAirspeedKts > _vnoKts + ScoringConfig.VnoOverspeedBufferKts
                      && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        if (overVno && !_prevOverspeed)
        {
            LogEvent(FlightEventType.Overspeed, snap.Timestamp, $"Exceeded Vno — {snap.IndicatedAirspeedKts:F0}kt (limit {_vnoKts:F0}kt)");
            logger.Log($"EVENT: Overspeed ({snap.IndicatedAirspeedKts:F0}kt > Vno {_vnoKts:F0}kt)");
            CalloutOverspeed?.Invoke();
        }
        _prevOverspeed = overVno;

        // 250kt below 10,000ft — regulatory speed limit (FAR 91.117), rising edge
        var overLimit = snap.AltitudeMslFt < 10000 && snap.IndicatedAirspeedKts > 250
                        && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        if (overLimit && !_prevSpeedLimit)
        {
            LogEvent(FlightEventType.SpeedLimitViolation, snap.Timestamp,
                $"Exceeded 250kt below 10,000ft — {snap.IndicatedAirspeedKts:F0}kt at {snap.AltitudeMslFt:F0}ft");
            logger.Log($"EVENT: Speed limit violation ({snap.IndicatedAirspeedKts:F0}kt below 10,000ft)");
        }
        _prevSpeedLimit = overLimit;

        // Stall — annunciator (any non-zero = stick shaker) OR high AoA
        var airborne = _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        var stalling = (snap.StallWarning > 0 && airborne)
                    || (snap.AngleOfAttackDeg > 14.0 && airborne);
        if (stalling && !_prevStall)
        {
            LogEvent(FlightEventType.Stall, snap.Timestamp,
                $"Stall — AoA={snap.AngleOfAttackDeg:F1}° StallW={snap.StallWarning:F2}");
            logger.Log($"EVENT: Stall (AoA={snap.AngleOfAttackDeg:F1}°, StallW={snap.StallWarning:F2})");
        }
        _prevStall = stalling;

        // G-force — two separate bands, mutually exclusive per snapshot
        var absG = Math.Abs(snap.GForceNormal);
        var isVeryHighG = absG > 3.5;
        var isHighG = absG > 2.5 && !isVeryHighG;

        if (isVeryHighG && !_prevVeryHighG)
        {
            LogEvent(FlightEventType.VeryHighG, snap.Timestamp, $"Excessive G-force — {snap.GForceNormal:F1}G");
            logger.Log($"EVENT: Very high G ({snap.GForceNormal:F1}G)");
        }
        _prevVeryHighG = isVeryHighG;

        if (isHighG && !_prevHighG)
        {
            LogEvent(FlightEventType.HighG, snap.Timestamp, $"High G-force manoeuvre — {snap.GForceNormal:F1}G");
            logger.Log($"EVENT: High G ({snap.GForceNormal:F1}G)");
        }
        _prevHighG = isHighG;

        // Bank angle — rising edge, airborne phases only
        var absBank = Math.Abs(snap.BankAngleDeg);
        var isVeryHighBank = absBank > 60;
        var isHighBank = absBank > 45 && !isVeryHighBank;

        if (isVeryHighBank && !_prevVeryHighBank
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.VeryHighBank, snap.Timestamp, $"Excessive bank — {snap.BankAngleDeg:F1}°");
            logger.Log($"EVENT: Very high bank ({snap.BankAngleDeg:F1}°)");
            CalloutHighBank?.Invoke();
        }
        _prevVeryHighBank = isVeryHighBank;

        if (isHighBank && !_prevHighBank
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.HighBank, snap.Timestamp, $"High bank — {snap.BankAngleDeg:F1}°");
            logger.Log($"EVENT: High bank ({snap.BankAngleDeg:F1}°)");
            CalloutHighBank?.Invoke();
        }
        _prevHighBank = isHighBank;

        // Flap overspeed — fires regardless of phase (can happen during climbout or missed approach)
        if (_vfeKts > 0 && snap.FlapRatio > 0.05 && snap.IndicatedAirspeedKts > _vfeKts && !_flapOverspeedFlagged
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.FlapOverspeed, snap.Timestamp,
                $"Flap overspeed — {snap.IndicatedAirspeedKts:F0}kt with flaps (Vfe={_vfeKts:F0}kt)");
            logger.Log($"EVENT: Flap overspeed ({snap.IndicatedAirspeedKts:F0}kt, Vfe={_vfeKts:F0}kt)");
            _flapOverspeedFlagged = true;
        }

        // System state checks (airborne phases only)
        if (_phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach or FlightPhase.Landed)
            DetectSystemChecks(snap, logger);

        if (snap.AltitudeAglFt > _peakAglFt) _peakAglFt = snap.AltitudeAglFt;

        if (_phase == FlightPhase.Taxiing)
            DetectTaxiEvents(snap, logger);

        if (_phase == FlightPhase.Airborne && snap.AltitudeAglFt < 500)
            DetectTakeoffClimbEvents(snap, logger);

        if (_phase == FlightPhase.Approach)
            DetectApproachEvents(snap, logger);
        else if (_phase == FlightPhase.Airborne && snap.AltitudeAglFt < 1500
                 && snap.VerticalSpeedFpm < -200 && _peakAglFt > 1000)
            DetectApproachEvents(snap, logger);

        _prevHdgDeg = snap.MagHeadingDeg;
    }

    private void DetectSystemChecks(FlightDataSnapshot snap, EventLogger logger)
    {
        if (!snap.PitotHeatOn && !_pitotFlagged
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.SystemPitotHeat, snap.Timestamp, "Pitot heat off in flight");
            logger.Log("EVENT: Pitot heat off while airborne");
            _pitotFlagged = true;
        }

        if (_phase == FlightPhase.Cruise && snap.FlapRatio > 0.05 && !_flapsCruiseFlagged
            && _airborneTime != default
            && (snap.Timestamp - _airborneTime).TotalMinutes >= 5)
        {
            LogEvent(FlightEventType.SystemFlapsCruise, snap.Timestamp, "Flaps not retracted after climb");
            logger.Log("EVENT: Flaps extended in cruise (>5 min airborne)");
            _flapsCruiseFlagged = true;
        }

        if (_phase == FlightPhase.Cruise && snap.GearDeployRatio > 0.1 && !_gearCruiseFlagged)
        {
            LogEvent(FlightEventType.SystemGearCruise, snap.Timestamp, "Gear not retracted after takeoff");
            logger.Log("EVENT: Gear extended in cruise");
            _gearCruiseFlagged = true;
        }

        if (_phase == FlightPhase.Approach && snap.AltitudeAglFt < 500
            && snap.GearDeployRatio < 0.9 && !_gearApproachFlagged)
        {
            LogEvent(FlightEventType.SystemGearApproach, snap.Timestamp, "Gear not down on approach");
            logger.Log("EVENT: Gear not down on short final");
            _gearApproachFlagged = true;
        }

        var onShortFinal = _phase == FlightPhase.Airborne && snap.AltitudeAglFt < 500
                        || _phase is FlightPhase.Approach or FlightPhase.Landed;
        if (onShortFinal && !snap.LandingLightsOn && !_landingLightsFlagged)
        {
            LogEvent(FlightEventType.SystemLandingLights, snap.Timestamp, "Landing lights off on approach");
            logger.Log("EVENT: Landing lights off on approach");
            _landingLightsFlagged = true;
        }

        if (!snap.BeaconOn && !_beaconFlagged
            && _phase is FlightPhase.Taxiing or FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.SystemBeacon, snap.Timestamp, "Beacon off with engines running");
            logger.Log("EVENT: Beacon off");
            _beaconFlagged = true;
        }

        if (!snap.StrobeLightsOn && !_strobeFlagged
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.SystemStrobes, snap.Timestamp, "Strobe lights off while airborne");
            logger.Log("EVENT: Strobes off while airborne");
            _strobeFlagged = true;
        }

        // Transponder should be in ALT mode (4) when airborne
        if (snap.TransponderMode != 4 && !_transponderFlagged
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.SystemTransponder, snap.Timestamp,
                $"Transponder not in ALT mode (mode {snap.TransponderMode})");
            logger.Log($"EVENT: Transponder not ALT (mode {snap.TransponderMode})");
            _transponderFlagged = true;
        }

        // Barometer tracking
        var inFlight = _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        if (inFlight)
        {
            if (_baroOnTakeoff == 0) _baroOnTakeoff = snap.BarometerInHg;
            if (Math.Abs(snap.BarometerInHg - _baroOnTakeoff) > 0.005) _baroEverChanged = true;
            if (snap.AltitudeMslFt > 18000 && Math.Abs(snap.BarometerInHg - 29.92) < 0.02)
                _baroEverAt2992WhileHigh = true;
            if (snap.AltitudeMslFt > _maxAltMsl) _maxAltMsl = snap.AltitudeMslFt;
        }

        if (_phase == FlightPhase.Landed && !_baroCheckedLanding)
        {
            _baroCheckedLanding = true;
            if (_maxAltMsl > 18000)
            {
                if (!_baroEverAt2992WhileHigh)
                {
                    LogEvent(FlightEventType.SystemBarometer, snap.Timestamp,
                        "Altimeter not set to 29.92 above FL180");
                    logger.Log("EVENT: Baro never set to 29.92 above FL180");
                }
                if (Math.Abs(snap.BarometerInHg - 29.92) < 0.02)
                {
                    LogEvent(FlightEventType.SystemBarometer, snap.Timestamp,
                        "Altimeter still at 29.92 after descent from FL180 — local QNH not set");
                    logger.Log("EVENT: Baro still 29.92 at landing after FL180 flight");
                }
            }
            else if (!_baroEverChanged && _baroOnTakeoff > 0)
            {
                LogEvent(FlightEventType.SystemBarometer, snap.Timestamp,
                    $"Altimeter never adjusted during flight (stayed at {_baroOnTakeoff:F2} inHg)");
                logger.Log($"EVENT: Baro never adjusted (stayed at {_baroOnTakeoff:F2})");
            }
        }

        // IMC — visibility below 3SM or solid overcast, fires once when entered while airborne
        var inImc = snap.VisibilityM > 0
                    && (snap.VisibilityM < ScoringConfig.ImcVisibilityM
                        || snap.CloudCoverage > ScoringConfig.ImcCloudCoverage)
                    && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        if (inImc && !_imcFlagged)
        {
            var visSmiles = snap.VisibilityM / 1609.34;
            LogEvent(FlightEventType.SystemIMC, snap.Timestamp,
                $"IMC conditions — visibility {visSmiles:F1}SM, cloud coverage {snap.CloudCoverage:P0}");
            logger.Log($"EVENT: IMC conditions (vis={visSmiles:F1}SM, cloud={snap.CloudCoverage:P0})");
            _imcFlagged = true;
        }

        // Anti-ice off in icing conditions (OAT 0 to -20°C AND in cloud)
        var icingConditions = snap.OutsideAirTempC <= 0 && snap.OutsideAirTempC >= -20
                              && snap.CloudCoverage > 0.1 && snap.AltitudeAglFt > 200;
        if (icingConditions && !snap.AntiIceOn && !_antiIceFlagged)
        {
            LogEvent(FlightEventType.SystemAntiIce, snap.Timestamp,
                $"Anti-ice off in icing conditions (OAT {snap.OutsideAirTempC:F1}°C, cloud {snap.CloudCoverage:P0})");
            logger.Log($"EVENT: Anti-ice off in icing conditions (OAT {snap.OutsideAirTempC:F1}°C)");
            _antiIceFlagged = true;
        }

        // ── Personality callouts (no scoring) ────────────────────────────────
        var airborne = _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach;
        if (airborne)
        {
            // Rain callout — fire once when precipitation begins
            if (!_rainCalloutFired && snap.RainPercent > 0.05)
            {
                _rainCalloutFired = true;
                CalloutRain?.Invoke();
                logger.Log("CALLOUT: Rain detected");
            }

            // Icing callout — entering icing window (0 to -20°C in cloud)
            if (!_icingCalloutFired && icingConditions)
            {
                _icingCalloutFired = true;
                CalloutIcing?.Invoke();
                logger.Log($"CALLOUT: Icing conditions (OAT {snap.OutsideAirTempC:F1}°C)");
            }

            // Turbulence callout — 3 consecutive samples with G deviation > 0.15
            if (!_turbulenceCalloutFired)
            {
                if (Math.Abs(snap.GForceNormal - 1.0) > 0.15)
                    _turbulentSampleCount++;
                else
                    _turbulentSampleCount = 0;

                if (_turbulentSampleCount >= 3)
                {
                    _turbulenceCalloutFired = true;
                    CalloutTurbulence?.Invoke();
                    logger.Log($"CALLOUT: Turbulence (G={snap.GForceNormal:F2})");
                }
            }
        }
    }

    private void DetectFailures(FlightDataSnapshot snap, EventLogger logger)
    {
        // N1 > 102% = prop/turbine over-speed (2% buffer avoids noise)
        var maxN1 = Math.Max(snap.Eng1N1Pct, snap.Eng2N1Pct);
        if (maxN1 > ScoringConfig.N1OverspeedPct + ScoringConfig.N1OverspeedBufferPct && !_engOverspeedFlagged)
        {
            var eng = snap.Eng1N1Pct > snap.Eng2N1Pct ? 1 : 2;
            LogEvent(FlightEventType.FailureEngineOverspeed, snap.Timestamp,
                $"Engine {eng} N1 over-speed — {maxN1:F1}%");
            logger.Log($"EVENT: Engine {eng} N1 over-speed ({maxN1:F1}%)");
            _engOverspeedFlagged = true;
        }

        if (snap.EngineFire && !_engineFireFlagged)
        {
            LogEvent(FlightEventType.FailureEngineFire, snap.Timestamp, "Engine fire warning");
            logger.Log("EVENT: Engine fire");
            _engineFireFlagged = true;
        }

        // Engine out in flight — either engine stops while airborne
        var eng1Out = !snap.Engine1Running;
        var eng2Out = !snap.Engine2Running;
        if (!_eng1OutFlagged && eng1Out && _prevEng1Running
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.FailureEngineOut, snap.Timestamp, "Engine 1 out in flight");
            logger.Log("EVENT: Engine 1 out in flight");
            _eng1OutFlagged = true;
        }
        if (!_eng2OutFlagged && eng2Out && _prevEng2Running
            && _phase is FlightPhase.Airborne or FlightPhase.Cruise or FlightPhase.Approach)
        {
            LogEvent(FlightEventType.FailureEngineOut, snap.Timestamp, "Engine 2 out in flight");
            logger.Log("EVENT: Engine 2 out in flight");
            _eng2OutFlagged = true;
        }
        _prevEng1Running = snap.Engine1Running;
        _prevEng2Running = snap.Engine2Running;

        if (snap.OilPressureLow && !_oilFlagged)
        {
            LogEvent(FlightEventType.FailureOilPressure, snap.Timestamp, "Oil pressure warning");
            logger.Log("EVENT: Oil pressure low");
            _oilFlagged = true;
        }

        if (snap.FuelPressureLow && !_fuelPrsFlagged)
        {
            LogEvent(FlightEventType.FailureFuelPressure, snap.Timestamp, "Fuel pressure warning");
            logger.Log("EVENT: Fuel pressure low");
            _fuelPrsFlagged = true;
        }

        if (snap.HydraulicPressureLow && !_hydFlagged)
        {
            LogEvent(FlightEventType.FailureHydraulic, snap.Timestamp, "Hydraulic pressure warning");
            logger.Log("EVENT: Hydraulic pressure low");
            _hydFlagged = true;
        }

        if (snap.LowVoltage && !_voltFlagged)
        {
            LogEvent(FlightEventType.FailureLowVoltage, snap.Timestamp, "Low voltage / electrical failure");
            logger.Log("EVENT: Low voltage");
            _voltFlagged = true;
        }

        if (snap.OverGFailure && !_overGFlagFlagged)
        {
            LogEvent(FlightEventType.FailureOverG, snap.Timestamp, $"Structural over-G damage ({snap.GForceNormal:F1}G)");
            logger.Log($"EVENT: Structural over-G ({snap.GForceNormal:F1}G)");
            _overGFlagFlagged = true;
        }

        if (snap.IceDamage > 0.01 && !_iceDamageFlagged)
        {
            LogEvent(FlightEventType.FailureIcingDamage, snap.Timestamp,
                $"Icing damage to airframe (frm_ice={snap.IceDamage:F3})");
            logger.Log($"EVENT: Icing damage (frm_ice={snap.IceDamage:F3})");
            _iceDamageFlagged = true;
        }
    }

    private void DetectTaxiEvents(FlightDataSnapshot snap, EventLogger logger)
    {
        double hdgRate = 0;
        if (_prevHdgDeg >= 0)
        {
            var delta = snap.MagHeadingDeg - _prevHdgDeg;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            hdgRate = Math.Abs(delta);
        }

        var lateralG = Math.Abs(snap.GForceLateral);

        if (!_fastTaxiFlagged && snap.GroundspeedKts > ScoringConfig.FastTaxiGsKts
            && (lateralG > ScoringConfig.FastTaxiLateralG || hdgRate > ScoringConfig.FastTaxiHdgRateDps))
        {
            LogEvent(FlightEventType.TaxiFastSpeed, snap.Timestamp,
                $"Fast taxi — {snap.GroundspeedKts:F0}kt GS, Gl={snap.GForceLateral:F2}G, HdgRate={hdgRate:F0}°/s");
            logger.Log($"EVENT: Fast taxi ({snap.GroundspeedKts:F0}kt, Gl={snap.GForceLateral:F2}G)");
            _fastTaxiFlagged = true;
        }

        if (!_aggressiveTurnFlagged && lateralG > ScoringConfig.AggrTaxiLateralG)
        {
            LogEvent(FlightEventType.TaxiAggressiveTurn, snap.Timestamp,
                $"Aggressive taxi turn — {snap.GForceLateral:F2}G lateral at {snap.GroundspeedKts:F0}kt");
            logger.Log($"EVENT: Aggressive taxi turn (Gl={snap.GForceLateral:F2}G at {snap.GroundspeedKts:F0}kt)");
            _aggressiveTurnFlagged = true;
        }
    }

    private void DetectTakeoffClimbEvents(FlightDataSnapshot snap, EventLogger logger)
    {
        if (!_hdgDeviationFlagged && _departureHdgDeg > 0)
        {
            var delta = snap.MagHeadingDeg - _departureHdgDeg;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            if (Math.Abs(delta) > ScoringConfig.TakeoffHdgDevDeg)
            {
                LogEvent(FlightEventType.TakeoffHeadingDeviation, snap.Timestamp,
                    $"Departure heading deviation — {Math.Abs(delta):F0}° from {_departureHdgDeg:F0}° (now {snap.MagHeadingDeg:F0}°) at {snap.AltitudeAglFt:F0}ft AGL");
                logger.Log($"EVENT: Heading deviation {Math.Abs(delta):F0}° from departure ({_departureHdgDeg:F0}°→{snap.MagHeadingDeg:F0}°, AGL={snap.AltitudeAglFt:F0}ft)");
                _hdgDeviationFlagged = true;
            }
        }

        if (!_takeoffSideloadFlagged && snap.AltitudeAglFt < 200
            && Math.Abs(snap.GForceLateral) > ScoringConfig.TakeoffSideloadG)
        {
            LogEvent(FlightEventType.TakeoffDirectionalControl, snap.Timestamp,
                $"Directional control — {snap.GForceLateral:F2}G lateral at {snap.AltitudeAglFt:F0}ft AGL");
            logger.Log($"EVENT: Takeoff directional control (Gl={snap.GForceLateral:F2}G, AGL={snap.AltitudeAglFt:F0}ft)");
            _takeoffSideloadFlagged = true;
        }
    }

    private void DetectApproachEvents(FlightDataSnapshot snap, EventLogger logger)
    {
        if (snap.AltitudeAglFt < 1000 && snap.VerticalSpeedFpm < -1500 && !_highDescentFlagged)
        {
            LogEvent(FlightEventType.HighDescentRate, snap.Timestamp, "Excessive descent rate on approach");
            logger.Log($"EVENT: High descent rate ({snap.VerticalSpeedFpm:F0} fpm below 1,000ft AGL)");
            _highDescentFlagged = true;
        }

        // Excessive approach speed — Vref×1.5 if known, else 175kt fallback
        var excessSpeedThreshold = _vrefKts > 0 ? _vrefKts * 1.5 : 175;
        if (snap.AltitudeAglFt < 1000 && snap.IndicatedAirspeedKts > excessSpeedThreshold && !_approachSpeedFlagged)
        {
            LogEvent(FlightEventType.ExcessiveApproachSpeed, snap.Timestamp,
                $"Excessive approach speed — {snap.IndicatedAirspeedKts:F0}kt below 1,000ft AGL (Vref={_vrefKts:F0}kt)");
            logger.Log($"EVENT: Excessive approach speed ({snap.IndicatedAirspeedKts:F0}kt, Vref={_vrefKts:F0}kt)");
            _approachSpeedFlagged = true;
        }

        // Unstable below 500ft — Vref×1.3 speed or -1000fpm VS; fallback 150kt
        var unstableSpeedThreshold = _vrefKts > 0 ? _vrefKts * 1.3 : 150;
        if (snap.AltitudeAglFt < 500 && !_unstableApproachFlagged
            && (snap.VerticalSpeedFpm < -1000 || snap.IndicatedAirspeedKts > unstableSpeedThreshold))
        {
            LogEvent(FlightEventType.UnstableApproach, snap.Timestamp,
                $"Unstable below 500ft — {snap.IndicatedAirspeedKts:F0}kt / {snap.VerticalSpeedFpm:F0} fpm");
            logger.Log($"EVENT: Unstable approach below 500ft ({snap.IndicatedAirspeedKts:F0}kt / {snap.VerticalSpeedFpm:F0} fpm)");
            _unstableApproachFlagged = true;
        }

    }

    private void LogEvent(FlightEventType type, DateTime ts, string description)
    {
        _events.Add(new FlightEvent
        {
            Type = type,
            Phase = _phase,
            TimestampSec = _elapsedSec,
            Description = description
        });
    }

    public CheckRideReport BuildReport()
    {
        var flightSec = _airborneTime != default
            ? (int)((_parkedTime != default ? _parkedTime : DateTime.UtcNow) - _airborneTime).TotalSeconds
            : 0;

        var report = new CheckRideReport
        {
            Aircraft  = _aircraft,
            RecordedAt = DateTime.UtcNow,
            Events    = new List<FlightEvent>(_events),
            Stats     = new FlightStats
            {
                FlightTimeSec              = flightSec,
                DistanceNm                 = Math.Round(_distanceNm, 1),
                AutopilotTimeSec           = _apTimeSec,
                AutopilotPct               = flightSec > 0 ? Math.Round(_apTimeSec * 100.0 / flightSec, 1) : 0,
                MaxAltitudeMslFt           = Math.Round(_maxAltMslFt, 0),
                MaxAltitudeAglFt           = Math.Round(_maxAltAglFt, 0),
                MaxIasKts                  = Math.Round(_maxIasKts, 1),
                MaxBankAngleDeg            = Math.Round(_maxBankDeg, 1),
                MaxGForceNormal            = Math.Round(_maxGNormal, 2),
                MaxGForceLateral           = Math.Round(_maxGLateral, 2),
                AvgFinalDescentRateFpm     = _finalDescentSamples > 0
                                                ? Math.Round(_finalDescentSum / _finalDescentSamples, 0)
                                                : 0,
                LandingVsFpm               = Math.Round(_landingVsFpm, 0),
                LandingLateralG            = Math.Round(_landingLateralG, 2),
                WindSpeedAtLandingKt       = Math.Round(_windSpeedAtLanding, 1),
                WindDirectionAtLandingDeg  = Math.Round(_windDirAtLanding, 0),
                GoArounds                  = _goArounds,
                VsoKts                     = Math.Round(_vsoKts, 1),
                VrefKts                    = Math.Round(_vrefKts, 1),
                VnoKts                     = Math.Round(_vnoKts, 1),
                VneKts                     = Math.Round(_vneKts, 1),
                VfeKts                     = Math.Round(_vfeKts, 1),
                VleKts                     = Math.Round(_vleKts, 1)
            }
        };

        report.Summary = BuildSummary(report);
        report.Score   = CalculateScore(report);
        report.Grade   = ScoreToGrade(report.Score, report.Summary);
        report.Conditions = new List<FlightConditionSample>(_conditions);
        report.Track      = new List<FlightTrackPoint>(_track);

        return report;
    }

    private void SampleConditions(FlightDataSnapshot snap)
    {
        // Skip snapshots during sim loading (WindSpeedKt = -1 while loading)
        if (snap.WindSpeedKt < 0 || snap.AltitudeAglFt < -5) return;

        var elapsed = (snap.Timestamp - _lastConditionSample).TotalSeconds;
        if (_lastConditionSample != DateTime.MinValue && elapsed < ConditionIntervalSec) return;

        _conditions.Add(new FlightConditionSample
        {
            T                = _elapsedSec,
            LocalTimeSec     = (int)snap.LocalTimeSec,
            IsNight          = snap.SunPitchDeg < 0,
            WindSpeedKt      = Math.Round(snap.WindSpeedKt, 1),
            WindDirectionDeg = Math.Round(snap.WindDirectionDeg, 0),
            VisibilityM      = Math.Round(snap.VisibilityM, 0),
            CloudBaseAglFt   = Math.Round(snap.CloudBaseAglM * 3.28084, 0),
            CloudCoverage    = Math.Round(snap.CloudCoverage, 2),
            OatC             = Math.Round(snap.OutsideAirTempC, 1)
        });
        _lastConditionSample = snap.Timestamp;
    }

    private void SampleTrack(FlightDataSnapshot snap, bool forced = false)
    {
        // Skip snapshots during sim loading
        if (snap.WindSpeedKt < 0 || snap.AltitudeAglFt < -5) return;

        var elapsed = (snap.Timestamp - _lastTrackSample).TotalSeconds;
        if (!forced && elapsed < TrackIntervalSec) return;

        _track.Add(new FlightTrackPoint
        {
            T        = _elapsedSec,
            Lat      = Math.Round(snap.Latitude, 5),
            Lon      = Math.Round(snap.Longitude, 5),
            AltMslFt = (int)snap.AltitudeMslFt,
            AltAglFt = (int)snap.AltitudeAglFt,
            IasKts   = (int)snap.IndicatedAirspeedKts,
            GsKts    = (int)snap.GroundspeedKts,
            VsFpm    = (int)snap.VerticalSpeedFpm,
            HdgDeg   = (int)snap.MagHeadingDeg,
            Phase    = _phase.ToString()
        });
        _lastTrackSample = snap.Timestamp;
    }

    private static CheckRideSummary BuildSummary(CheckRideReport r)
    {
        var absVs = Math.Abs(r.Stats.LandingVsFpm);
        return new CheckRideSummary
        {
            OverspeedCount = r.Events.Count(e => e.Type == FlightEventType.Overspeed),
            StallCount     = r.Events.Count(e => e.Type == FlightEventType.Stall),
            HighGCount     = r.Events.Count(e => e.Type is FlightEventType.HighG or FlightEventType.VeryHighG),
            SystemFlags    = r.Events.Count(e => e.Type is
                FlightEventType.SystemPitotHeat or FlightEventType.SystemFlapsCruise or
                FlightEventType.SystemGearCruise or FlightEventType.SystemGearApproach or
                FlightEventType.SystemLandingLights or FlightEventType.SystemBeacon or
                FlightEventType.SystemStrobes or FlightEventType.SystemTransponder or
                FlightEventType.SystemAntiIce or FlightEventType.SystemBarometer),
            ImcFlight      = r.Events.Any(e => e.Type == FlightEventType.SystemIMC),
            TakeoffFlags   = r.Events.Count(e => e.Type is
                FlightEventType.TakeoffLowPower or FlightEventType.TakeoffHeadingDeviation or
                FlightEventType.TakeoffDirectionalControl or FlightEventType.WrongDepartureAirport),
            TaxiFlags      = r.Events.Count(e => e.Type is
                FlightEventType.TaxiFastSpeed or FlightEventType.TaxiAggressiveTurn),
            FailureCount   = r.Events.Count(e => e.Type is
                FlightEventType.FailureEngineFire or FlightEventType.FailureEngineOut or
                FlightEventType.FailureEngineOverspeed or
                FlightEventType.FailureOilPressure or FlightEventType.FailureFuelPressure or
                FlightEventType.FailureHydraulic or FlightEventType.FailureLowVoltage or
                FlightEventType.FailureOverG or FlightEventType.FailureIcingDamage),
            Crashed        = r.Events.Any(e => e.Type == FlightEventType.Crash),
            RunwayExcursion = r.Events.Any(e => e.Type == FlightEventType.RunwayExcursion),
            LandingQuality = absVs < 75 ? "Greaser"
                : absVs < 150 ? "Smooth"
                : absVs < 300 ? "Normal"
                : absVs < 600 ? "Firm"
                : "Hard"
        };
    }

    private static int CalculateScore(CheckRideReport r)
    {
        if (r.Stats.FlightTimeSec == 0) return 0;

        int score = 100;
        var bd = new List<ScoreLineItem>();

        // Adds a per-occurrence penalty (only recorded if count > 0)
        void PerOccurrence(string label, int count, int pts)
        {
            if (count == 0) return;
            int delta = pts * count;
            score += delta;
            bd.Add(new ScoreLineItem { Label = label, Count = count, Pts = delta });
        }

        // Adds a one-shot penalty (only recorded if it occurred)
        void Once(string label, bool applies, int pts)
        {
            if (!applies) return;
            score += pts;
            bd.Add(new ScoreLineItem { Label = label, Count = 1, Pts = pts });
        }

        // Speed / stall
        PerOccurrence("Overspeed",             r.Events.Count(e => e.Type == FlightEventType.Overspeed),          ScoringConfig.PenaltyOverspeed);
        PerOccurrence("250 kt below 10,000 ft",r.Events.Count(e => e.Type == FlightEventType.SpeedLimitViolation),ScoringConfig.PenaltySpeedLimit);
        PerOccurrence("Stall",                 r.Events.Count(e => e.Type == FlightEventType.Stall),              ScoringConfig.PenaltyStall);

        // G / bank
        PerOccurrence("High G-Force",          r.Events.Count(e => e.Type == FlightEventType.HighG),              ScoringConfig.PenaltyHighG);
        PerOccurrence("Very High G-Force",     r.Events.Count(e => e.Type == FlightEventType.VeryHighG),          ScoringConfig.PenaltyVeryHighG);
        PerOccurrence("Excessive Bank",        r.Events.Count(e => e.Type == FlightEventType.HighBank),           ScoringConfig.PenaltyHighBank);
        PerOccurrence("Very Excessive Bank",   r.Events.Count(e => e.Type == FlightEventType.VeryHighBank),       ScoringConfig.PenaltyVeryHighBank);
        PerOccurrence("High Descent Rate",     r.Events.Count(e => e.Type == FlightEventType.HighDescentRate),    ScoringConfig.PenaltyHighDescentRate);

        // Crash / excursion
        Once("Crash",                          r.Events.Any(e => e.Type == FlightEventType.Crash),               ScoringConfig.PenaltyCrash);
        Once("Runway Excursion",               r.Events.Any(e => e.Type == FlightEventType.RunwayExcursion),     ScoringConfig.PenaltyRunwayExcursion);

        // Approach
        Once("Excessive Approach Speed",       r.Events.Any(e => e.Type == FlightEventType.ExcessiveApproachSpeed), ScoringConfig.PenaltyExcessApproachSpd);
        Once("Unstable Approach",              r.Events.Any(e => e.Type == FlightEventType.UnstableApproach),     ScoringConfig.PenaltyUnstableApproach);
        Once("Flap Overspeed",                 r.Events.Any(e => e.Type == FlightEventType.FlapOverspeed),        ScoringConfig.PenaltyFlapOverspeed);

        // Landing
        Once("Hard Landing",                   r.Events.Any(e => e.Type == FlightEventType.HardLanding),          ScoringConfig.PenaltyHardLanding);
        Once("Fast Landing (IAS)",             r.Events.Any(e => e.Type == FlightEventType.FastLanding),          ScoringConfig.PenaltyFastLanding);
        Once("Firm Landing",                   r.Events.Any(e => e.Type == FlightEventType.FirmLanding),          ScoringConfig.PenaltyFirmLanding);
        Once("Gear Up Landing",                r.Events.Any(e => e.Type == FlightEventType.GearUpLanding),        ScoringConfig.PenaltyGearUpLanding);
        Once("Sideload Landing",               r.Events.Any(e => e.Type == FlightEventType.SideloadLanding),      ScoringConfig.PenaltySideloadLanding);

        // Systems
        PerOccurrence("System Check Failures", r.Summary.SystemFlags,                                             ScoringConfig.PenaltySystemFlag);

        // Taxi
        PerOccurrence("Fast Taxi",             r.Events.Count(e => e.Type == FlightEventType.TaxiFastSpeed),      ScoringConfig.PenaltyTaxiFastSpeed);
        PerOccurrence("Aggressive Taxi Turn",  r.Events.Count(e => e.Type == FlightEventType.TaxiAggressiveTurn), ScoringConfig.PenaltyTaxiAggrTurn);

        // Takeoff
        Once("Takeoff Low Power",              r.Events.Any(e => e.Type == FlightEventType.TakeoffLowPower),      ScoringConfig.PenaltyTakeoffLowPower);
        Once("Takeoff Hdg Deviation",          r.Events.Any(e => e.Type == FlightEventType.TakeoffHeadingDeviation), ScoringConfig.PenaltyTakeoffHdgDev);
        Once("Takeoff Directional Control",    r.Events.Any(e => e.Type == FlightEventType.TakeoffDirectionalControl), ScoringConfig.PenaltyTakeoffSideload);
        Once("Wrong Departure Airport",        r.Events.Any(e => e.Type == FlightEventType.WrongDepartureAirport), ScoringConfig.PenaltyWrongDepartureAirport);

        // Aircraft failures
        Once("Engine Fire",                    r.Events.Any(e => e.Type == FlightEventType.FailureEngineFire),    ScoringConfig.PenaltyEngineFire);
        Once("Engine Out",                     r.Events.Any(e => e.Type == FlightEventType.FailureEngineOut),     ScoringConfig.PenaltyEngineOut);
        Once("Engine Overspeed",               r.Events.Any(e => e.Type == FlightEventType.FailureEngineOverspeed), ScoringConfig.PenaltyEngineOverspeed);
        Once("Over-G Structural Damage",       r.Events.Any(e => e.Type == FlightEventType.FailureOverG),         ScoringConfig.PenaltyOverGFailure);
        Once("Icing Damage",                   r.Events.Any(e => e.Type == FlightEventType.FailureIcingDamage),   ScoringConfig.PenaltyIcingDamage);
        PerOccurrence("System Failure",        r.Events.Count(e => e.Type is
            FlightEventType.FailureOilPressure or FlightEventType.FailureFuelPressure or
            FlightEventType.FailureHydraulic or FlightEventType.FailureLowVoltage),                               ScoringConfig.PenaltySystemFailure);

        // Bonuses
        var absVs = Math.Abs(r.Stats.LandingVsFpm);
        if (!r.Summary.Crashed && !r.Summary.RunwayExcursion)
        {
            if (absVs < 75)       { score += ScoringConfig.BonusGreaser; bd.Add(new ScoreLineItem { Label = "Greaser Landing Bonus", Count = 1, Pts = ScoringConfig.BonusGreaser }); }
            else if (absVs < 150) { score += ScoringConfig.BonusSmooth;  bd.Add(new ScoreLineItem { Label = "Smooth Landing Bonus",  Count = 1, Pts = ScoringConfig.BonusSmooth  }); }
        }
        if (r.Summary.SystemFlags == 0)
        {
            score += ScoringConfig.BonusCleanSystems;
            bd.Add(new ScoreLineItem { Label = "Clean Systems Bonus", Count = 1, Pts = ScoringConfig.BonusCleanSystems });
        }
        if (r.Summary.OverspeedCount == 0 && r.Summary.StallCount == 0)
        {
            score += ScoringConfig.BonusCleanFlight;
            bd.Add(new ScoreLineItem { Label = "Clean Flight Bonus", Count = 1, Pts = ScoringConfig.BonusCleanFlight });
        }

        r.Breakdown = bd;
        return Math.Clamp(score, 0, 100);
    }

    private static string ScoreToGrade(int score, CheckRideSummary summary)
    {
        if (summary.Crashed || summary.RunwayExcursion) return "F";
        return score switch
        {
            >= 95 => "S",
            >= 85 => "A",
            >= 70 => "B",
            >= 50 => "C",
            >= 30 => "D",
            _     => "F"
        };
    }

    private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3440.065; // Earth radius in NM
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
