// GridCharacter.cs
// Base class for any character that moves on the grid.
// Both Robot and Dog will inherit from this, sharing this core functionality.
// Supports normal grid movement and continuous sprint movement.

using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Enemies;
using DogAndRobot.Environment;

namespace DogAndRobot.Characters
{
    public enum MoveState { Normal, Sprinting, Braking, HoldingPole }

    public class GridCharacter : MonoBehaviour
    {
        // === GRID STATE ===
        [Header("Debug Info")]
        [SerializeField]
        private GridPosition _gridPosition;

        public GridPosition GridPosition
        {
            get => _gridPosition;
            protected set => _gridPosition = value;
        }

        // === MOVEMENT STATE ===
        public bool IsMoving { get; private set; }

        private Vector3 _targetWorldPosition;
        private Vector3 _moveStartPosition;
        private float _moveProgress = 0f;

        // Visual offset from grid position (used when joined)
        private Vector3 _visualOffset = Vector3.zero;



        /// <summary>
        /// When set, sprint enemy collisions call this instead of attacking directly.
        /// Used by CharacterLinkManager for joined sprint to try both damage types.
        /// Parameters: enemy, sprint direction.
        /// </summary>
        public System.Action<Enemy, GridPosition> SprintEnemyHitOverride { get; set; }

        /// <summary>
        /// When true, this character won't play step sounds (used for the follower in joined sprint).
        /// </summary>
        public bool SuppressStepSound { get; set; }

        /// <summary>
        /// Set after a successful sprint hit. CharacterLinkManager reads and clears this
        /// to enable the lunge follow-up mechanic.
        /// </summary>
        public Enemy LastSprintHitEnemy { get; set; }
        public GridPosition LastSprintHitDirection { get; set; }

        // Sprint step sound: only play every other cell crossing
        private int _sprintStepCounter;

        // === SPRINT STATE ===
        public MoveState SprintState => _sprintState;
        private MoveState _sprintState = MoveState.Normal;
        private GridPosition _sprintDirection;
        public GridPosition SprintDirection => _sprintDirection;
        private float _sprintSpeed;
        private float _sprintElapsed;
        private Vector3 _brakeStartPos;
        private float _lastSkidTime;
        // Store the scale before sprint started so we can restore it
        private Vector3 _preSprintScale;

        // === SPRINT ORBIT STATE ===
        bool _isSprintOrbit;
        GridPosition _postOrbitSprintDirection;
        float _postOrbitSprintSpeed;

        // === POLE HOLD STATE ===
        Pole _heldPole;
        GridPosition _poleDirection; // direction from character TO pole

        public Pole HeldPole => _heldPole;
        public GridPosition PoleDirection => _poleDirection;
        // Note: use existing SprintState property to check MoveState externally

        // === ORBIT ANIMATION STATE ===
        bool _isOrbiting;
        Vector3 _orbitCenter;      // pole world position
        float _orbitStartAngle;    // radians
        float _orbitEndAngle;      // radians
        float _orbitProgress;      // 0 to 1
        float _orbitDuration;      // seconds
        GridPosition _orbitTargetGridPos;

        public bool IsOrbiting => _isOrbiting;

        // === SETTINGS ACCESS ===
        private float CellSize => SettingsManager.Instance?.settings?.cellSize ?? 1f;
        private float MoveSpeed => SettingsManager.Instance?.settings?.moveSpeed ?? 10f;
        private float ArrivalThreshold => SettingsManager.Instance?.settings?.arrivalThreshold ?? 0.01f;
        private float SprintSpeedBoost => SettingsManager.Instance?.settings?.sprintSpeedBoost ?? 1.25f;
        private float SprintRampDuration => SettingsManager.Instance?.settings?.sprintRampDuration ?? 0.5f;
        private float SprintBrakeDeceleration => SettingsManager.Instance?.settings?.sprintBrakeDeceleration ?? 40f;
        private float SprintBrakeOvershoot => SettingsManager.Instance?.settings?.sprintBrakeOvershoot ?? 3f;
        // Base sprint speed matches regular movement: cellSize * moveSpeed
        private float SprintBaseSpeed => CellSize * MoveSpeed;
        private float SprintMaxSpeed => SprintBaseSpeed * SprintSpeedBoost;

