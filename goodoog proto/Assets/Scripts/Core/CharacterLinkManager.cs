// CharacterLinkManager.cs
// Manages the joined/separated state of Robot and Dog.
// When joined: both characters occupy the same space and move as one.
// When separated: characters move independently.
// To separate: input opposing directions simultaneously.
// To rejoin: move into the same grid space.
// Also handles sprint routing for both joined and separated states.
// Sprint activates via alternating input: A-B-A(hold) or B-A-B(hold).

using UnityEngine;
using System.Collections;
using DogAndRobot.Characters;
using DogAndRobot.Input;

namespace DogAndRobot.Core
{
    public class CharacterLinkManager : MonoBehaviour
    {
        [Header("Character References")]
        public Robot robot;
        public Dog dog;
        // Track which character is "leading" the movement when joined
        private DamageType _joinedMovementLeader;


        [Header("State")]
        [SerializeField] private bool _isJoined = true;

        // Public property so other scripts can check the state
        public bool IsJoined => _isJoined;

        // Settings access
        private float SeparationInputWindow => SettingsManager.Instance?.settings?.separationInputWindow ?? 0.15f;
        private float JoinedOffset => SettingsManager.Instance?.settings?.joinedOffset ?? 0.25f;
        private float JoinedScale => SettingsManager.Instance?.settings?.joinedScale ?? 0.5f;
        private float SprintTapWindow => SettingsManager.Instance?.settings?.sprintTapWindow ?? 0.3f;

        // Input handler references
        private CharacterInputHandler _robotInput;
        private CharacterInputHandler _dogInput;

        // Track recent inputs for separation detection
        private GridPosition _lastRobotDirection = GridPosition.Zero;
        private GridPosition _lastDogDirection = GridPosition.Zero;
        private float _lastRobotInputTime = -999f;
        private float _lastDogInputTime = -999f;

        // Track position before movement for clean separation
        private GridPosition _positionBeforeInput;
        private bool _hasPendingMovement = false;

        // === ALTERNATING SPRINT TAP TRACKING ===
        // Pattern: A-B-A(hold) or B-A-B(hold) in the same direction
        private GridPosition _sprintTapDir = GridPosition.Zero;
        private int _sprintTapCount;
        private CharacterInputHandler _lastSprintTapper;
        private float _lastSprintTapTime;

        // Sprint tracking — which input triggered the active sprint
        private GridPosition _activeSprintDir = GridPosition.Zero;

        // Setup flag
        private bool _setupComplete = false;

        private void Awake()
        {
            // We'll get the input handlers in Start, after characters have initialized
        }

        private void Start()
        {
            // Give characters a frame to set up their input handlers
            StartCoroutine(DelayedSetup());
        }

        private IEnumerator DelayedSetup()
        {
            // Wait one frame for Robot and Dog Awake() to run
            yield return null;

            _robotInput = robot.GetComponent<CharacterInputHandler>();
            _dogInput = dog.GetComponent<CharacterInputHandler>();

            if (_robotInput == null || _dogInput == null)
            {
                Debug.LogError("CharacterLinkManager: Could not find input handlers!");
                enabled = false;
                yield break;
            }

            // Start joined - make sure dog is at robot's position
            if (_isJoined)
            {
                dog.TeleportTo(robot.GridPosition);
                UpdateJoinedVisuals();
            }

            _setupComplete = true;
        }

        private void Update()
        {
            // Don't do anything until setup is complete
            if (!_setupComplete) return;

            // Handle sprint state each frame (hold/release detection)
            HandleSprintUpdate();

            // We handle all input here instead of in the individual character scripts
            HandleLinkedInput();

            // Check if separated characters have rejoined
            CheckForRejoin();
        }

        private void HandleLinkedInput()
        {
            // Don't process tap input while sprinting or braking
            if (IsAnySprinting()) return;

            // Get input from both handlers
            GridPosition robotDirection = _robotInput.GetMovementInput();
            GridPosition dogDirection = _dogInput.GetMovementInput();

            // Track alternating taps for sprint (before movement consumes them)
            if (robotDirection != GridPosition.Zero)
            {
                TrackAlternatingTap(_robotInput, robotDirection);
            }
            if (dogDirection != GridPosition.Zero)
            {
                TrackAlternatingTap(_dogInput, dogDirection);
            }

            if (_isJoined)
            {
                HandleJoinedInput(robotDirection, dogDirection);
            }
            else
            {
                HandleSeparateInput(robotDirection, dogDirection);
            }
        }

