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
using DogAndRobot.Environment;
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

        // Tracks whether the joined sprint leader was orbiting last frame.
        // Used to detect when a sprint orbit completes so the follower can re-enter sprint.
        private bool _leaderWasOrbiting = false;

        // Sprint follow-up: after a successful sprint hit, track the enemy for a lunge follow-up
        private Enemy _sprintHitEnemy;
        private GridPosition _sprintHitDirection;

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
                SetJoinedSprintOverrides(true);
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
            // Don't process tap input while sprinting, braking, or orbiting
            if (IsAnySprinting()) return;
            if (robot.IsOrbiting || dog.IsOrbiting) return;

            // Get input from both handlers
            GridPosition robotDirection = _robotInput.GetMovementInput();
            GridPosition dogDirection = _dogInput.GetMovementInput();

            // Check for sprint follow-up lunge before normal input
            if (TrySprintFollowUp(robotDirection, dogDirection)) return;

            if (_isJoined)
            {
                HandleJoinedInput(robotDirection, dogDirection);
            }
            else
            {
                HandleSeparateInput(robotDirection, dogDirection);
            }
        }

        /// <summary>
        /// After a successful sprint hit, the next correct-type tap toward the enemy
        /// lunges the character to close the gap and hit immediately.
        /// Wrong type = normal movement (clears the follow-up state).
        /// </summary>
        private bool TrySprintFollowUp(GridPosition robotDir, GridPosition dogDir)
        {
            // Check joined sprint follow-up
            if (_sprintHitEnemy != null)
            {
                GridPosition inputDir = GridPosition.Zero;
                GridCharacter attacker = null;
                GridPosition redirectDir = GridPosition.Zero;

                if (robotDir == _sprintHitDirection && robotDir != GridPosition.Zero)
                {
                    inputDir = robotDir;
                    attacker = robot;
                    // Check if dog is holding a perpendicular direction for redirect
                    GridPosition dogHeld = _dogInput.GetHeldDirection();
                    if (dogHeld != GridPosition.Zero && ArePerpendicularDirections(_sprintHitDirection, dogHeld))
                        redirectDir = dogHeld;
                }
                else if (dogDir == _sprintHitDirection && dogDir != GridPosition.Zero)
                {
                    inputDir = dogDir;
                    attacker = dog;
                    // Check if robot is holding a perpendicular direction for redirect
                    GridPosition robotHeld = _robotInput.GetHeldDirection();
                    if (robotHeld != GridPosition.Zero && ArePerpendicularDirections(_sprintHitDirection, robotHeld))
                        redirectDir = robotHeld;
                }

                // Any input in a different direction clears the follow-up
                if (inputDir == GridPosition.Zero)
                {
                    if (robotDir != GridPosition.Zero || dogDir != GridPosition.Zero)
                        ClearSprintFollowUp();
                    return false;
                }

                // Enemy might have been destroyed
                if (_sprintHitEnemy == null)
                {
                    ClearSprintFollowUp();
                    return false;
                }

                // Check damage type match
                DamageType damageType = attacker.GetDamageType();
                Enemy enemy = _sprintHitEnemy;
                GridPosition dir = _sprintHitDirection;
                GridPosition? knockbackOverride = redirectDir != GridPosition.Zero ? redirectDir : (GridPosition?)null;

                if (enemy.IsVulnerable)
                {
                    // Hit first so enemy moves, then lunge to its new position
                    enemy.TryPush(dir, knockbackOverride);
                    LungeToEnemy(attacker, enemy, dir);
                    ClearSprintFollowUp();
                    return true;
                }

                // Check if damage type matches what the enemy needs
                if (enemy.PeekNextDamageType() == damageType)
                {
                    // Hit first so enemy moves, then lunge to its new position
                    enemy.TryTakeHit(damageType, dir, attacker.transform, knockbackOverride: knockbackOverride);
                    LungeToEnemy(attacker, enemy, dir);
                    ClearSprintFollowUp();
                    return true;
                }
                else
                {
                    // Wrong type — clear follow-up, process as normal movement
                    ClearSprintFollowUp();
                    return false;
                }
            }

            // Check separated sprint follow-up (stored on individual characters)
            if (TrySeparateSprintFollowUp(robot, robotDir)) return true;
            if (TrySeparateSprintFollowUp(dog, dogDir)) return true;

            return false;
        }

        private bool TrySeparateSprintFollowUp(GridCharacter character, GridPosition inputDir)
        {
            if (character.LastSprintHitEnemy == null) return false;

            if (inputDir == GridPosition.Zero) return false;

            // Any direction other than the sprint hit direction clears follow-up
            if (inputDir != character.LastSprintHitDirection)
            {
                character.LastSprintHitEnemy = null;
                return false;
            }

            Enemy enemy = character.LastSprintHitEnemy;
            GridPosition dir = character.LastSprintHitDirection;

            // Enemy might have been destroyed
            if (enemy == null)
            {
                character.LastSprintHitEnemy = null;
                return false;
            }

            if (enemy.IsVulnerable)
            {
                enemy.TryPush(dir);
                LungeToEnemy(character, enemy, dir);
                character.LastSprintHitEnemy = null;
                return true;
            }

            DamageType damageType = character.GetDamageType();
            if (enemy.PeekNextDamageType() == damageType)
            {
                enemy.TryTakeHit(damageType, dir, character.transform);
                LungeToEnemy(character, enemy, dir);
                character.LastSprintHitEnemy = null;
                return true;
            }
            else
            {
                // Wrong type — normal movement
                character.LastSprintHitEnemy = null;
                return false;
            }
        }

        private void LungeToEnemy(GridCharacter character, Enemy enemy, GridPosition direction)
        {
            // Move character to the tile adjacent to the enemy (one tile before it)
            GridPosition targetTile = enemy.GridPosition - direction;
            character.AnimateMoveTo(targetTile);

            // If joined, move the other character too
            if (_isJoined)
            {
                if (character == robot)
                    dog.AnimateMoveTo(targetTile);
                else
                    robot.AnimateMoveTo(targetTile);

                UpdateJoinedOffsets();
            }

            // Lunge juice
            Vector2 dir2d = new Vector2(direction.x, direction.y);
            GameFeelManager.Squash(character.transform, dir2d);
        }

        private void ClearSprintFollowUp()
        {
            _sprintHitEnemy = null;
            robot.LastSprintHitEnemy = null;
            dog.LastSprintHitEnemy = null;
        }

        // === POLE INPUT ===

        /// <summary>
        /// Returns true if the given pole is currently held by the other character.
        /// Used to block both characters from grabbing the same pole.
        /// </summary>
        private bool IsPoleHeldByOther(GridCharacter character, Pole pole)
        {
            GridCharacter other = (character == robot) ? (GridCharacter)dog : robot;
            return other.HeldPole == pole;
        }

        /// <summary>
        /// Wraps TryMove with a same-pole double-grab guard.
        /// If the destination holds a pole already held by the other character, the move is blocked.
        /// </summary>
        private bool TryMoveWithPoleCheck(GridCharacter character, GridPosition direction, GridPosition? knockbackOverride = null)
        {
            GridPosition dest = character.GridPosition + direction;
            Pole poleAtDest = Pole.FindAtPosition(dest);
            if (poleAtDest != null && IsPoleHeldByOther(character, poleAtDest))
                return false; // blocked — other character already holds this pole

            return character.TryMove(direction, knockbackOverride);
        }

        /// <summary>
        /// Routes directional input for a character that is currently holding a pole.
        /// Away = release + move; Toward = no-op; Perpendicular = orbit around pole.
        /// initiator is the character whose input triggered this (determines damage type for swing attacks).
        /// Always returns true (input is consumed).
        /// </summary>
        private bool HandlePoleInput(GridCharacter character, GridPosition direction, GridCharacter initiator)
        {
            GridPosition poleDir = character.PoleDirection;

            // Away from pole — release and move normally
            if (direction.x == -poleDir.x && direction.y == -poleDir.y)
            {
                character.ReleasePole();
                character.TryMove(direction);
                return true;
            }

            // Toward pole — no-op (already at the pole edge, can't move into it)
            if (direction.Equals(poleDir))
                return true;

            // Perpendicular — orbit around the pole
            GridPosition polePos = character.HeldPole.GridPosition;
            GridPosition newPos = polePos + direction;

            // Check blocked by wall or pole at orbit destination
            if (WallManager.Instance.IsWall(newPos) || Pole.FindAtPosition(newPos) != null)
                return true;

            if (!_isJoined)
            {
                GridCharacter other = (character == robot) ? (GridCharacter)dog : robot;
                if (other.GridPosition.Equals(newPos))
                    return true;
            }

            // --- SWING ATTACK: check enemies along the arc ---
            // Determine rotation direction (CCW vs CW)
            GridPosition startRel = character.GridPosition - polePos;
            GridPosition endRel = newPos - polePos;
            int cross = startRel.x * endRel.y - startRel.y * endRel.x;

            DamageType damageType = initiator.GetDamageType();

            // Two arc positions can have enemies:
            // 1) Diagonal tile (corner of arc): characterPos + inputDirection
            // 2) Destination tile (end of arc): newPos itself
            GridPosition diagonalPos = character.GridPosition + direction;
            Enemy diagonalEnemy = Enemy.FindAtPosition(diagonalPos);
            Enemy destEnemy = Enemy.FindAtPosition(newPos);

            // Compute push destinations
            GridPosition diagonalDest = GridPosition.Zero;
            GridPosition destEnemyDest = GridPosition.Zero;

            if (diagonalEnemy != null)
            {
                GridPosition rel = diagonalPos - polePos;
                GridPosition rotated = cross > 0
                    ? new GridPosition(-rel.y, rel.x)
                    : new GridPosition(rel.y, -rel.x);
                diagonalDest = polePos + rotated;
            }

            if (destEnemy != null)
            {
                // Push one tile in the arc continuation direction (tangent at end)
                GridPosition pushDir = cross > 0
                    ? new GridPosition(-endRel.y, endRel.x)
                    : new GridPosition(endRel.y, -endRel.x);
                destEnemyDest = newPos + pushDir;
            }

            // Validate all enemies can be hit and pushed
            if (diagonalEnemy != null)
            {
                if (!diagonalEnemy.IsVulnerable && diagonalEnemy.PeekNextDamageType() != damageType)
                    return true; // wrong type, arc blocked

                if (IsPositionBlocked(diagonalDest, character))
                    return true;
            }

            if (destEnemy != null)
            {
                if (!destEnemy.IsVulnerable && destEnemy.PeekNextDamageType() != damageType)
                    return true; // wrong type, arc blocked

                if (IsPositionBlocked(destEnemyDest, character))
                    return true;

                // If both enemies exist, their destinations must not conflict
                if (diagonalEnemy != null && diagonalDest.Equals(destEnemyDest))
                    return true;
            }

            // Execute swing attacks (diagonal first, then destination)
            if (diagonalEnemy != null)
                ExecuteSwingHit(diagonalEnemy, damageType, initiator, direction, diagonalDest - diagonalPos);

            if (destEnemy != null)
            {
                GridPosition pushDir = destEnemyDest - newPos;
                ExecuteSwingHit(destEnemy, damageType, initiator, direction, pushDir);
            }

            // Execute orbit
            character.StartOrbit(newPos, character.HeldPole);

            if (_isJoined)
            {
                GridCharacter follower = (character == robot) ? (GridCharacter)dog : robot;
                follower.StartOrbit(newPos, character.HeldPole);
            }

            return true;
        }

        /// <summary>
        /// Checks if a grid position is blocked by a wall, pole, enemy, or (when separated) the other character.
        /// </summary>
        private bool IsPositionBlocked(GridPosition pos, GridCharacter holder)
        {
            if (WallManager.Instance.IsWall(pos) ||
                Pole.FindAtPosition(pos) != null ||
                Enemy.FindAtPosition(pos) != null)
                return true;

            if (!_isJoined)
            {
                GridCharacter other = (holder == robot) ? (GridCharacter)dog : robot;
                if (other.GridPosition.Equals(pos))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Hits an enemy as part of a swing attack, applying damage and knockback.
        /// </summary>
        private void ExecuteSwingHit(Enemy enemy, DamageType damageType, GridCharacter initiator,
            GridPosition swingDirection, GridPosition knockbackDir)
        {
            if (enemy.IsVulnerable)
                enemy.TryPush(swingDirection, knockbackDir);
            else
                enemy.TryTakeHit(damageType, swingDirection, initiator.transform,
                    knockbackOverride: knockbackDir);

            SFXManager.PlayMediumHit();
            Vector2 dir2d = new Vector2(swingDirection.x, swingDirection.y);
            GameFeelManager.ScreenShake(0.15f, 0.06f);
            GameFeelManager.HitStop(0.06f);
            GameFeelParticles.HitBurst(enemy.transform.position, dir2d, Color.white, 8);
        }

        // === JOINED / SEPARATE INPUT ===

        private void HandleJoinedInput(GridPosition robotDir, GridPosition dogDir)
        {
            // Check if either character is holding a pole — route to pole input handler
            // When joined, EITHER character's input can control the holder's pole orbit.
            if (robot.SprintState == MoveState.HoldingPole)
            {
                GridCharacter initiator = null;
                GridPosition dir = GridPosition.Zero;
                if (robotDir != GridPosition.Zero) { dir = robotDir; initiator = robot; }
                else if (dogDir != GridPosition.Zero) { dir = dogDir; initiator = dog; }
                if (dir != GridPosition.Zero)
                {
                    HandlePoleInput(robot, dir, initiator);
                    return;
                }
            }
            if (dog.SprintState == MoveState.HoldingPole)
            {
                GridCharacter initiator = null;
                GridPosition dir = GridPosition.Zero;
                if (dogDir != GridPosition.Zero) { dir = dogDir; initiator = dog; }
                else if (robotDir != GridPosition.Zero) { dir = robotDir; initiator = robot; }
                if (dir != GridPosition.Zero)
                {
                    HandlePoleInput(dog, dir, initiator);
                    return;
                }
            }

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

            // If both inputs are the same direction within the window, treat as one move
            // (prevents double-movement when both players press matching keys near-simultaneously)
            if (bothInputsRecent && _lastRobotDirection == _lastDogDirection && _lastRobotDirection != GridPosition.Zero)
            {
                // If we already moved from the first input, consume the second without moving
                if (_hasPendingMovement) return;
            }

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
                StartJoinedSprint(_robotInput.SprintDirection, robot);
                return;
            }
            if (_dogInput.SprintTriggered && _dogInput.IsDirectionHeld(_dogInput.SprintDirection))
            {
                StartJoinedSprint(_dogInput.SprintDirection, dog);
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

                _joinedMovementLeader = leader;

                // Check if the other character is holding a perpendicular direction
                // for knockback redirect (hold direction + tap attack = redirect)
                CharacterInputHandler otherInput = leader == DamageType.Robot ? _dogInput : _robotInput;
                GridPosition otherHeld = otherInput.GetHeldDirection();
                GridPosition? knockbackOverride = null;

                if (otherHeld != GridPosition.Zero && ArePerpendicularDirections(moveDirection, otherHeld))
                {
                    knockbackOverride = otherHeld;
                    Debug.Log($"[PerpPush] Redirect! attack={moveDirection}, knockback={otherHeld}");
                }

                ProcessJoinedAttack(moveDirection, leader, knockbackOverride);
            }
        }

        /// <summary>
        /// Processes a joined move/attack, optionally with a perpendicular knockback redirect.
        /// </summary>
        private void ProcessJoinedAttack(GridPosition dir, DamageType leader, GridPosition? knockbackOverride)
        {
            GridCharacter attacker = leader == DamageType.Robot ? (GridCharacter)robot : dog;
            GridCharacter follower = leader == DamageType.Robot ? (GridCharacter)dog : robot;

            TryMoveWithPoleCheck(attacker, dir, knockbackOverride);
            follower.AnimateMoveTo(attacker.GridPosition);
            UpdateJoinedOffsets();
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
            // Check for pole input first, then fall through to normal movement
            if (robot.SprintState == MoveState.HoldingPole && robotDir != GridPosition.Zero)
                HandlePoleInput(robot, robotDir, robot);
            else if (robotDir != GridPosition.Zero && robot.SprintState == MoveState.Normal)
                TryMoveWithPoleCheck(robot, robotDir);

            if (dog.SprintState == MoveState.HoldingPole && dogDir != GridPosition.Zero)
                HandlePoleInput(dog, dogDir, dog);
            else if (dogDir != GridPosition.Zero && dog.SprintState == MoveState.Normal)
                TryMoveWithPoleCheck(dog, dogDir);
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

            // Joint attack! (TryLaunch plays HeavyHit)
            enemy.TryLaunch(dir);

            // Juice: squash both characters
            Vector2 dir2d = new Vector2(dir.x, dir.y);
            GameFeelManager.Squash(robot.transform, dir2d);
            GameFeelManager.Squash(dog.transform, dir2d);

            // Joint attack VFX: strong screenshake + hit flash + spark burst
            GameFeelManager.ScreenShake(0.2f, 0.1f);
            GameFeelManager.HitStop(0.1f);
            GameFeelManager.ChromaticPulse(0.8f, 0.15f);

            // Flash both characters white
            SpriteRenderer robotSr = robot.GetComponent<SpriteRenderer>();
            SpriteRenderer dogSr = dog.GetComponent<SpriteRenderer>();
            if (robotSr != null) GameFeelManager.Flash(robotSr, 0.1f);
            if (dogSr != null) GameFeelManager.Flash(dogSr, 0.1f);

            // Spark burst at enemy position
            GameFeelParticles.JointAttackBurst(enemy.transform.position, dir2d);

            // Clear tap tracking
            _lastRobotTapDir = GridPosition.Zero;
            _lastDogTapDir = GridPosition.Zero;

            return true;
        }

        // === SPRINT POLE GRAB ===

        /// <summary>
        /// While sprinting, check if any input is held perpendicular to the sprint direction.
        /// If so, look for a pole adjacent in that direction. Grabs the first aligned pole
        /// during cell crossings for reliable detection.
        /// </summary>
        bool TrySprintPoleGrab(GridCharacter character, CharacterInputHandler input)
        {
            if (character.SprintState != MoveState.Sprinting) return false;

            // Check all perpendicular held directions
            GridPosition sprintDir = character.SprintDirection;
            GridPosition heldDir = input.GetHeldDirection();
            if (heldDir == GridPosition.Zero) return false;

            // Must be perpendicular to sprint direction
            if (!ArePerpendicularDirections(sprintDir, heldDir)) return false;

            GridPosition perpPolePos = character.GridPosition + heldDir;
            Pole perpPole = Pole.FindAtPosition(perpPolePos);
            if (perpPole == null) return false;

            // Calculate opposite side destination: pole position + held direction
            GridPosition exitPos = perpPolePos + heldDir;
            if (!WallManager.Instance.IsWall(exitPos) &&
                Enemy.FindAtPosition(exitPos) == null &&
                Pole.FindAtPosition(exitPos) == null)
            {
                character.StartSprintOrbit(perpPole, heldDir, exitPos);
                return true;
            }
            else
            {
                // Blocked — fall back to normal hold
                character.StopSprintImmediate();
                character.EnterPoleHold(perpPole, heldDir);
                return true;
            }
        }

        // === SPRINT MANAGEMENT ===

        private bool IsAnySprinting()
        {
            // HoldingPole is not a sprint state — pole input is handled separately
            bool robotActive = robot.SprintState == MoveState.Sprinting || robot.SprintState == MoveState.Braking;
            bool dogActive = dog.SprintState == MoveState.Sprinting || dog.SprintState == MoveState.Braking;
            return robotActive || dogActive;
        }

        // Track who initiated the joined sprint for damage type matching
        private GridCharacter _joinedSprintInitiator;

        private void StartJoinedSprint(GridPosition direction, GridCharacter initiator)
        {
            _activeSprintDir = direction;
            _joinedSprintInitiator = initiator;
            ClearSprintFollowUp();
            // Suppress step sounds on follower so we don't get double steps
            dog.SuppressStepSound = true;
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

        private bool IsSprintOrBrake(MoveState state)
        {
            return state == MoveState.Sprinting || state == MoveState.Braking;
        }

        private void HandleJoinedSprintUpdate()
        {
            // Only care if currently sprinting or braking — HoldingPole is handled by HandleLinkedInput
            if (!IsSprintOrBrake(robot.SprintState) && !IsSprintOrBrake(dog.SprintState))
            {
                dog.SuppressStepSound = false;
                _leaderWasOrbiting = false;
                return;
            }

            // Resolve leader/follower based on who initiated the sprint
            GridCharacter sprintLeader = (_joinedSprintInitiator == robot) ? (GridCharacter)robot : dog;
            GridCharacter sprintFollower = (sprintLeader == robot) ? (GridCharacter)dog : robot;
            CharacterInputHandler leaderInput = (sprintLeader == robot) ? _robotInput : _dogInput;
            CharacterInputHandler followerInput = (sprintLeader == robot) ? _dogInput : _robotInput;

            // --- ORBIT GUARD ---
            // While the leader is doing a sprint orbit, the follower stays stacked and we skip
            // all normal sprint/brake logic to prevent it from force-stopping the leader.
            if (sprintLeader.IsOrbiting)
            {
                sprintFollower.transform.position = sprintLeader.transform.position;
                _leaderWasOrbiting = true;
                return;
            }

            // --- POST-ORBIT FOLLOWER RESYNC ---
            // The frame after orbit completes: leader transitions back to Sprinting,
            // follower is in Normal (was stopped before orbit). Re-enter follower sprint
            // and sync their grid position to match the leader's new post-orbit position.
            if (_leaderWasOrbiting && sprintLeader.SprintState == MoveState.Sprinting
                && sprintFollower.SprintState == MoveState.Normal)
            {
                sprintFollower.TeleportTo(sprintLeader.GridPosition);
                sprintFollower.StartSprint(sprintLeader.SprintDirection);
                _activeSprintDir = sprintLeader.SprintDirection;
                _leaderWasOrbiting = false;
            }
            else
            {
                _leaderWasOrbiting = false;
            }

            // Check for perpendicular sprint pole grab (either character's input can trigger)
            if (sprintLeader.SprintState == MoveState.Sprinting)
            {
                if (TrySprintPoleGrab(sprintLeader, leaderInput) || TrySprintPoleGrab(sprintLeader, followerInput))
                {
                    // Stop the follower's sprint so they wait while leader orbits
                    sprintFollower.StopSprintImmediate();
                    dog.SuppressStepSound = false;
                    return;
                }
            }

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
                    dog.SuppressStepSound = false;
                    ResetAllSprintInput();
                }
            }

            // If one character's sprint ended (e.g. hit an enemy) while the other is still going,
            // force-end the other to keep them in sync.
            // Only applies to actual sprint/brake states, NOT HoldingPole.
            if (robot.SprintState == MoveState.Normal && IsSprintOrBrake(dog.SprintState))
            {
                dog.StopSprintImmediate();
                dog.SuppressStepSound = false;
            }
            else if (dog.SprintState == MoveState.Normal && IsSprintOrBrake(robot.SprintState))
            {
                robot.StopSprintImmediate();
                dog.SuppressStepSound = false;
            }

            // Keep follower synced during sprint/brake
            if (IsSprintOrBrake(robot.SprintState) || IsSprintOrBrake(dog.SprintState))
            {
                // Sync follower position to leader during joined sprint
                sprintFollower.transform.position = sprintLeader.transform.position;
            }

            // When both finish, clean up
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
            {
                _activeSprintDir = GridPosition.Zero;
                ResetAllSprintInput();
                dog.SuppressStepSound = false;
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

            // Check for perpendicular sprint pole grab
            if (TrySprintPoleGrab(character, input)) return;

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

        private bool ArePerpendicularDirections(GridPosition dir1, GridPosition dir2)
        {
            if (dir1 == GridPosition.Zero || dir2 == GridPosition.Zero)
                return false;

            // Dot product of cardinal unit vectors: 0 means perpendicular
            return dir1.x * dir2.x + dir1.y * dir2.y == 0;
        }

        private void SetJoinedSprintOverrides(bool joined)
        {
            if (joined)
            {
                System.Action<Enemy, GridPosition> handler = HandleJoinedSprintEnemyHit;
                robot.SprintEnemyHitOverride = handler;
                dog.SprintEnemyHitOverride = handler;
            }
            else
            {
                robot.SprintEnemyHitOverride = null;
                dog.SprintEnemyHitOverride = null;
            }
        }

        private void HandleJoinedSprintEnemyHit(Enemy enemy, GridPosition direction)
        {
            if (enemy.IsVulnerable)
            {
                enemy.TryPush(direction);
                _sprintHitEnemy = enemy;
                _sprintHitDirection = direction;
                return;
            }

            // Use the sprint initiator's damage type — mismatched type fails like a normal attack
            GridCharacter attacker = _joinedSprintInitiator != null ? _joinedSprintInitiator : robot;
            DamageType damageType = attacker.GetDamageType();
            SFXManager.PlayMediumHit();
            if (enemy.TryTakeHit(damageType, direction, attacker.transform, suppressAudio: true))
            {
                _sprintHitEnemy = enemy;
                _sprintHitDirection = direction;
            }
        }

        private void Separate(GridPosition robotDir, GridPosition dogDir)
        {
            Debug.Log("Separating characters!");

            _isJoined = false;
            SetJoinedSprintOverrides(false);
            robot.SuppressStepSound = false;
            dog.SuppressStepSound = false;

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
                SetJoinedSprintOverrides(true);
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