        // === UNITY LIFECYCLE METHODS ===

        protected virtual void Awake()
        {
            _gridPosition = GridPosition.FromWorldPosition(transform.position, CellSize);
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition;
        }

        protected virtual void Update()
        {
            if (_isOrbiting)
            {
                UpdateOrbitAnimation();
                return; // skip normal visual update, sprint update, etc.
            }

            if (_sprintState == MoveState.Sprinting)
            {
                UpdateSprinting();
            }
            else if (_sprintState == MoveState.Braking)
            {
                UpdateBraking();
            }
            else
            {
                UpdateVisualPosition();
            }
        }

        // === MOVEMENT METHODS ===

        /// <summary>
        /// Attempts to move the character one grid cell in the given direction.
        /// Returns true if the move was successful, false if blocked.
        /// </summary>
        public bool TryMove(GridPosition direction, GridPosition? knockbackOverride = null)
        {
            // Calculate where we'd end up
            GridPosition newPosition = _gridPosition + direction;

            // Check if there's an enemy at the target position
            Enemy enemy = FindEnemyAtPosition(newPosition);
            if (enemy != null)
            {
                GridPosition enemyPosBefore = enemy.GridPosition;

                // If enemy is vulnerable, push it instead of attacking
                if (enemy.IsVulnerable)
                {
                    if (enemy.TryPush(direction, knockbackOverride))
                    {
                        // Only move into the space if the enemy actually moved out
                        if (enemy.GridPosition != enemyPosBefore)
                        {
                            _gridPosition = newPosition;
                            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                            IsMoving = true;
                        }
                        return true;
                    }
                    return false;
                }

                // Try to attack the enemy
                DamageType damageType = GetDamageType();
                bool hit = enemy.TryTakeHit(damageType, direction, transform, knockbackOverride: knockbackOverride);

                if (hit)
                {
                    // Only follow up into the space if the enemy actually moved out of it
                    if (enemy == null || enemy.GridPosition != enemyPosBefore)
                    {
                        _gridPosition = newPosition;
                        _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                        IsMoving = true;
                    }
                    return true;
                }

                return false;
            }

            // Check if there's a pole at the target position
            Pole pole = Pole.FindAtPosition(newPosition);
            if (pole != null)
            {
                EnterPoleHold(pole, direction);
                return true;
            }

            // Check if the move is valid
            if (CanMoveTo(newPosition))
            {
                _gridPosition = newPosition;
                _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                IsMoving = true;
                if (!SuppressStepSound) SFXManager.PlayStep();
                return true;
            }

            return false;
        }

        private Enemy FindEnemyAtPosition(GridPosition position)
        {
            return Enemy.FindAtPosition(position);
        }

        /// <summary>
        /// Finds a vulnerable enemy at the given grid position. Used by CharacterLinkManager for charge/joint attacks.
        /// </summary>
        public Enemy FindVulnerableEnemyAt(GridPosition position)
        {
            Enemy enemy = FindEnemyAtPosition(position);
            if (enemy != null && enemy.IsVulnerable)
                return enemy;
            return null;
        }

        public virtual DamageType GetDamageType()
        {
            return DamageType.Robot;
        }

        public void TeleportTo(GridPosition position)
        {
            _gridPosition = position;
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition + _visualOffset;
            IsMoving = false;
            _moveProgress = 0f;
        }

        public void AnimateMoveTo(GridPosition position)
        {
            _gridPosition = position;
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            IsMoving = true;
            _moveProgress = 0f;
        }

