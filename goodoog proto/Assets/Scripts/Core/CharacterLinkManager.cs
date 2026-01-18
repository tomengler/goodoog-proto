// CharacterLinkManager.cs
// Manages the joined/separated state of Robot and Dog.
// When joined: both characters occupy the same space and move as one.
// When separated: characters move independently.
// To separate: input opposing directions simultaneously.
// To rejoin: move into the same grid space.

using UnityEngine;
using DogAndRobot.Characters;
using DogAndRobot.Input;

namespace DogAndRobot.Core
{
    public class CharacterLinkManager : MonoBehaviour
    {
        [Header("Character References")]
        public Robot robot;
        public Dog dog;

        [Header("State")]
        [SerializeField] private bool _isJoined = true;

        // Public property so other scripts can check the state
        public bool IsJoined => _isJoined;

        // Input handler references
        private CharacterInputHandler _robotInput;
        private CharacterInputHandler _dogInput;

        // Read from central settings with fallback
        private float SeparationInputWindow => SettingsManager.Instance?.settings?.separationInputWindow ?? 0.15f;

        // Track recent inputs
        private GridPosition _lastRobotDirection = GridPosition.Zero;
        private GridPosition _lastDogDirection = GridPosition.Zero;
        private float _lastRobotInputTime = -999f;
        private float _lastDogInputTime = -999f;

        // Add this field near the top with the others
        private GridPosition _positionBeforeInput;
        private bool _hasPendingMovement = false;

        // We need to intercept input before the characters process it
        // So we'll disable their normal input handling and do it here

        private void Start()
        {
            _robotInput = robot.GetComponent<CharacterInputHandler>();
            _dogInput = dog.GetComponent<CharacterInputHandler>();

            if (_robotInput == null || _dogInput == null)
            {
                Debug.LogError("CharacterLinkManager: Could not find input handlers!");
                enabled = false;
                return;
            }

            // Start joined - make sure dog is at robot's position
            if (_isJoined)
            {
                dog.TeleportTo(robot.GridPosition);
            }
        }

        private void Update()
        {
            // We handle all input here instead of in the individual character scripts
            HandleLinkedInput();

            // Check if separated characters have rejoined
            CheckForRejoin();
        }

        private void HandleLinkedInput()
        {
            // Get input from both handlers
            GridPosition robotDirection = _robotInput.GetMovementInput();
            GridPosition dogDirection = _dogInput.GetMovementInput();

            if (_isJoined)
            {
                HandleJoinedInput(robotDirection, dogDirection);
            }
            else
            {
                HandleSeparateInput(robotDirection, dogDirection);
            }
        }

        private void HandleJoinedInput(GridPosition robotDir, GridPosition dogDir)
        {
            // Record inputs with timestamps
            if (robotDir != GridPosition.Zero)
            {
                _lastRobotDirection = robotDir;
                _lastRobotInputTime = Time.time;
            }

            if (dogDir != GridPosition.Zero)
            {
                _lastDogDirection = dogDir;
                _lastDogInputTime = Time.time;
            }

            // Check if both inputs happened within our window
            bool bothInputsRecent = (Time.time - _lastRobotInputTime) < SeparationInputWindow
                                 && (Time.time - _lastDogInputTime) < SeparationInputWindow;

            if (bothInputsRecent && AreOpposingDirections(_lastRobotDirection, _lastDogDirection))
            {
                // Snap back to position before the first input
                if (_hasPendingMovement)
                {
                    robot.TeleportTo(_positionBeforeInput);
                    dog.TeleportTo(_positionBeforeInput);
                }

                // Now separate from that clean position
                Separate(_lastRobotDirection, _lastDogDirection);

                _lastRobotDirection = GridPosition.Zero;
                _lastDogDirection = GridPosition.Zero;
                _hasPendingMovement = false;
                return;
            }

            // Handle normal movement
            GridPosition moveDirection = GridPosition.Zero;

            if (robotDir != GridPosition.Zero)
            {
                moveDirection = robotDir;
            }
            else if (dogDir != GridPosition.Zero)
            {
                moveDirection = dogDir;
            }

            if (moveDirection != GridPosition.Zero)
            {
                // Remember where we were before moving
                if (!_hasPendingMovement)
                {
                    _positionBeforeInput = robot.GridPosition;
                    _hasPendingMovement = true;
                }

                robot.TryMove(moveDirection);
                dog.TryMove(moveDirection);
            }

            // Clear pending state once the window has passed
            if (_hasPendingMovement &&
                (Time.time - _lastRobotInputTime) >= SeparationInputWindow &&
                (Time.time - _lastDogInputTime) >= SeparationInputWindow)
            {
                _hasPendingMovement = false;
            }
        }
        private void HandleSeparateInput(GridPosition robotDir, GridPosition dogDir)
        {
            // When separated, each character moves independently
            // The individual Update() methods in Robot and Dog won't work
            // because GetMovementInput() already consumed the input this frame
            // So we need to apply the movement here

            if (robotDir != GridPosition.Zero)
            {
                robot.TryMove(robotDir);
            }

            if (dogDir != GridPosition.Zero)
            {
                dog.TryMove(dogDir);
            }
        }

        private bool AreOpposingDirections(GridPosition dir1, GridPosition dir2)
        {
            // Both must have input
            if (dir1 == GridPosition.Zero || dir2 == GridPosition.Zero)
                return false;

            // Check if they're exact opposites
            // Opposing means they sum to zero: Left + Right = (-1,0) + (1,0) = (0,0)
            GridPosition sum = dir1 + dir2;
            return sum == GridPosition.Zero;
        }

        private void Separate(GridPosition robotDir, GridPosition dogDir)
        {
            Debug.Log("Separating characters!");

            _isJoined = false;

            // Move each character in their input direction
            robot.TryMove(robotDir);
            dog.TryMove(dogDir);
        }

        private void CheckForRejoin()
        {
            // Only check if currently separated
            if (_isJoined) return;

            // If both characters are on the same grid position, rejoin
            if (robot.GridPosition == dog.GridPosition)
            {
                Debug.Log("Characters rejoined!");
                _isJoined = true;
            }
        }

        /// <summary>
        /// Force the characters to join at the robot's position.
        /// Useful for spawning, level resets, etc.
        /// </summary>
        public void ForceJoin()
        {
            dog.TeleportTo(robot.GridPosition);
            _isJoined = true;
        }

        /// <summary>
        /// Force the characters to separate without movement.
        /// </summary>
        public void ForceSeparate()
        {
            _isJoined = false;
        }
    }
}