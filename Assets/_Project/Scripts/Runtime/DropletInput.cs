using UnityEngine;
using UnityEngine.InputSystem;

namespace DropletPrototype
{
    public struct FlightCommand
    {
        public Vector2 look;
        public float throttle, strafe;
        public bool boost, brake;
    }

    public sealed class DropletInput : MonoBehaviour
    {
        public DropletMotor motor;
        Vector2 pendingLook;
        bool waitForRelease;
        public bool GameplayEnabled { get; private set; } = true;
        public System.Action PauseRequested;
        public System.Action StartRequested;
        public System.Action RestartRequested;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (PauseRequested != null) PauseRequested();
                else SetGameplay(!GameplayEnabled);
            }
            if (keyboard.enterKey.wasPressedThisFrame) StartRequested?.Invoke();
            if (keyboard.rKey.wasPressedThisFrame && (!GameplayEnabled || (motor != null && motor.mission != null && motor.mission.combat != null))) RestartRequested?.Invoke();
            if (GameplayEnabled && !waitForRelease && Mouse.current != null)
                pendingLook += Mouse.current.delta.ReadValue();
            if (waitForRelease && !keyboard.wKey.isPressed && !keyboard.sKey.isPressed &&
                !keyboard.aKey.isPressed && !keyboard.dKey.isPressed && !keyboard.leftShiftKey.isPressed && !keyboard.spaceKey.isPressed)
                waitForRelease = false;
        }

        public FlightCommand ReadStep(float dt, DropletSettings settings)
        {
            var keyboard = Keyboard.current;
            if (!GameplayEnabled || waitForRelease || keyboard == null) return default;
            float maxPixels = settings.turnDegreesPerSecond * dt / settings.mouseSensitivity;
            Vector2 look = Vector2.ClampMagnitude(pendingLook, maxPixels);
            pendingLook -= look;
            return new FlightCommand { look = look,
                throttle = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0),
                strafe = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                boost = keyboard.leftShiftKey.isPressed, brake = keyboard.spaceKey.isPressed };
        }

        public void SetGameplay(bool enabled)
        {
            GameplayEnabled = enabled; ResetInput();
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }
        public void ResetInput() { pendingLook = Vector2.zero; waitForRelease = true; }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && GameplayEnabled)
            {
                if (PauseRequested != null) PauseRequested(); else SetGameplay(false);
            }
        }
        void OnDisable() { pendingLook = Vector2.zero; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
