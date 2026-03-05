// CharacterLinkManager.cs
// Manages the joined/separated state of Robot and Dog.
// When joined: both characters occupy the same space and move as one.
// When separated: characters move independently.
// To separate: input opposing directions simultaneously.
// To rejoin: move into the same grid space.
// Also handles sprint routing for both joined and separated states.
// Sprint activates via same-character triple-tap: A-A-A(hold).

using UnityEngine;
using System.Collections;
using DogAndRobot.Characters;
using DogAndRobot.Enemies;
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

        // Sprint tracking — direction of the active sprint
        private GridPosition _activeSprintDir = GridPosition.Zero;

        // Joint attack tracking
        private GridPosition _lastRobotTapDir = GridPosition.Zero;
        private GridPosition _lastDogTapDir = GridPosition.Zero;
        private float _lastRobotTapTime = -999f;
        private float _lastDogTapTime = -999f;

        // Settings access for new attacks
        private float JointAttackWindow => SettingsManager.Instance?.settings?.jointAttackWindow ?? 0.15f;

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

            // Handle charge attacks (visual feedback + release detection)
            HandleChargeAttacks();

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

            if (_isJoined)
            {
                HandleJoinedInput(robotDirection, dogDirection);
            }
            else
            {
                HandleSeparateInput(robotDirection, dogDirection);
            }
        }

        // === JOINED / SEPARATE INPUT ===

        private void HandleJoinedInput(GridPosition robotDir, GridPosition dogDir)
        {
            // Record inputs with timestamps
            if (robotDir != GridPosition.Zero)
            {
                _lastRobotDirection = robotDir;
                _lastRobotInputTime = Time.time;
                _lastRobotTapDir = robotDir;
                _lastRobotTapTime = Time.time;
            }

            if (dogDir != GridPosition.Zero)
            {
                _lastDogDirection = dogDir;
                _lastDogInputTime = Time.time;
                _lastDogTapDir = dogDir;
                _lastDogTapTime = Time.time;
            }

            // Check for joint attack (both tap same direction within window)
            if (HandleJointAttack(robotDir, dogDir)) return;

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

            // Check for sprint trigger from either input handler
            if (_robotInput.SprintTriggered && _robotInput.IsDirectionHeld(_robotInput.SprintDirection))
            {
                StartJoinedSprint(_robotInput.SprintDirection);
                return;
            }
            if (_dogInput.SprintTriggered && _dogInput.IsDirectionHeld(_dogInput.SprintDirection))
            {
                StartJoinedSprint(_dogInput.SprintDirection);
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
            // Track taps for joint attack
            if (robotDir != GridPosition.Zero)
            {
                _lastRobotTapDir = robotDir;
                _lastRobotTapTime = Time.time;
            }
            if (dogDir != GridPosition.Zero)
            {
                _lastDogTapDir = dogDir;
                _lastDogTapTime = Time.time;
            }

            // Check for joint attack
            if (HandleJointAttack(robotDir, dogDir)) return;

            // Check sprint triggers per character
            if (_robotInput.SprintTriggered && _robotInput.IsDirectionHeld(_robotInput.SprintDirection)
                && robot.SprintState == MoveState.Normal)
            {
                _activeSprintDir = _robotInput.SprintDirection;
                robot.StartSprint(_robotInput.SprintDirection);
            }
            if (_dogInput.SprintTriggered && _dogInput.IsDirectionHeld(_dogInput.SprintDirection)
                && dog.SprintState == MoveState.Normal)
            {
                _activeSprintDir = _dogInput.SprintDirection;
                dog.StartSprint(_dogInput.SprintDirection);
            }

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

        // === CHARGE & JOINT ATTACKS ===

        private void HandleChargeAttacks()
        {
            // Don't process charge during sprint
            if (IsAnySprinting()) return;

            // Visual shake while charging
            var feelSettings = GameFeelManager.Instance?.Settings;
            float shakeIntensity = feelSettings?.chargeShakeIntensity ?? 0.02f;

            if (_robotInput.IsCharging && _robotInput.ChargeReady)
            {
                GameFeelManager.ObjectShake(robot.transform, Vector2.one, Time.deltaTime, shakeIntensity);
            }
            if (_dogInput.IsCharging && _dogInput.ChargeReady)
            {
                GameFeelManager.ObjectShake(dog.transform, Vector2.one, Time.deltaTime, shakeIntensity);
            }

            // Check for charge release
            HandleChargeRelease(_robotInput, robot);
            HandleChargeRelease(_dogInput, dog);
        }

        private void HandleChargeRelease(CharacterInputHandler input, GridCharacter character)
        {
            if (!input.ChargeReleased) return;

            GridPosition dir = input.ChargeDirection;
            GridPosition targetPos = character.GridPosition + dir;
            Enemy enemy = character.FindVulnerableEnemyAt(targetPos);

            if (enemy != null)
            {
                enemy.TryLaunch(dir);

                // Squash the attacker
                Vector2 dir2d = new Vector2(dir.x, dir.y);
                GameFeelManager.Squash(character.transform, dir2d);
            }
        }

        /// <summary>
        /// Checks if both players tapped the same direction within the joint attack window.
        /// Returns true if a joint attack was triggered (consuming the input).
        /// </summary>
        private bool HandleJointAttack(GridPosition robotDir, GridPosition dogDir)
        {
            // Need both taps to be recent and in the same direction
            bool bothRecent = (Time.time - _lastRobotTapTime) < JointAttackWindow
                           && (Time.time - _lastDogTapTime) < JointAttackWindow;

            if (!bothRecent) return false;
            if (_lastRobotTapDir == GridPosition.Zero || _lastDogTapDir == GridPosition.Zero) return false;
            if (_lastRobotTapDir != _lastDogTapDir) return false;

            GridPosition dir = _lastRobotTapDir;

            // Find a vulnerable enemy in that direction from either character
            Enemy enemy = robot.FindVulnerableEnemyAt(robot.GridPosition + dir);
            if (enemy == null)
                enemy = dog.FindVulnerableEnemyAt(dog.GridPosition + dir);

            if (enemy == null) return false;

            // Joint attack!
            enemy.TryLaunch(dir);

            // Juice: squash both characters
            Vector2 dir2d = new Vector2(dir.x, dir.y);
            GameFeelManager.Squash(robot.transform, dir2d);
            GameFeelManager.Squash(dog.transform, dir2d);

            // Extra screen shake for joint attack
            GameFeelManager.ScreenShake(0.15f, 0.06f);

            // Clear tap tracking
            _lastRobotTapDir = GridPosition.Zero;
            _lastDogTapDir = GridPosition.Zero;

            return true;
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
                HandleSeparateSprintUpdate(robot, _robotInput);
                HandleSeparateSprintUpdate(dog, _dogInput);
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
                    ResetAllSprintInput();
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
                ResetAllSprintInput();
                // Re-sync dog grid position to robot
                dog.TeleportTo(robot.GridPosition);
                if (_isJoined)
                {
                    UpdateJoinedOffsets();
                }
            }
        }

        private void HandleSeparateSprintUpdate(GridCharacter character, CharacterInputHandler input)
        {
            if (character.SprintState == MoveState.Normal) return;

            if (character.SprintState == MoveState.Sprinting)
            {
                GridPosition sprintDir = input.SprintDirection;

                // Check if key still held
                if (!input.IsDirectionHeld(sprintDir))
                {
                    character.StartBraking();
                }

                // Check opposite direction
                GridPosition held = input.GetHeldDirection();
                GridPosition opposite = new GridPosition(-sprintDir.x, -sprintDir.y);
                if (held == opposite)
                {
                    character.StopSprintImmediate();
                    input.ResetSprintState();
                }
            }

            // When sprint ends, reset input
            if (character.SprintState == MoveState.Normal)
            {
                input.ResetSprintState();
            }
        }

        private void ResetAllSprintInput()
        {
            _robotInput.ResetSprintState();
            _dogInput.ResetSprintState();
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
