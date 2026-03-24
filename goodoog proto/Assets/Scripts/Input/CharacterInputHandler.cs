// CharacterInputHandler.cs
// Reads keyboard input and converts it to movement commands.
// Tracks consecutive same-direction taps for sprint activation.

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

        // Sprint tap tracking
        private GridPosition _consecutiveTapDir;
        private int _consecutiveTapCount;
        private float _lastTapTime;
        private bool _waitingForHold;
        private float _holdStartTime;

        // Sprint state exposed to CharacterLinkManager
        public bool SprintTriggered { get; private set; }
        public GridPosition SprintDirection => _consecutiveTapDir;

        // Read from central settings with fallback
        private float InputCooldown => SettingsManager.Instance?.settings?.inputCooldown ?? 0.15f;
        private float SprintTapWindow => SettingsManager.Instance?.settings?.sprintTapWindow ?? 0.3f;
        private float SprintHoldDuration => SettingsManager.Instance?.settings?.sprintHoldDuration ?? 0.15f;

        private void Awake()
        {
            // Keys will be configured by Robot/Dog scripts calling ConfigureKeys()
        }

        private void Start()
        {
            ConfigureKeys();
        }

        private void Update()
        {
            // Check if we're waiting for the 3rd tap to be held long enough
            if (_waitingForHold)
            {
                if (!IsDirectionHeld(_consecutiveTapDir))
                {
                    // Released before hold duration — cancel
                    _waitingForHold = false;
                }
                else if (Time.time - _holdStartTime >= SprintHoldDuration)
                {
                    // Held long enough — trigger sprint
                    SprintTriggered = true;
                    _waitingForHold = false;
                }
            }
        }

        private void LateUpdate()
        {
            // Clear one-frame flags
            SprintTriggered = false;
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
                TrackTapForSprint(direction);
            }

            return direction;
        }

        private void TrackTapForSprint(GridPosition direction)
        {
            if (direction == _consecutiveTapDir && (Time.time - _lastTapTime) < SprintTapWindow)
            {
                _consecutiveTapCount++;
            }
            else
            {
                _consecutiveTapDir = direction;
                _consecutiveTapCount = 1;
            }

            _lastTapTime = Time.time;

            if (_consecutiveTapCount >= 2 && !_waitingForHold)
            {
                _waitingForHold = true;
                _holdStartTime = Time.time;
            }
        }

        /// <summary>
        /// Checks if the key for a specific grid direction is currently held down.
        /// </summary>
        public bool IsDirectionHeld(GridPosition direction)
        {
            if (direction == GridPosition.Up) return UnityEngine.Input.GetKey(_upKey);
            if (direction == GridPosition.Down) return UnityEngine.Input.GetKey(_downKey);
            if (direction == GridPosition.Left) return UnityEngine.Input.GetKey(_leftKey);
            if (direction == GridPosition.Right) return UnityEngine.Input.GetKey(_rightKey);
            return false;
        }

        /// <summary>
        /// Returns the currently held direction, or Zero if none.
        /// </summary>
        public GridPosition GetHeldDirection()
        {
            if (UnityEngine.Input.GetKey(_upKey)) return GridPosition.Up;
            if (UnityEngine.Input.GetKey(_downKey)) return GridPosition.Down;
            if (UnityEngine.Input.GetKey(_leftKey)) return GridPosition.Left;
            if (UnityEngine.Input.GetKey(_rightKey)) return GridPosition.Right;
            return GridPosition.Zero;
        }

        /// <summary>
        /// Resets sprint tracking state. Called when sprint ends.
        /// </summary>
        public void ResetSprintState()
        {
            _consecutiveTapCount = 0;
            _consecutiveTapDir = GridPosition.Zero;
            _waitingForHold = false;
            SprintTriggered = false;
        }

        /// <summary>
        /// Returns a perpendicular direction tap this frame relative to the given sprint direction.
        /// Used for sprint pole grab detection.
        /// </summary>
        public GridPosition GetPerpendicularTapThisFrame(GridPosition sprintDir)
        {
            if (sprintDir.x != 0)
            {
                // Sprinting horizontally — perpendicular is up/down
                if (UnityEngine.Input.GetKeyDown(_upKey)) return GridPosition.Up;
                if (UnityEngine.Input.GetKeyDown(_downKey)) return GridPosition.Down;
            }
            else if (sprintDir.y != 0)
            {
                // Sprinting vertically — perpendicular is left/right
                if (UnityEngine.Input.GetKeyDown(_leftKey)) return GridPosition.Left;
                if (UnityEngine.Input.GetKeyDown(_rightKey)) return GridPosition.Right;
            }
            return GridPosition.Zero;
        }

        public bool IsSpecialHeld()
        {
            return profile == InputProfile.WASD
                ? UnityEngine.Input.GetKey(KeyCode.Space)
                : UnityEngine.Input.GetKey(KeyCode.RightShift);
        }
    }
}