        public void StartOrbit(GridPosition targetGridPos, Pole pole, float speedOverride = -1f)
        {
            float cellSize = SettingsManager.Instance.settings.cellSize;
            _orbitCenter = pole.GridPosition.ToWorldPosition(cellSize);

            Vector3 startWorld = _gridPosition.ToWorldPosition(cellSize);
            Vector3 endWorld = targetGridPos.ToWorldPosition(cellSize);

            Vector3 startOffset = startWorld - _orbitCenter;
            Vector3 endOffset = endWorld - _orbitCenter;

            _orbitStartAngle = Mathf.Atan2(startOffset.y, startOffset.x);
            _orbitEndAngle = Mathf.Atan2(endOffset.y, endOffset.x);

            // Determine shortest arc direction
            float angleDiff = _orbitEndAngle - _orbitStartAngle;
            if (angleDiff > Mathf.PI) angleDiff -= 2f * Mathf.PI;
            if (angleDiff < -Mathf.PI) angleDiff += 2f * Mathf.PI;
            _orbitEndAngle = _orbitStartAngle + angleDiff;

            // Duration based on arc length and speed
            float arcLength = Mathf.Abs(angleDiff) * cellSize; // radius = cellSize
            float speed = speedOverride > 0 ? speedOverride
                : SprintBaseSpeed; // cellSize * moveSpeed = world units/sec
            _orbitDuration = arcLength / speed;

            _orbitTargetGridPos = targetGridPos;
            _orbitProgress = 0f;
            _isOrbiting = true;
            IsMoving = true;

            // Update grid position immediately
            _gridPosition = targetGridPos;

            // Update pole direction
            _poleDirection = pole.GridPosition - targetGridPos;
        }

        void UpdateOrbitAnimation()
        {
            if (!_isOrbiting) return;

            float cellSize = SettingsManager.Instance.settings.cellSize;
            _orbitProgress += Time.deltaTime / _orbitDuration;

            if (_orbitProgress >= 1f)
            {
                _orbitProgress = 1f;
                _isOrbiting = false;

                transform.position = _orbitTargetGridPos.ToWorldPosition(cellSize) + _visualOffset;

                if (_isSprintOrbit)
                {
                    _isSprintOrbit = false;
                    ReleasePole();
                    _sprintDirection = _postOrbitSprintDirection;
                    _sprintSpeed = _postOrbitSprintSpeed;
                    _sprintState = MoveState.Sprinting;
                    _sprintElapsed = SprintRampDuration; // already at full speed
                    _preSprintScale = transform.localScale;
                    IsMoving = false; // sprint manages its own movement
                    ApplySprintSquashStretch();
                }
                else
                {
                    IsMoving = false;
                }
                return;
            }

            // Smooth interpolation
            float t = _orbitProgress;
            float angle = Mathf.Lerp(_orbitStartAngle, _orbitEndAngle, t);
            float radius = cellSize;

            Vector3 pos = _orbitCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
            transform.position = pos + _visualOffset;
        }

        protected virtual bool CanMoveTo(GridPosition position)
        {
            if (WallManager.Instance != null && WallManager.Instance.IsWall(position))
                return false;
            return true;
        }

        // === POLE HOLD METHODS ===

        public void EnterPoleHold(Pole pole, GridPosition directionToPole)
        {
            _sprintState = MoveState.HoldingPole;
            _heldPole = pole;
            _poleDirection = directionToPole;
            pole.SetHolderColor(GetHolderColor());
        }

        public void ReleasePole()
        {
            if (_heldPole != null)
            {
                _heldPole.ResetColor();
                _heldPole = null;
            }
            _poleDirection = GridPosition.Zero;
            _sprintState = MoveState.Normal;
        }

        Color GetHolderColor()
        {
            return GetDamageType() == DamageType.Dog
                ? new Color(1f, 0.831f, 0.631f, 1f)    // #FFD4A1
                : new Color(0.631f, 0.655f, 1f, 1f);    // #A1A7FF
        }

        // === SPRINT METHODS ===

