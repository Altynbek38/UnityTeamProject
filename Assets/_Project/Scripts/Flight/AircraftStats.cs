using UnityEngine;

namespace BushPilot.Flight
{
    [CreateAssetMenu(fileName = "DefaultAircraftStats", menuName = "Bush Pilot/Aircraft Stats")]
    public class AircraftStats : ScriptableObject
    {
        [Header("Engine & Speed")]
        [Tooltip("Maximum forward thrust force applied at 100% throttle.")]
        public float maxThrust = 220f;

        [Tooltip("Minimum airspeed (m/s) required before aerodynamic lift keeps the aircraft airborne.")]
        public float takeoffSpeed = 14f;

        [Tooltip("Maximum forward airspeed (m/s).")]
        public float maxSpeed = 55f;

        [Tooltip("How quickly throttle changes when using keyboard R/F keys.")]
        public float throttleChangeRate = 0.45f;

        [Header("Aerodynamics & Lift")]
        [Tooltip("Lift force multiplier based on forward speed.")]
        public float liftPower = 0.65f;

        [Tooltip("Sideways velocity damping to prevent drifting in turns.")]
        public float sideDrag = 2.8f;

        [Tooltip("Braking drag applied when wheel brakes are held on the ground.")]
        public float groundBrakeDrag = 3.5f;

        [Header("Rotational Controls (Torque)")]
        [Tooltip("Pitch responsiveness (nose up / down).")]
        public float pitchTorque = 36f;

        [Tooltip("Roll responsiveness (banking left / right).")]
        public float rollTorque = 48f;

        [Tooltip("Yaw responsiveness (rudder left / right).")]
        public float yawTorque = 20f;

        [Tooltip("Automatic banking into turns and gentle roll/pitch stabilization.")]
        public float autoStabilization = 1.2f;

        [Tooltip("Maximum angular velocity (rad/s) to prevent uncontrollable spins.")]
        public float maxAngularVelocity = 2.5f;

        [Header("Landing Evaluation Thresholds")]
        [Tooltip("Max vertical descent speed (m/s) for a Perfect landing.")]
        public float perfectMaxVerticalSpeed = 2.2f;

        [Tooltip("Max vertical descent speed (m/s) for a Good landing.")]
        public float goodMaxVerticalSpeed = 4.5f;

        [Tooltip("Max vertical descent speed (m/s) for a Hard landing. Above this triggers Crash.")]
        public float hardMaxVerticalSpeed = 7.5f;

        [Tooltip("Max roll/pitch angle (degrees) allowed on touchdown for Perfect landing.")]
        public float perfectMaxAngle = 7f;

        [Tooltip("Max roll/pitch angle (degrees) allowed on touchdown before Crash.")]
        public float maxSafeLandingAngle = 22f;

        [Tooltip("Forward speed (m/s) below which the aircraft is considered safely stopped on the runway.")]
        public float fullStopSpeedThreshold = 2.0f;
    }
}
