using System;
using System.Collections.Generic;
using BushPilot.Flight;
using UnityEngine;

namespace BushPilot.Missions
{
    public enum MissionState
    {
        Ready = 0,
        Flying = 1,
        ApproachLanding = 2,
        Success = 3,
        Failed = 4
    }

    /// <summary>
    /// Checkpoint ring trigger that activates sequentially in a flight route.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private int _orderIndex;
        [SerializeField] private Renderer[] _visualRenderers;

        private bool _isActiveTarget;
        private bool _isCompleted;
        private Action<Checkpoint> _onTriggeredCallback;

        public int OrderIndex => _orderIndex;
        public bool IsCompleted => _isCompleted;

        public void Initialize(int index, Action<Checkpoint> onTriggered)
        {
            _orderIndex = index;
            _onTriggeredCallback = onTriggered;
            if (_visualRenderers == null || _visualRenderers.Length == 0)
            {
                _visualRenderers = GetComponentsInChildren<Renderer>();
            }
        }

        public void SetCheckpointState(bool isNextTarget, bool isCompleted)
        {
            _isActiveTarget = isNextTarget;
            _isCompleted = isCompleted;

            gameObject.SetActive(!isCompleted);
            if (isCompleted)
            {
                return;
            }

            // Highlight active target ring in bright gold/cyan, upcoming rings in semi-transparent blue
            Color ringColor = isNextTarget ? new Color(1f, 0.78f, 0.1f, 1f) : new Color(0.2f, 0.65f, 0.95f, 0.45f);
            if (_visualRenderers != null)
            {
                foreach (Renderer r in _visualRenderers)
                {
                    if (r != null && r.material != null)
                    {
                        r.material.color = ringColor;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActiveTarget || _isCompleted)
            {
                return;
            }

            AircraftController aircraft = other.GetComponentInParent<AircraftController>();
            if (aircraft != null && !aircraft.IsCrashed)
            {
                _onTriggeredCallback?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Controls mission state flow: Ready -> Flying -> ApproachLanding -> Success / Failed.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [Header("Mission Configuration")]
        [SerializeField] private string _missionTitle = "01. Training Flight — Valley Delivery";
        [SerializeField] private int _baseContractReward = 500;
        [SerializeField] private List<Checkpoint> _checkpoints = new List<Checkpoint>();
        [SerializeField] private LandingZone _destinationRunway;

        [Header("References")]
        [SerializeField] private AircraftController _aircraft;
        [SerializeField] private LandingDetector _landingDetector;

        public string MissionTitle => _missionTitle;
        public MissionState CurrentState { get; private set; } = MissionState.Ready;
        public int CurrentCheckpointIndex { get; private set; }
        public int TotalCheckpoints => _checkpoints != null ? _checkpoints.Count : 0;
        public int EarnedReward { get; private set; }
        public string StatusMessage { get; private set; } = "Increase Throttle (R / Slider) & Take Off!";

        private void Start()
        {
            if (_aircraft == null)
            {
                _aircraft = FindFirstObjectByType<AircraftController>();
            }
            if (_landingDetector == null && _aircraft != null)
            {
                _landingDetector = _aircraft.GetComponent<LandingDetector>();
            }

            if (_landingDetector != null)
            {
                _landingDetector.OnTouchdownEvaluated += HandleTouchdownEvaluated;
                _landingDetector.OnLandingConfirmed += HandleLandingConfirmed;
            }

            ResetMission();
        }

        private void OnDestroy()
        {
            if (_landingDetector != null)
            {
                _landingDetector.OnTouchdownEvaluated -= HandleTouchdownEvaluated;
                _landingDetector.OnLandingConfirmed -= HandleLandingConfirmed;
            }
        }

        private void Update()
        {
            if (_aircraft != null && _aircraft.Input != null && _aircraft.Input.ResetPressed)
            {
                ResetMission();
                return;
            }

            if (CurrentState == MissionState.Ready && _aircraft != null && !_aircraft.IsGrounded)
            {
                CurrentState = MissionState.Flying;
                UpdateStatusPrompt();
            }
        }

        public void ConfigureMission(
            string title,
            List<Checkpoint> checkpoints,
            LandingZone destinationRunway,
            AircraftController aircraft,
            LandingDetector detector)
        {
            _missionTitle = title;
            _checkpoints = checkpoints;
            _destinationRunway = destinationRunway;
            _aircraft = aircraft;
            _landingDetector = detector;
        }

        public void ResetMission()
        {
            CurrentState = MissionState.Ready;
            CurrentCheckpointIndex = 0;
            EarnedReward = 0;

            for (int i = 0; i < _checkpoints.Count; i++)
            {
                if (_checkpoints[i] != null)
                {
                    _checkpoints[i].Initialize(i, OnCheckpointPassed);
                    _checkpoints[i].SetCheckpointState(i == 0, false);
                }
            }

            UpdateStatusPrompt();
        }

        private void OnCheckpointPassed(Checkpoint passed)
        {
            if (passed.OrderIndex != CurrentCheckpointIndex)
            {
                return;
            }

            passed.SetCheckpointState(false, true);
            CurrentCheckpointIndex++;

            if (CurrentCheckpointIndex < _checkpoints.Count)
            {
                _checkpoints[CurrentCheckpointIndex].SetCheckpointState(true, false);
                CurrentState = MissionState.Flying;
            }
            else
            {
                CurrentState = MissionState.ApproachLanding;
            }

            UpdateStatusPrompt();
        }

        private void HandleTouchdownEvaluated(LandingGrade grade, float verticalSpeed, float angle)
        {
            if (grade == LandingGrade.Crash)
            {
                CurrentState = MissionState.Failed;
                EarnedReward = 0;
                StatusMessage = $"CRASHED! (V-Speed: {verticalSpeed:F1} m/s, Tilt: {angle:F0}°). Press [T] or [Backspace] to Retry.";
                return;
            }

            if (CurrentCheckpointIndex < TotalCheckpoints)
            {
                StatusMessage = $"Touchdown ({grade}), but {TotalCheckpoints - CurrentCheckpointIndex} checkpoint(s) remaining! Take off again!";
            }
            else
            {
                StatusMessage = $"Touchdown: {grade}! Hold [SPACE / Trigger] to brake to a complete stop on the runway.";
            }
        }

        private void HandleLandingConfirmed(LandingGrade grade, LandingZone zone)
        {
            if (CurrentState == MissionState.Failed || CurrentState == MissionState.Success)
            {
                return;
            }

            if (CurrentCheckpointIndex < TotalCheckpoints)
            {
                StatusMessage = $"Stopped on {zone.runwayName}, but mission requires all {TotalCheckpoints} checkpoints first!";
                return;
            }

            if (_destinationRunway != null && zone != _destinationRunway)
            {
                StatusMessage = $"Landed on {zone.runwayName}. Fly to destination: {_destinationRunway.runwayName}!";
                return;
            }

            CurrentState = MissionState.Success;
            float multiplier = grade switch
            {
                LandingGrade.Perfect => 1.5f,
                LandingGrade.Good => 1.0f,
                LandingGrade.Hard => 0.6f,
                _ => 0f
            };

            EarnedReward = Mathf.RoundToInt(_baseContractReward * multiplier);
            StatusMessage = $"MISSION COMPLETE! Landing: {grade} | Reward: ${EarnedReward}. Press [T] to Fly Again.";
        }

        private void UpdateStatusPrompt()
        {
            if (TotalCheckpoints == 0 || CurrentCheckpointIndex >= TotalCheckpoints)
            {
                string runwayTitle = _destinationRunway != null ? _destinationRunway.runwayName : "Destination Runway";
                StatusMessage = $"All Checkpoints Cleared! Land safely & stop on [{runwayTitle}].";
            }
            else
            {
                StatusMessage = $"Fly through Checkpoint {CurrentCheckpointIndex + 1} / {TotalCheckpoints}";
            }
        }
    }
}
