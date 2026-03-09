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

                if (robotDir == _sprintHitDirection && robotDir != GridPosition.Zero)
                {
                    inputDir = robotDir;
                    attacker = robot;
                }
                else if (dogDir == _sprintHitDirection && dogDir != GridPosition.Zero)
                {
                    inputDir = dogDir;
                    attacker = dog;
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

                if (enemy.IsVulnerable)
                {
                    // Hit first so enemy moves, then lunge to its new position
                    enemy.TryPush(dir);
                    LungeToEnemy(attacker, enemy, dir);
                    ClearSprintFollowUp();
                    return true;
                }

                // Check if damage type matches what the enemy needs
                if (enemy.PeekNextDamageType() == damageType)
                {
                    // Hit first so enemy moves, then lunge to its new position
                    enemy.TryTakeHit(damageType, dir, attacker.transform);
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

            // Visual effects while charging
            var feelSettings = GameFeelManager.Instance?.Settings;
            float shakeIntensity = feelSettings?.chargeShakeIntensity ?? 0.02f;

            UpdateChargeVisuals(_robotInput, robot, shakeIntensity);
            UpdateChargeVisuals(_dogInput, dog, shakeIntensity);

            // Check for charge release
            HandleChargeRelease(_robotInput, robot);
            HandleChargeRelease(_dogInput, dog);
        }

        // Charge visual state tracking
        private float _robotChargeParticleTimer;
        private float _dogChargeParticleTimer;
        private SpriteRenderer _robotSr;
        private SpriteRenderer _dogSr;
        private Color _robotOriginalColor;
        private Color _dogOriginalColor;
        private bool _robotWasCharging;
        private bool _dogWasCharging;

        // Charge bar UI
        private GameObject _robotChargeBar;
        private GameObject _robotChargeBarFill;
        private SpriteRenderer _robotChargeBarBg;
        private SpriteRenderer _robotChargeBarFg;
        private GameObject _dogChargeBar;
        private GameObject _dogChargeBarFill;
        private SpriteRenderer _dogChargeBarBg;
        private SpriteRenderer _dogChargeBarFg;

        private float ChargeBarFillDuration => SettingsManager.Instance?.settings?.chargeBarFillDuration ?? 2f;

        private void UpdateChargeVisuals(CharacterInputHandler input, GridCharacter character, float shakeIntensity)
        {
            bool isCharging = input.IsCharging;
            bool isRobot = (character == robot);
            ref float particleTimer = ref (isRobot ? ref _robotChargeParticleTimer : ref _dogChargeParticleTimer);
            ref bool wasCharging = ref (isRobot ? ref _robotWasCharging : ref _dogWasCharging);

            // Get/cache sprite renderer
            SpriteRenderer sr = isRobot ? _robotSr : _dogSr;
            if (sr == null)
            {
                sr = character.GetComponent<SpriteRenderer>();
                if (isRobot) { _robotSr = sr; _robotOriginalColor = sr != null ? sr.color : Color.white; }
                else { _dogSr = sr; _dogOriginalColor = sr != null ? sr.color : Color.white; }
            }
            Color originalColor = isRobot ? _robotOriginalColor : _dogOriginalColor;

            if (isCharging)
            {
                float chargeProgress = input.ChargeProgress;

                // Shake intensifies with charge
                GameFeelManager.ObjectShake(character.transform, Vector2.one, Time.deltaTime,
                    shakeIntensity * (0.5f + chargeProgress * 1.5f));

                // White pulse effect — oscillate between original color and white
                if (sr != null)
                {
                    float pulse = Mathf.Sin(Time.time * (8f + chargeProgress * 12f)) * 0.5f + 0.5f;
                    sr.color = Color.Lerp(originalColor, Color.white, pulse * chargeProgress);
                }

                // Particles sucked toward character center
                particleTimer += Time.deltaTime;
                float spawnInterval = Mathf.Lerp(0.15f, 0.04f, chargeProgress);
                if (particleTimer >= spawnInterval)
                {
                    particleTimer = 0f;
                    GameFeelParticles.ChargeSuckParticle(character.transform.position);
                }

                // Show/update charge bar
                EnsureChargeBar(isRobot, character);
                UpdateChargeBar(isRobot, character, chargeProgress);

                wasCharging = true;
            }
            else if (wasCharging)
            {
                // Restore original color
                if (sr != null) sr.color = originalColor;
                particleTimer = 0f;
                wasCharging = false;

                // Hide charge bar
                HideChargeBar(isRobot);
            }
        }

        private void EnsureChargeBar(bool isRobot, GridCharacter character)
        {
            ref GameObject bar = ref (isRobot ? ref _robotChargeBar : ref _dogChargeBar);
            ref GameObject fill = ref (isRobot ? ref _robotChargeBarFill : ref _dogChargeBarFill);
            ref SpriteRenderer bgSr = ref (isRobot ? ref _robotChargeBarBg : ref _dogChargeBarBg);
            ref SpriteRenderer fgSr = ref (isRobot ? ref _robotChargeBarFg : ref _dogChargeBarFg);

            if (bar != null) return;

            // Create bar container
            bar = new GameObject(isRobot ? "RobotChargeBar" : "DogChargeBar");
            bar.transform.SetParent(character.transform);
            bar.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            bar.transform.localScale = Vector3.one;

            // Background (dark grey)
            GameObject bg = new GameObject("ChargeBg");
            bg.transform.SetParent(bar.transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(0.6f, 0.08f, 1f);
            bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            bgSr.sortingOrder = 10;
            // Use the square particle sprite if available
            if (GameFeelParticles.Instance != null)
            {
                var particlePrefabSr = GameFeelParticles.Instance.GetSquareSprite();
                if (particlePrefabSr != null) bgSr.sprite = particlePrefabSr;
            }

            // Fill (white)
            fill = new GameObject("ChargeFill");
            fill.transform.SetParent(bar.transform);
            fill.transform.localPosition = new Vector3(-0.3f, 0f, 0f);
            fill.transform.localScale = new Vector3(0f, 0.06f, 1f);
            fgSr = fill.AddComponent<SpriteRenderer>();
            fgSr.color = Color.white;
            fgSr.sortingOrder = 11;
            if (GameFeelParticles.Instance != null)
            {
                var particlePrefabSr = GameFeelParticles.Instance.GetSquareSprite();
                if (particlePrefabSr != null) fgSr.sprite = particlePrefabSr;
            }
        }

        private void UpdateChargeBar(bool isRobot, GridCharacter character, float progress)
        {
            GameObject fill = isRobot ? _robotChargeBarFill : _dogChargeBarFill;
            GameObject bar = isRobot ? _robotChargeBar : _dogChargeBar;
            if (fill == null || bar == null) return;

            bar.SetActive(true);

            // Scale the fill bar width based on progress
            float maxWidth = 0.6f;
            float fillWidth = maxWidth * progress;
            fill.transform.localScale = new Vector3(fillWidth, 0.06f, 1f);
            // Anchor fill to left side of bar
            fill.transform.localPosition = new Vector3(-maxWidth / 2f + fillWidth / 2f, 0f, 0f);
        }

        private void HideChargeBar(bool isRobot)
        {
            GameObject bar = isRobot ? _robotChargeBar : _dogChargeBar;
            if (bar != null) bar.SetActive(false);
        }

        private void HandleChargeRelease(CharacterInputHandler input, GridCharacter character)
        {
            if (!input.ChargeReleased) return;

            float chargeAmount = input.ReleasedChargeProgress;
            GridPosition dir = input.ChargeDirection;
            GridPosition targetPos = character.GridPosition + dir;
            Enemy enemy = character.FindVulnerableEnemyAt(targetPos);

            if (enemy != null)
            {
                if (chargeAmount <= 0f)
                {
                    // 0% charge = regular push (no launch)
                    enemy.TryPush(dir);
                    SFXManager.PlayMediumHit();
                }
                else
                {
                    // Launch with strength based on charge amount (TryLaunch plays HeavyHit)
                    enemy.TryLaunch(dir, chargeAmount);
                }

                // Squash the attacker (stronger with more charge)
                Vector2 dir2d = new Vector2(dir.x, dir.y);
                GameFeelManager.Squash(character.transform, dir2d);

                // Scale effects with charge
                if (chargeAmount > 0f)
                {
                    GameFeelManager.ScreenShake(0.1f + chargeAmount * 0.1f, 0.04f + chargeAmount * 0.06f);
                    GameFeelManager.ChromaticPulse(0.3f + chargeAmount * 0.5f, 0.1f);
                }
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

        // === SPRINT MANAGEMENT ===

        private bool IsAnySprinting()
        {
            return robot.SprintState != MoveState.Normal || dog.SprintState != MoveState.Normal;
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

        private void HandleJoinedSprintUpdate()
        {
            // Only care if currently sprinting or braking
            if (robot.SprintState == MoveState.Normal && dog.SprintState == MoveState.Normal)
            {
                dog.SuppressStepSound = false;
                return;
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
            // force-end the other to keep them in sync
            if (robot.SprintState == MoveState.Normal && dog.SprintState != MoveState.Normal)
            {
                dog.StopSprintImmediate();
                dog.SuppressStepSound = false;
            }
            else if (dog.SprintState == MoveState.Normal && robot.SprintState != MoveState.Normal)
            {
                robot.StopSprintImmediate();
                dog.SuppressStepSound = false;
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
