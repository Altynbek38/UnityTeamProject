using System;
using UnityEngine;

namespace BushPilot.Flight
{
    public enum LandingGrade
    {
        None = 0,
        Perfect = 1,
        Good = 2,
        Hard = 3,
        Crash = 4
    }

    /// <summary>
    /// Marker component placed on Runway / Landing Zone colliders.
    /// </summary>
    public class LandingZone : MonoBehaviour
    {
        [Tooltip("Display name of this airstrip or runway.")]
        public string runwayName = "Main Bush Airstrip";

        [Tooltip("Whether this runway is the target destination for the active mission.")]
        public bool isDestinationRunway = true;
    }

    /// <summary>
    /// Evaluates touchdown vertical speed, bank/pitch angle, and subsequent full stop on a LandingZone.
    /// </summary>
    [RequireComponent(typeof(AircraftController))]
    public class LandingDetector : MonoBehaviour
    {
        private AircraftController _aircraft;

        public LandingGrade LastTouchdownGrade { get; private set; } = LandingGrade.None;
        public LandingGrade FinalConfirmedGrade { get; private set; } = LandingGrade.None;
        public float LastTouchdownVerticalSpeed { get; private set; }
        public float LastTouchdownAngle { get; private set; }
        public bool IsOnLandingZone { get; private set; }
        public LandingZone CurrentLandingZone { get; private set; }

        public event Action<LandingGrade, float, float> OnTouchdownEvaluated;
        public event Action<LandingGrade, LandingZone> OnLandingConfirmed;

        private bool _hasLeftGroundSinceSpawn;
        private float _airborneTimer;

        private void Awake()
        {
            _aircraft = GetComponent<AircraftController>();
        }

        private void Update()
        {
            if (_aircraft == null || _aircraft.IsCrashed)
            {
                return;
            }

            if (!_aircraft.IsGrounded)
            {
                _airborneTimer += Time.deltaTime;
                if (_airborneTimer > 1.2f)
                {
                    _hasLeftGroundSinceSpawn = true;
                }
            }
            else
            {
                _airborneTimer = 0f;
            }

            // Confirm completed landing once aircraft slows down to a stop on the runway after touchdown
            if (_hasLeftGroundSinceSpawn &&
                IsOnLandingZone &&
                LastTouchdownGrade != LandingGrade.None &&
                LastTouchdownGrade != LandingGrade.Crash &&
                FinalConfirmedGrade == LandingGrade.None)
            {
                float stopThreshold = _aircraft.Stats != null ? _aircraft.Stats.fullStopSpeedThreshold : 2.0f;
                if (_aircraft.ForwardSpeed <= stopThreshold)
                {
                    FinalConfirmedGrade = LastTouchdownGrade;
                    OnLandingConfirmed?.Invoke(FinalConfirmedGrade, CurrentLandingZone);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_aircraft == null || _aircraft.IsCrashed)
            {
                return;
            }

            LandingZone zone = collision.collider.GetComponentInParent<LandingZone>();
            float impactVerticalSpeed = Mathf.Abs(collision.relativeVelocity.y);
            float maxTilt = Mathf.Max(Mathf.Abs(_aircraft.CurrentRollAngle), Mathf.Abs(_aircraft.CurrentPitchAngle));

            AircraftStats stats = _aircraft.Stats;
            float hardSpeedLimit = stats != null ? stats.hardMaxVerticalSpeed : 7.5f;
            float safeAngleLimit = stats != null ? stats.maxSafeLandingAngle : 22f;

            // If hitting non-runway terrain at high speed or steep angle after takeoff -> Crash
            if (zone == null)
            {
                if (_hasLeftGroundSinceSpawn && (impactVerticalSpeed > hardSpeedLimit || maxTilt > safeAngleLimit || collision.relativeVelocity.magnitude > 16f))
                {
                    RegisterCrash(impactVerticalSpeed, maxTilt);
                }
                return;
            }

            IsOnLandingZone = true;
            CurrentLandingZone = zone;

            if (!_hasLeftGroundSinceSpawn)
            {
                return;
            }

            LastTouchdownVerticalSpeed = impactVerticalSpeed;
            LastTouchdownAngle = maxTilt;

            if (impactVerticalSpeed > hardSpeedLimit || maxTilt > safeAngleLimit)
            {
                RegisterCrash(impactVerticalSpeed, maxTilt);
                return;
            }

            float perfectSpeed = stats != null ? stats.perfectMaxVerticalSpeed : 2.2f;
            float goodSpeed = stats != null ? stats.goodMaxVerticalSpeed : 4.5f;
            float perfectAngle = stats != null ? stats.perfectMaxAngle : 7f;

            if (impactVerticalSpeed <= perfectSpeed && maxTilt <= perfectAngle)
            {
                LastTouchdownGrade = LandingGrade.Perfect;
            }
            else if (impactVerticalSpeed <= goodSpeed)
            {
                LastTouchdownGrade = LandingGrade.Good;
            }
            else
            {
                LastTouchdownGrade = LandingGrade.Hard;
            }

            OnTouchdownEvaluated?.Invoke(LastTouchdownGrade, impactVerticalSpeed, maxTilt);
        }

        private void OnCollisionStay(Collision collision)
        {
            LandingZone zone = collision.collider.GetComponentInParent<LandingZone>();
            if (zone != null)
            {
                IsOnLandingZone = true;
                CurrentLandingZone = zone;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            LandingZone zone = collision.collider.GetComponentInParent<LandingZone>();
            if (zone != null && zone == CurrentLandingZone)
            {
                IsOnLandingZone = false;
                CurrentLandingZone = null;
            }
        }

        private void RegisterCrash(float verticalSpeed, float tiltAngle)
        {
            LastTouchdownVerticalSpeed = verticalSpeed;
            LastTouchdownAngle = tiltAngle;
            LastTouchdownGrade = LandingGrade.Crash;
            FinalConfirmedGrade = LandingGrade.Crash;

            _aircraft.TriggerCrash();
            OnTouchdownEvaluated?.Invoke(LandingGrade.Crash, verticalSpeed, tiltAngle);
        }

        public void ResetLandingState()
        {
            LastTouchdownGrade = LandingGrade.None;
            FinalConfirmedGrade = LandingGrade.None;
            LastTouchdownVerticalSpeed = 0f;
            LastTouchdownAngle = 0f;
            _hasLeftGroundSinceSpawn = false;
            _airborneTimer = 0f;
        }
    }
}