        // === ALTERNATING TAP DETECTION ===

        private void TrackAlternatingTap(CharacterInputHandler tapper, GridPosition direction)
        {
            // Check if this continues the alternating pattern in the same direction
            if (direction == _sprintTapDir
                && tapper != _lastSprintTapper
                && (Time.time - _lastSprintTapTime) < SprintTapWindow)
            {
                _sprintTapCount++;
            }
            else
            {
                // Start a new sequence
                _sprintTapDir = direction;
                _sprintTapCount = 1;
            }

            _lastSprintTapper = tapper;
            _lastSprintTapTime = Time.time;

            // 3rd tap + held = sprint
            if (_sprintTapCount >= 3 && tapper.IsDirectionHeld(direction))
            {
                TriggerSprint(direction);
            }
        }

        private void TriggerSprint(GridPosition direction)
        {
            if (_isJoined)
            {
                StartJoinedSprint(direction);
            }
            else
            {
                // Both characters sprint when alternating input triggers
                if (robot.SprintState == MoveState.Normal)
                    robot.StartSprint(direction);
                if (dog.SprintState == MoveState.Normal)
                    dog.StartSprint(direction);
            }
            ResetSprintTapTracking();
        }

        private void ResetSprintTapTracking()
        {
            _sprintTapDir = GridPosition.Zero;
            _sprintTapCount = 0;
            _lastSprintTapper = null;
            _lastSprintTapTime = -999f;
        }

        // === JOINED / SEPARATE INPUT ===

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

                // Clear visual offsets before separating
                ClearVisualOffsets();

                // Now separate from that clean position
                Separate(_lastRobotDirection, _lastDogDirection);

                _lastRobotDirection = GridPosition.Zero;
                _lastDogDirection = GridPosition.Zero;
                _hasPendingMovement = false;
                return;
            }

            // Handle normal movement
            GridPosition moveDirection = GridPosition.Zero;
            DamageType leader = DamageType.Robot;

            if (robotDir != GridPosition.Zero)
            {
                moveDirection = robotDir;
                leader = DamageType.Robot;
            }
            else if (dogDir != GridPosition.Zero)
            {
                moveDirection = dogDir;
                leader = DamageType.Dog;
            }

