using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BushPilot.Input
{
    public class FlightInputAdapter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _deadzone = 0.08f;
        [SerializeField] private float _throttleStepRate = 0.45f;
        [SerializeField] private bool _invertPitch = false;

        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        public float Yaw { get; private set; }
        public float Throttle { get; private set; }
        public bool IsBraking { get; private set; }
        public bool CameraSwitchPressed { get; private set; }
        public bool ResetPressed { get; private set; }

        public bool IsJoystickConnected => Joystick.current != null;
        public string ActiveDeviceName => IsJoystickConnected ? Joystick.current.displayName : "Keyboard";

        private float _lastSliderValue;
        private bool _sliderInitialized;

        private void Update()
        {
            CameraSwitchPressed = false;
            ResetPressed = false;

            float kbPitch = 0f;
            float kbRoll = 0f;
            float kbYaw = 0f;
            bool kbBrake = false;

            // 1. Keyboard Input
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                // W / Stick Forward = Nose Down (+1), S / Stick Back = Pull Nose Up (-1)
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbPitch += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbPitch -= 1f;

                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbRoll += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbRoll -= 1f;

                if (kb.eKey.isPressed) kbYaw += 1f;
                if (kb.qKey.isPressed) kbYaw -= 1f;

                if (kb.rKey.isPressed || kb.leftShiftKey.isPressed)
                {
                    Throttle = Mathf.Clamp01(Throttle + _throttleStepRate * Time.deltaTime);
                }
                if (kb.fKey.isPressed || kb.leftCtrlKey.isPressed)
                {
                    Throttle = Mathf.Clamp01(Throttle - _throttleStepRate * Time.deltaTime);
                }

                kbBrake = kb.spaceKey.isPressed;
                if (kb.cKey.wasPressedThisFrame) CameraSwitchPressed = true;
                if (kb.tKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) ResetPressed = true;
            }

            // 2. Logitech Extreme 3D Pro / Joystick Input
            float joyPitch = 0f;
            float joyRoll = 0f;
            float joyYaw = 0f;
            bool joyBrake = false;

            Joystick joy = Joystick.current;
            if (joy != null)
            {
                Vector2 stick = joy.stick.ReadValue();
                joyRoll = ApplyDeadzone(stick.x);
                joyPitch = ApplyDeadzone(stick.y);

                // Twist axis (Rudder)
                if (joy.twist != null)
                {
                    joyYaw = ApplyDeadzone(joy.twist.ReadValue());
                }
                else if (joy.TryGetChildControl<AxisControl>("rz") is AxisControl rz)
                {
                    joyYaw = ApplyDeadzone(rz.ReadValue());
                }

                // Throttle Slider on base
                if (joy.TryGetChildControl<AxisControl>("slider") is AxisControl slider)
                {
                    float raw = slider.ReadValue();
                    // Convert [-1..1] to [0..1] and invert so pushing lever forward = 100%
                    float normalized = raw < -0.05f ? (raw + 1f) * 0.5f : raw;
                    normalized = 1f - Mathf.Clamp01(normalized);

                    if (!_sliderInitialized)
                    {
                        _lastSliderValue = normalized;
                        _sliderInitialized = true;
                    }
                    else if (Mathf.Abs(normalized - _lastSliderValue) > 0.02f)
                    {
                        Throttle = normalized;
                        _lastSliderValue = normalized;
                    }
                }

                joyBrake = joy.trigger.isPressed;
            }
            else
            {
                _sliderInitialized = false;
            }

            float rawPitch = Mathf.Clamp(kbPitch + joyPitch, -1f, 1f);
            Pitch = _invertPitch ? -rawPitch : rawPitch;
            Roll = Mathf.Clamp(kbRoll + joyRoll, -1f, 1f);
            Yaw = Mathf.Clamp(kbYaw + joyYaw, -1f, 1f);
            IsBraking = kbBrake || joyBrake;
        }

        public void SetThrottleImmediate(float value)
        {
            Throttle = Mathf.Clamp01(value);
        }

        private float ApplyDeadzone(float value)
        {
            if (Mathf.Abs(value) < _deadzone) return 0f;
            return Mathf.Sign(value) * ((Mathf.Abs(value) - _deadzone) / (1f - _deadzone));
        }
    }
}
