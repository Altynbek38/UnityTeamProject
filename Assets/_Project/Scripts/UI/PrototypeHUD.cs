using BushPilot.Flight;
using BushPilot.Input;
using BushPilot.Missions;
using UnityEngine;

namespace BushPilot.UI
{
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private AircraftController _aircraft;
        [SerializeField] private FlightInputAdapter _input;
        [SerializeField] private MissionManager _missionManager;

        private GUIStyle _panelStyle;
        private GUIStyle _textStyle;

        private void Start()
        {
            if (_aircraft == null) _aircraft = FindFirstObjectByType<AircraftController>();
            if (_aircraft != null && _input == null) _input = _aircraft.GetComponent<FlightInputAdapter>();
            if (_missionManager == null) _missionManager = FindFirstObjectByType<MissionManager>();
        }

        private void OnGUI()
        {
            if (_aircraft == null) return;

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(12, 12, 10, 10)
                };
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }

            float throttle = (_input != null ? _input.Throttle : 0f) * 100f;
            string cpText = _missionManager != null
                ? $"{_missionManager.CurrentCheckpointIndex} / {_missionManager.TotalCheckpoints}"
                : "0 / 0";
            string status = _missionManager != null ? _missionManager.StatusMessage : string.Empty;

            GUILayout.BeginArea(new Rect(16f, 16f, 310f, 150f), _panelStyle);
            GUILayout.Label($"Speed: {_aircraft.SpeedKmh:F0} km/h", _textStyle);
            GUILayout.Label($"Altitude: {_aircraft.Altitude:F0} m", _textStyle);
            GUILayout.Label($"Throttle: {throttle:F0}%", _textStyle);
            GUILayout.Label($"Checkpoints: {cpText}", _textStyle);
            GUILayout.Space(4f);
            GUILayout.Label(status, _textStyle);
            GUILayout.EndArea();
        }
    }
}