        /// <summary>
        /// Grabs a pole perpendicular to the sprint direction and orbits 180 degrees around it,
        /// resuming sprint in the opposite direction on completion.
        /// </summary>
        public void StartSprintOrbit(Pole pole, GridPosition grabDirection, GridPosition exitPos)
        {
            float currentSpeed = _sprintSpeed;
            GridPosition exitSprintDir = new GridPosition(-_sprintDirection.x, -_sprintDirection.y);

            // Stop sprint state temporarily for orbit
            StopSprintImmediate();
            _sprintState = MoveState.HoldingPole;
            _heldPole = pole;
            pole.SetHolderColor(GetHolderColor());

            // Start 180-degree orbit with sprint speed
            StartOrbit(exitPos, pole, currentSpeed);

            // Store post-orbit sprint info
            _postOrbitSprintDirection = exitSprintDir;
            _postOrbitSprintSpeed = currentSpeed;
            _isSprintOrbit = true;
        }

        /// <summary>
        /// Start sprinting in the given direction.
        /// </summary>
        public void StartSprint(GridPosition direction)
        {
            _sprintState = MoveState.Sprinting;
            _sprintDirection = direction;
            _sprintSpeed = SprintBaseSpeed;
            _sprintElapsed = 0f;
            _sprintStepCounter = 0;
            _preSprintScale = transform.localScale;
            IsMoving = true;

            SFXManager.PlaySprintStart();

            // Burst particles at start
            Vector2 dir2d = new Vector2(direction.x, direction.y);
            GameFeelParticles.SprintBurst(transform.position, dir2d);

            // Apply initial squash/stretch
            ApplySprintSquashStretch();
        }

        /// <summary>
        /// Begin braking from sprint (decelerate to stop).
        /// </summary>
        public void StartBraking()
        {
            if (_sprintState != MoveState.Sprinting) return;
            _sprintState = MoveState.Braking;
            _brakeStartPos = transform.position;
            _lastSkidTime = Time.time;
        }

        /// <summary>
        /// Immediately stop sprint (for opposite-direction cancel).
        /// </summary>
        public void StopSprintImmediate()
        {
            SnapToNearestGrid();
            EndSprint();
        }

        private void EndSprint()
        {
            _sprintState = MoveState.Normal;
            _sprintSpeed = 0f;
            IsMoving = false;
            // Restore scale
            transform.localScale = _preSprintScale;
        }

        private void SnapToNearestGrid()
        {
            _gridPosition = GridPosition.FromWorldPosition(transform.position, CellSize);
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition + _visualOffset;
        }

