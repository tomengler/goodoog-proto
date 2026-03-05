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

        // Charge attack tracking
        private GridPosition _chargeDirection;
        private float _chargeHoldTime;
        private bool _isCharging;

        public bool IsCharging => _isCharging;
        public GridPosition ChargeDirection => _chargeDirection;
        public bool ChargeReleased { get; private set; }

        private float ChargeAttackHoldDuration =>
            SettingsManager.Instance?.settings?.chargeAttackHoldDuration ?? 1.0f;

        public bool ChargeReady => _isCharging && _chargeHoldTime >= ChargeAttackHoldDuration;

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

            // Charge attack tracking — only when a direction key is held
            UpdateChargeTracking();
        }

        private void LateUpdate()
        {
            // Clear one-frame flags
            SprintTriggered = false;
            ChargeReleased = false;
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

        private void UpdateChargeTracking()
        {
            GridPosition heldDir = GetHeldDirection();

            if (heldDir != GridPosition.Zero)
            {
                if (_isCharging && heldDir == _chargeDirection)
                {
                    // Same direction still held — accumulate time
                    _chargeHoldTime += Time.deltaTime;
                }
                else
                {
                    // New direction or starting fresh
                    _chargeDirection = heldDir;
                    _chargeHoldTime = 0f;
                    _isCharging = true;
                }
            }
            else if (_isCharging)
            {
                // Direction was released
                if (ChargeReady)
                {
                    ChargeReleased = true;
                }
                _isCharging = false;
                _chargeHoldTime = 0f;
            }
        }

        public bool IsSpecialHeld()
        {
            return profile == InputProfile.WASD
                ? UnityEngine.Input.GetKey(KeyCode.Space)
                : UnityEngine.Input.GetKey(KeyCode.RightShift);
        }
    }
}