            if (moveDirection != GridPosition.Zero)
            {
                // Remember where we were before moving
                if (!_hasPendingMovement)
                {
                    _positionBeforeInput = robot.GridPosition;
                    _hasPendingMovement = true;
                }

                // Store who's leading
                _joinedMovementLeader = leader;

                // Only the leader actually "moves" (and can deal damage)
                // The other character just follows
                if (leader == DamageType.Robot)
                {
                    robot.TryMove(moveDirection);
                    dog.AnimateMoveTo(robot.GridPosition);
                }
                else
                {
                    dog.TryMove(moveDirection);
                    robot.AnimateMoveTo(dog.GridPosition);
                }

                // Update offsets without snapping positions
                UpdateJoinedOffsets();
            }
        }

        private void HandleSeparateInput(GridPosition robotDir, GridPosition dogDir)
        {
            // Normal movement for non-sprinting characters
            if (robotDir != GridPosition.Zero && robot.SprintState == MoveState.Normal)
            {
                robot.TryMove(robotDir);
            }

            if (dogDir != GridPosition.Zero && dog.SprintState == MoveState.Normal)
            {
                dog.TryMove(dogDir);
            }
        }

        // === SPRINT MANAGEMENT ===

        private bool IsAnySprinting()
        {
            return robot.SprintState != MoveState.Normal || dog.SprintState != MoveState.Normal;
        }

        private void StartJoinedSprint(GridPosition direction)
        {
            _activeSprintDir = direction;
            robot.StartSprint(direction);
            dog.StartSprint(direction);
        }

        private void HandleSprintUpdate()
        {
            if (_isJoined)
            {
                HandleJoinedSprintUpdate();
            }
            else
            {
                HandleSeparateSprintUpdate();
            }
        }

        private void HandleJoinedSprintUpdate()
        {
            // Only care if currently sprinting or braking
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
                return;

            if (robot.SprintState == MoveState.Sprinting)
            {
                // Check if the sprint direction key is still held by either input
                bool stillHeld = _robotInput.IsDirectionHeld(_activeSprintDir)
                              || _dogInput.IsDirectionHeld(_activeSprintDir);

                if (!stillHeld)
                {
                    // Key released — start braking both
                    robot.StartBraking();
                    dog.StartBraking();
                }

                // Check for opposite direction — immediate stop
                GridPosition robotHeld = _robotInput.GetHeldDirection();
                GridPosition dogHeld = _dogInput.GetHeldDirection();
                GridPosition oppositeDir = new GridPosition(-_activeSprintDir.x, -_activeSprintDir.y);

                if (robotHeld == oppositeDir || dogHeld == oppositeDir)
                {
                    robot.StopSprintImmediate();
                    dog.StopSprintImmediate();
                    ResetSprintTapTracking();
                }
            }

            // Keep follower synced during sprint/brake
            if (robot.SprintState != MoveState.Normal || dog.SprintState != MoveState.Normal)
            {
                // Sync dog position to robot during joined sprint
                dog.transform.position = robot.transform.position;
            }

            // When both finish, clean up
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
            {
                _activeSprintDir = GridPosition.Zero;
                ResetSprintTapTracking();
                // Re-sync dog grid position to robot
                dog.TeleportTo(robot.GridPosition);
                if (_isJoined)
                {
                    UpdateJoinedOffsets();
                }
            }
        }

        private void HandleSeparateSprintUpdate()
        {
            // Both characters share the same sprint direction from alternating input
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
                return;

            // Check hold/opposite on either input during sprint
            if (robot.SprintState == MoveState.Sprinting || dog.SprintState == MoveState.Sprinting)
            {
                GridPosition dir = _activeSprintDir;
                bool stillHeld = _robotInput.IsDirectionHeld(dir) || _dogInput.IsDirectionHeld(dir);

                if (!stillHeld)
                {
                    if (robot.SprintState == MoveState.Sprinting) robot.StartBraking();
                    if (dog.SprintState == MoveState.Sprinting) dog.StartBraking();
                }

                // Check opposite direction
                GridPosition opposite = new GridPosition(-dir.x, -dir.y);
                GridPosition robotHeld = _robotInput.GetHeldDirection();
                GridPosition dogHeld = _dogInput.GetHeldDirection();

                if (robotHeld == opposite || dogHeld == opposite)
                {
                    if (robot.SprintState != MoveState.Normal) robot.StopSprintImmediate();
                    if (dog.SprintState != MoveState.Normal) dog.StopSprintImmediate();
                    ResetSprintTapTracking();
                }
            }

            // When both finish, clean up
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
            {
                _activeSprintDir = GridPosition.Zero;
                ResetSprintTapTracking();
            }
        }

        // === SEPARATION / REJOIN ===

        private bool AreOpposingDirections(GridPosition dir1, GridPosition dir2)
        {
            if (dir1 == GridPosition.Zero || dir2 == GridPosition.Zero)
                return false;

            GridPosition sum = dir1 + dir2;
            return sum == GridPosition.Zero;
        }

        private void Separate(GridPosition robotDir, GridPosition dogDir)
        {
            Debug.Log("Separating characters!");

            _isJoined = false;

            robot.TryMove(robotDir);
            dog.TryMove(dogDir);
        }

        private void CheckForRejoin()
        {
            if (_isJoined) return;

            // Don't rejoin while sprinting
            if (IsAnySprinting()) return;

            if (robot.GridPosition == dog.GridPosition)
            {
                Debug.Log("Characters rejoined!");
                _isJoined = true;
                UpdateJoinedVisuals();
            }
        }

        // === VISUAL HELPERS ===

        private void UpdateJoinedVisuals()
        {
            if (!_isJoined) return;
            UpdateJoinedOffsets();
            robot.TeleportTo(robot.GridPosition);
            dog.TeleportTo(dog.GridPosition);
        }

        private void UpdateJoinedOffsets()
        {
            robot.SetVisualScale(JoinedScale, 1f);
            dog.SetVisualScale(JoinedScale, 1f);

            float halfWidth = JoinedScale / 2f;
            robot.SetVisualOffset(new Vector3(-halfWidth, 0, 0));
            dog.SetVisualOffset(new Vector3(halfWidth, 0, 0));
        }

        private void ClearVisualOffsets()
        {
            robot.SetVisualOffset(Vector3.zero);
            dog.SetVisualOffset(Vector3.zero);

            robot.SetVisualScale(1f, 1f);
            dog.SetVisualScale(1f, 1f);
        }

        public void ForceJoin()
        {
            dog.TeleportTo(robot.GridPosition);
            _isJoined = true;
            UpdateJoinedVisuals();
        }

        public void ForceSeparate()
        {
            _isJoined = false;
            ClearVisualOffsets();
        }
    }
}
