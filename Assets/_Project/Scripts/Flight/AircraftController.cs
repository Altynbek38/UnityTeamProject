using BushPilot.Input;
using UnityEngine;

namespace BushPilot.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FlightInputAdapter))]
    public class AircraftController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AircraftStats _stats;
        [SerializeField] private Transform _propellerTransform;
        [SerializeField] private Transform _visualModelRoot;

        [Header("Animated Control Surfaces")]
        [SerializeField] private Transform _leftAileron;
        [SerializeField] private Transform _rightAileron;
        [SerializeField] private Transform _elevator;
        [SerializeField] private Transform _rudder;
        [SerializeField] private float _maxAileronDeflection = 16f;
        [SerializeField] private float _maxElevatorDeflection = 16f;
        [SerializeField] private float _maxRudderDeflection = 20f;

        private Quaternion _leftAileronBaseRot;
        private Quaternion _rightAileronBaseRot;
        private Quaternion _elevatorBaseRot;
        private Quaternion _rudderBaseRot;
        private bool _controlSurfacesInitialized;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 2.1f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        private Rigidbody _rb;
        private FlightInputAdapter _input;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        // Telemetry for HUD & LandingDetector
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => ForwardSpeed * 3.6f;
        public float VerticalSpeed { get; private set; }
        public float Altitude { get; private set; }
        public float CurrentRollAngle { get; private set; }
        public float CurrentPitchAngle { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCrashed { get; private set; }

        public AircraftStats Stats => _stats;
        public FlightInputAdapter Input => _input;
        public Rigidbody Rb => _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<FlightInputAdapter>();

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            ConfigureRigidbody();
        }

        private void Start()
        {
            DisableImportedAnimators();
            WrapAndCacheControlSurfaces();
        }

        private void DisableImportedAnimators()
        {
            // Prevent any Animator/Animation inside imported FBX models from fighting our Transform rotations!
            foreach (Animator anim in GetComponentsInChildren<Animator>(true))
            {
                anim.enabled = false;
            }
            foreach (Animation legacyAnim in GetComponentsInChildren<Animation>(true))
            {
                legacyAnim.enabled = false;
            }
        }

        private void WrapAndCacheControlSurfaces()
        {
            if (_controlSurfacesInitialized) return;
            _controlSurfacesInitialized = true;

            _propellerTransform = EnsureCenteredPivot(_propellerTransform, "Propeller_AutoPivot");
            _leftAileron = EnsureCenteredPivot(_leftAileron, "LeftAileron_AutoPivot");
            _rightAileron = EnsureCenteredPivot(_rightAileron, "RightAileron_AutoPivot");
            _elevator = EnsureCenteredPivot(_elevator, "Elevator_AutoPivot");
            _rudder = EnsureCenteredPivot(_rudder, "Rudder_AutoPivot");

            if (_leftAileron != null) _leftAileronBaseRot = _leftAileron.localRotation;
            if (_rightAileron != null) _rightAileronBaseRot = _rightAileron.localRotation;
            if (_elevator != null) _elevatorBaseRot = _elevator.localRotation;
            if (_rudder != null) _rudderBaseRot = _rudder.localRotation;
        }

        private Transform EnsureCenteredPivot(Transform part, string pivotName)
        {
            if (part == null) return null;

            // If already wrapped in a pivot without a Renderer, use it directly
            Renderer r = part.GetComponent<Renderer>();
            if (r == null) return part;

            GameObject pivot = new GameObject(pivotName);
            pivot.transform.SetParent(part.parent, true);
            pivot.transform.position = r.bounds.center;
            pivot.transform.rotation = transform.rotation;
            part.SetParent(pivot.transform, true);
            return pivot.transform;
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = 12f;
            _rb.linearDamping = 0.25f;
            _rb.angularDamping = 2.2f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (_stats != null)
            {
                _rb.maxAngularVelocity = _stats.maxAngularVelocity;
            }
        }

        private void Update()
        {
            if (_input != null && _input.ResetPressed)
            {
                ResetAircraft();
            }

            AnimatePropeller();
            AnimateControlSurfaces();
        }

        private void FixedUpdate()
        {
            if (_stats == null || IsCrashed)
            {
                return;
            }

            UpdateTelemetry();
            ApplyThrustAndDrag();
            ApplyAerodynamicLift();
            ApplySteeringTorque();
        }

        private void UpdateTelemetry()
        {
            Vector3 velocity = _rb.linearVelocity;
            ForwardSpeed = Mathf.Max(0f, Vector3.Dot(velocity, transform.forward));
            VerticalSpeed = velocity.y;
            Altitude = transform.position.y;

            // Signed roll & pitch angles (-180..180)
            Vector3 euler = transform.eulerAngles;
            CurrentPitchAngle = NormalizeAngle(euler.x);
            CurrentRollAngle = NormalizeAngle(euler.z);

            // Raycast ground check ignoring self colliders
            IsGrounded = Physics.Raycast(
                transform.position + Vector3.up * 0.4f,
                Vector3.down,
                out RaycastHit hit,
                _groundCheckDistance + 0.4f,
                _groundLayers,
                QueryTriggerInteraction.Ignore) && hit.rigidbody != _rb;
        }

        private void ApplyThrustAndDrag()
        {
            // 1. Forward Engine Thrust
            float throttle = _input != null ? _input.Throttle : 0f;
            if (ForwardSpeed < _stats.maxSpeed)
            {
                float speedRatio = Mathf.Clamp01(ForwardSpeed / _stats.maxSpeed);
                float thrustFactor = Mathf.Lerp(1f, 0.2f, speedRatio * speedRatio);
                _rb.AddForce(transform.forward * (throttle * _stats.maxThrust * thrustFactor), ForceMode.Force);
            }

            // 2. Sideways Velocity Damping (prevents icy sliding in turns)
            Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
            localVel.x = Mathf.Lerp(localVel.x, 0f, _stats.sideDrag * Time.fixedDeltaTime);

            // 3. Wheel Brakes on Ground
            if (IsGrounded && _input != null && _input.IsBraking)
            {
                localVel.z = Mathf.MoveTowards(localVel.z, 0f, _stats.groundBrakeDrag * 8f * Time.fixedDeltaTime);
            }

            _rb.linearVelocity = transform.TransformDirection(localVel);
        }

        private void ApplyAerodynamicLift()
        {
            // Lift grows smoothly with forward speed up to takeoff speed
            float liftFactor = Mathf.InverseLerp(3f, _stats.takeoffSpeed, ForwardSpeed);
            if (liftFactor <= 0.01f)
            {
                return;
            }

            // Reduce lift if plane is upside down or steeply banked
            float uprightFactor = Mathf.Clamp01(Vector3.Dot(transform.up, Vector3.up));
            float gravityCompensation = Physics.gravity.magnitude * _rb.mass;

            // At takeoffSpeed and level flight, lift counters gravity + responds to pitch
            float liftForce = gravityCompensation * liftFactor * (0.88f + 0.28f * _stats.liftPower) * uprightFactor;
            _rb.AddForce(transform.up * liftForce, ForceMode.Force);
        }

        private void ApplySteeringTorque()
        {
            if (_input == null)
            {
                return;
            }

            // Control surfaces need airflow (keep mild steering on runway for taxiing)
            float airflowAuthority = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(2f, _stats.takeoffSpeed, ForwardSpeed));

            // Pitch: positive _input.Pitch (W / Stick Forward) pushes nose DOWN (+X rotation in Unity)
            // Negative _input.Pitch (S / Pull Stick Back) pulls nose UP (-X rotation in Unity)
            float pitchTorque = _input.Pitch * _stats.pitchTorque * airflowAuthority;

            // Roll: positive _input.Roll banks RIGHT (negative Z rotation in Unity)
            float rollTorque = -_input.Roll * _stats.rollTorque * airflowAuthority;

            // Yaw: positive _input.Yaw turns nose RIGHT (positive Y rotation)
            float yawTorque = _input.Yaw * _stats.yawTorque;

            // Coordinated turn: banking slightly yaws the aircraft naturally in the direction of the bank
            if (!IsGrounded)
            {
                float bankYaw = -Mathf.Sin(CurrentRollAngle * Mathf.Deg2Rad) * (_stats.yawTorque * 0.45f) * airflowAuthority;
                yawTorque += bankYaw;
            }

            _rb.AddRelativeTorque(new Vector3(pitchTorque, yawTorque, rollTorque), ForceMode.Force);

            // Gentle auto-leveling on roll when pilot releases Roll stick
            if (Mathf.Abs(_input.Roll) < 0.05f)
            {
                float rollCorrection = -CurrentRollAngle * 0.45f * _stats.autoStabilization;
                _rb.AddRelativeTorque(new Vector3(0f, 0f, rollCorrection), ForceMode.Force);
            }
        }

        private void AnimatePropeller()
        {
            if (_propellerTransform == null || IsCrashed)
            {
                return;
            }

            float throttle = _input != null ? _input.Throttle : 0f;
            float rpmSpeed = Mathf.Lerp(450f, 2600f, throttle);
            _propellerTransform.Rotate(Vector3.forward, rpmSpeed * Time.deltaTime, Space.Self);
        }

        private void AnimateControlSurfaces()
        {
            if (_input == null || IsCrashed)
            {
                return;
            }

            float dt = 12f * Time.deltaTime;

            // Left & Right Ailerons (opposite deflection on Roll around their own local hinge!)
            if (_leftAileron != null)
            {
                Quaternion target = _leftAileronBaseRot * Quaternion.Euler(_input.Roll * _maxAileronDeflection, 0f, 0f);
                _leftAileron.localRotation = Quaternion.Slerp(_leftAileron.localRotation, target, dt);
            }
            if (_rightAileron != null)
            {
                Quaternion target = _rightAileronBaseRot * Quaternion.Euler(-_input.Roll * _maxAileronDeflection, 0f, 0f);
                _rightAileron.localRotation = Quaternion.Slerp(_rightAileron.localRotation, target, dt);
            }

            // Elevator on horizontal tail (deflects on Pitch)
            if (_elevator != null)
            {
                Quaternion target = _elevatorBaseRot * Quaternion.Euler(_input.Pitch * _maxElevatorDeflection, 0f, 0f);
                _elevator.localRotation = Quaternion.Slerp(_elevator.localRotation, target, dt);
            }

            // Rudder on vertical fin (deflects on Yaw)
            if (_rudder != null)
            {
                Quaternion target = _rudderBaseRot * Quaternion.Euler(0f, -_input.Yaw * _maxRudderDeflection, 0f);
                _rudder.localRotation = Quaternion.Slerp(_rudder.localRotation, target, dt);
            }
        }

        public void TriggerCrash()
        {
            if (IsCrashed)
            {
                return;
            }

            IsCrashed = true;
            if (_input != null)
            {
                _input.SetThrottleImmediate(0f);
            }
        }

        public void ResetAircraft()
        {
            IsCrashed = false;
            if (_input != null)
            {
                _input.SetThrottleImmediate(0f);
            }

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);

            LandingDetector detector = GetComponent<LandingDetector>();
            if (detector != null)
            {
                detector.ResetLandingState();
            }
        }

        public void SetReferences(
            AircraftStats stats,
            Transform propeller,
            Transform visualRoot,
            Transform leftAileron = null,
            Transform rightAileron = null,
            Transform elevator = null,
            Transform rudder = null)
        {
            _stats = stats;
            _propellerTransform = propeller;
            _visualModelRoot = visualRoot;
            _leftAileron = leftAileron;
            _rightAileron = rightAileron;
            _elevator = elevator;
            _rudder = rudder;
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