        private void UpdateSprinting()
        {
            float dt = Time.deltaTime;

            // Respect hitstop
            if (GameFeelManager.Instance != null && GameFeelManager.Instance.IsHitStopped) return;

            // Ramp speed from base (regular move speed) to max (base * boost) over ramp duration
            _sprintElapsed += dt;
            if (_sprintElapsed >= SprintRampDuration)
            {
                _sprintSpeed = SprintMaxSpeed;
            }
            else
            {
                float t = _sprintElapsed / SprintRampDuration;
                float smooth = t * t * (3f - 2f * t); // smoothstep
                _sprintSpeed = Mathf.Lerp(SprintBaseSpeed, SprintMaxSpeed, smooth);
            }

            // Move in world space
            Vector3 moveDir = new Vector3(_sprintDirection.x, _sprintDirection.y, 0f);
            transform.position += moveDir * _sprintSpeed * dt;

            // Update grid position as we cross cell boundaries
            GridPosition newGridPos = GridPosition.FromWorldPosition(transform.position, CellSize);
            if (newGridPos != _gridPosition)
            {
                // Check wall or pole at the new cell
                Pole sprintPole = Pole.FindAtPosition(newGridPos);
                if (!CanMoveTo(newGridPos) || sprintPole != null)
                {
                    if (sprintPole != null)
                    {
                        // Head-on sprint into pole — stop and enter hold
                        StopSprintImmediate();
                        EnterPoleHold(sprintPole, _sprintDirection);
                    }
                    else
                    {
                        // Hit a wall — snap back to last valid cell and end sprint
                        _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                        transform.position = _targetWorldPosition + _visualOffset;

                        Vector2 impactDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                        GameFeelParticles.WallExplosion(transform.position + (Vector3)(impactDir * CellSize * 0.5f), impactDir);

                        GameFeelManager.ScreenShake(0.15f, 0.06f);

                        EndSprint();
                    }
                    return;
                }

                // Check for enemy at the new cell
                Enemy sprintEnemy = FindEnemyAtPosition(newGridPos);
                if (sprintEnemy != null)
                {
                    // Snap back to tile before the enemy
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    transform.position = _targetWorldPosition + _visualOffset;
                    EndSprint();

                    if (SprintEnemyHitOverride != null)
                    {
                        SprintEnemyHitOverride(sprintEnemy, _sprintDirection);
                    }
                    else
                    {
                        // Attack with same type matching as regular attacks
                        if (sprintEnemy.IsVulnerable)
                        {
                            sprintEnemy.TryPush(_sprintDirection);
                            LastSprintHitEnemy = sprintEnemy;
                            LastSprintHitDirection = _sprintDirection;
                        }
                        else
                        {
                            DamageType damageType = GetDamageType();
                            if (sprintEnemy.TryTakeHit(damageType, _sprintDirection, transform, suppressAudio: true))
                            {
                                LastSprintHitEnemy = sprintEnemy;
                                LastSprintHitDirection = _sprintDirection;
                            }
                        }
                    }

                    SFXManager.PlayMediumHit();

                    // Extra slam juice
                    Vector2 slamDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                    GameFeelManager.ScreenShake(0.2f, 0.08f);
                    GameFeelManager.HitStop(0.08f);
                    GameFeelParticles.HitBurst(sprintEnemy.transform.position, slamDir, Color.white, 10);
                    GameFeelParticles.ImpactFlash(sprintEnemy.transform.position, slamDir, Color.white, 1.6f);
                    return;
                }

                _gridPosition = newGridPos;
                _sprintStepCounter++;
                if (!SuppressStepSound && _sprintStepCounter % 2 == 0)
                    SFXManager.PlayStep();
            }

            // Subtle screen shake rumble
            var feel = GameFeelManager.Instance?.Settings;
            float shakeIntensity = feel != null ? feel.sprintShakeIntensity : 0.01f;
            GameFeelManager.ScreenShake(dt, shakeIntensity);

            ApplySprintSquashStretch();
        }

        private void UpdateBraking()
        {
            float dt = Time.deltaTime;

            if (GameFeelManager.Instance != null && GameFeelManager.Instance.IsHitStopped) return;

            // Decelerate
            _sprintSpeed = Mathf.MoveTowards(_sprintSpeed, 0f, SprintBrakeDeceleration * dt);

            // Continue moving
            Vector3 moveDir = new Vector3(_sprintDirection.x, _sprintDirection.y, 0f);
            transform.position += moveDir * _sprintSpeed * dt;

            // Update grid position
            GridPosition newGridPos = GridPosition.FromWorldPosition(transform.position, CellSize);
            if (newGridPos != _gridPosition)
            {
                if (!CanMoveTo(newGridPos) || Pole.FindAtPosition(newGridPos) != null)
                {
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    transform.position = _targetWorldPosition + _visualOffset;

                    Vector2 impactDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                    GameFeelParticles.WallExplosion(transform.position + (Vector3)(impactDir * CellSize * 0.5f), impactDir);

                    EndSprint();
                    return;
                }

                // Check for enemy at the new cell (same as sprinting)
                Enemy brakeEnemy = FindEnemyAtPosition(newGridPos);
                if (brakeEnemy != null)
                {
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    transform.position = _targetWorldPosition + _visualOffset;
                    EndSprint();

                    if (SprintEnemyHitOverride != null)
                    {
                        SprintEnemyHitOverride(brakeEnemy, _sprintDirection);
                    }
                    else
                    {
                        if (brakeEnemy.IsVulnerable)
                        {
                            brakeEnemy.TryPush(_sprintDirection);
                            LastSprintHitEnemy = brakeEnemy;
                            LastSprintHitDirection = _sprintDirection;
                        }
                        else
                        {
                            DamageType damageType = GetDamageType();
                            if (brakeEnemy.TryTakeHit(damageType, _sprintDirection, transform, suppressAudio: true))
                            {
                                LastSprintHitEnemy = brakeEnemy;
                                LastSprintHitDirection = _sprintDirection;
                            }
                        }
                    }

                    SFXManager.PlayMediumHit();

                    Vector2 slamDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                    GameFeelManager.ScreenShake(0.15f, 0.06f);
                    GameFeelManager.HitStop(0.06f);
                    GameFeelParticles.HitBurst(brakeEnemy.transform.position, slamDir, Color.white, 8);
                    GameFeelParticles.ImpactFlash(brakeEnemy.transform.position, slamDir, Color.white, 1.6f);
                    return;
                }

                _gridPosition = newGridPos;
                _sprintStepCounter++;
                if (!SuppressStepSound && _sprintStepCounter % 2 == 0)
                    SFXManager.PlayStep();
            }

            // Skid particles
            var settings = GameFeelManager.Instance?.Settings;
            float skidInterval = settings != null ? settings.brakeSkidInterval : 0.03f;
            if (Time.time - _lastSkidTime >= skidInterval)
            {
                Vector2 dir2d = new Vector2(_sprintDirection.x, _sprintDirection.y);
                GameFeelParticles.SprintSkid(transform.position, dir2d);
                _lastSkidTime = Time.time;
            }

            // Check end conditions
            float overshootDist = Vector3.Distance(transform.position, _brakeStartPos) / CellSize;
            if (_sprintSpeed <= 0f || overshootDist >= SprintBrakeOvershoot)
            {
                SnapToNearestGrid();
                EndSprint();
            }
        }

