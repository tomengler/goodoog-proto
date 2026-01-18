// CharacterInputHandler.cs
// Reads keyboard input and converts it to movement commands.

using UnityEngine;
using DogAndRobot.Core;

namespace DogAndRobot.Input
{
    public enum InputProfile
    {
        WASD,
        IJKL
    }

    public class CharacterInputHandler : MonoBehaviour
    {
        [Header("Input Configuration")]
        public InputProfile profile = InputProfile.WASD;

        private float _lastInputTime;

        private KeyCode _upKey;
        private KeyCode _downKey;
        private KeyCode _leftKey;
        private KeyCode _rightKey;

        // Read from central settings with fallback
        private float InputCooldown => SettingsManager.Instance?.settings?.inputCooldown ?? 0.15f;

        private void Awake()
        {
            // Keys will be configured by Robot/Dog scripts calling ConfigureKeys()
        }

        private void Start()
        {
            ConfigureKeys();
        }

        public void ConfigureKeys()
        {
            switch (profile)
            {
                case InputProfile.WASD:
                    _upKey = KeyCode.W;
                    _downKey = KeyCode.S;
                    _leftKey = KeyCode.A;
                    _rightKey = KeyCode.D;
                    break;

                case InputProfile.IJKL:
                    _upKey = KeyCode.I;
                    _downKey = KeyCode.K;
                    _leftKey = KeyCode.J;
                    _rightKey = KeyCode.L;
                    break;
            }
        }

        public GridPosition GetMovementInput()
        {
            if (Time.time - _lastInputTime < InputCooldown)
            {
                return GridPosition.Zero;
            }

            GridPosition direction = GridPosition.Zero;

            if (UnityEngine.Input.GetKeyDown(_upKey))
            {
                direction = GridPosition.Up;
            }
            else if (UnityEngine.Input.GetKeyDown(_downKey))
            {
                direction = GridPosition.Down;
            }
            else if (UnityEngine.Input.GetKeyDown(_leftKey))
            {
                direction = GridPosition.Left;
            }
            else if (UnityEngine.Input.GetKeyDown(_rightKey))
            {
                direction = GridPosition.Right;
            }

            if (direction != GridPosition.Zero)
            {
                _lastInputTime = Time.time;
            }

            return direction;
        }

        public bool IsSpecialHeld()
        {
            return profile == InputProfile.WASD
                ? UnityEngine.Input.GetKey(KeyCode.Space)
                : UnityEngine.Input.GetKey(KeyCode.RightShift);
        }
    }
}