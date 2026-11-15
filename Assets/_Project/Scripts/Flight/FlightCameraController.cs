using BushPilot.Input;
using UnityEngine;

namespace BushPilot.Flight
{
    public class FlightCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private FlightInputAdapter _inputAdapter;

        [Header("Camera Presets (Chase / Far / Cockpit-Hood)")]
        [SerializeField] private Vector3[] _cameraOffsets = new Vector3[]
        {
            new Vector3(0f, 2.35f, -7.8f),
            new Vector3(0f, 3.8f, -12.5f),
            new Vector3(0f, 1.15f, 0.6f)
        };

        [SerializeField] private float _positionSmoothSpeed = 9f;
        [SerializeField] private float _rotationSmoothSpeed = 7f;

        private int _currentPresetIndex;

        public string CurrentViewName => _currentPresetIndex switch
        {
            0 => "Chase Cam (Close)",
            1 => "Chase Cam (Wide)",
            _ => "Cockpit / Hood View"
        };

        private void Start()
        {
            if (_target == null)
            {
                AircraftController aircraft = FindFirstObjectByType<AircraftController>();
                if (aircraft != null)
                {
                    _target = aircraft.transform;
                    _inputAdapter = aircraft.Input;
                }
            }
        }

        private void Update()
        {
            if (_inputAdapter != null && _inputAdapter.CameraSwitchPressed)
            {
                _currentPresetIndex = (_currentPresetIndex + 1) % _cameraOffsets.Length;
            }
        }

        private void LateUpdate()
        {
            if (_target == null || _cameraOffsets == null || _cameraOffsets.Length == 0)
            {
                return;
            }

            Vector3 offset = _cameraOffsets[_currentPresetIndex];

            if (_currentPresetIndex == 2)
            {
                transform.position = _target.TransformPoint(offset);
                transform.rotation = _target.rotation;
                return;
            }

            // Exact original smooth chase camera in LateUpdate
            Vector3 desiredPosition = _target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, _positionSmoothSpeed * Time.deltaTime);

            Vector3 lookPoint = _target.position + _target.forward * 14f + Vector3.up * 1.0f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, _rotationSmoothSpeed * Time.deltaTime);
        }

        public void SetTarget(Transform target, FlightInputAdapter inputAdapter)
        {
            _target = target;
            _inputAdapter = inputAdapter;
        }
    }
}