        private void ApplySprintSquashStretch()
        {
            var feel = GameFeelManager.Instance?.Settings;
            float squash = feel != null ? feel.sprintSquashAmount : 0.8f;
            float stretch = feel != null ? feel.sprintStretchAmount : 1.2f;

            // Squash along movement axis, stretch perpendicular
            if (_sprintDirection.x != 0)
            {
                // Horizontal sprint: squash X, stretch Y
                transform.localScale = new Vector3(squash, stretch, 1f);
            }
            else
            {
                // Vertical sprint: squash Y, stretch X
                transform.localScale = new Vector3(stretch, squash, 1f);
            }
        }

        // === NORMAL MOVEMENT VISUAL ===

        private void UpdateVisualPosition()
        {
            if (!IsMoving) return;

            // Respect hitstop
            if (GameFeelManager.Instance != null && GameFeelManager.Instance.IsHitStopped) return;

            Vector3 target = _targetWorldPosition + _visualOffset;

            // On first frame of movement, record start position
            if (_moveProgress == 0f)
            {
                _moveStartPosition = transform.position;
            }

            _moveProgress += MoveSpeed * Time.deltaTime;

            if (_moveProgress >= 1f)
            {
                transform.position = target;
                IsMoving = false;
                _moveProgress = 0f;
                return;
            }

            // Ease-out with overshoot for snappy movement
            float t = EaseOutBack(_moveProgress);
            transform.position = Vector3.LerpUnclamped(_moveStartPosition, target, t);
        }

        private static float EaseOutBack(float t)
        {
            float overshoot = 1.5f;
            if (GameFeelManager.Instance != null && GameFeelManager.Instance.Settings != null)
            {
                overshoot = GameFeelManager.Instance.Settings.moveOvershootDistance * 18f;
            }
            float c = overshoot + 1f;
            return 1f + c * Mathf.Pow(t - 1f, 3) + overshoot * Mathf.Pow(t - 1f, 2);
        }

        public Vector3 GetWorldPosition()
        {
            return _gridPosition.ToWorldPosition(CellSize);
        }

        public int DistanceTo(GridCharacter other)
        {
            return _gridPosition.ManhattanDistanceTo(other.GridPosition);
        }

        public void SetVisualOffset(Vector3 offset)
        {
            _visualOffset = offset;
        }

        public void SetVisualScale(float scaleX, float scaleY)
        {
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }
}
