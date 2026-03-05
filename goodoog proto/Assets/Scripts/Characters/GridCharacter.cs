// GridCharacter.cs
// Base class for any character that moves on the grid.
// Both Robot and Dog will inherit from this, sharing this core functionality.
// Supports normal grid movement and continuous sprint movement.

using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Enemies;

namespace DogAndRobot.Characters
{
    public enum MoveState { Normal, Sprinting, Braking }

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



        // === SPRINT STATE ===
        public MoveState SprintState => _sprintState;
        private MoveState _sprintState = MoveState.Normal;
        private GridPosition _sprintDirection;
        private float _sprintSpeed;
        private float _sprintElapsed;
        private Vector3 _brakeStartPos;
        private float _lastSkidTime;
        // Store the scale before sprint started so we can restore it
        private Vector3 _preSprintScale;

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
        public bool TryMove(GridPosition direction)
        {
            // Calculate where we'd end up
            GridPosition newPosition = _gridPosition + direction;

            // Check if there's an enemy at the target position
            Enemy enemy = FindEnemyAtPosition(newPosition);
            if (enemy != null)
            {
                // If enemy is vulnerable, push it instead of attacking
                if (enemy.IsVulnerable)
                {
                    if (enemy.TryPush(direction))
                    {
                        // Move into the space the enemy was pushed out of
                        _gridPosition = newPosition;
                        _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                        IsMoving = true;
                        return true;
                    }
                    return false;
                }

                // Try to attack the enemy
                DamageType damageType = GetDamageType();
                bool hit = enemy.TryTakeHit(damageType, direction, transform);

                if (hit)
                {
                    // Follow up: move into the space the enemy was knocked out of
                    _gridPosition = newPosition;
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    IsMoving = true;
                    return true;
                }

                return false;
            }

            // Check if the move is valid
            if (CanMoveTo(newPosition))
            {
                _gridPosition = newPosition;
                _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                IsMoving = true;
                return true;
            }

            return false;
        }

        private Enemy FindEnemyAtPosition(GridPosition position)
        {
            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (Enemy enemy in enemies)
            {
                if (enemy.GridPosition == position)
                {
                    return enemy;
                }
            }
            return null;
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

        protected virtual bool CanMoveTo(GridPosition position)
        {
            if (WallManager.Instance != null && WallManager.Instance.IsWall(position))
                return false;
            return true;
        }

        // === SPRINT METHODS ===

        /// <summary>
        /// Start sprinting in the given direction.
        /// </summary>
        public void StartSprint(GridPosition direction)
        {
            _sprintState = MoveState.Sprinting;
            _sprintDirection = direction;
            _sprintSpeed = SprintBaseSpeed;
            _sprintElapsed = 0f;
            _preSprintScale = transform.localScale;
            IsMoving = true;

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
                // Check wall at the new cell
                if (!CanMoveTo(newGridPos))
                {
                    // Hit a wall — snap back to last valid cell and end sprint
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    transform.position = _targetWorldPosition + _visualOffset;

                    Vector2 impactDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                    GameFeelParticles.WallExplosion(transform.position + (Vector3)(impactDir * CellSize * 0.5f), impactDir);

                    GameFeelManager.ScreenShake(0.15f, 0.06f);

                    EndSprint();
                    return;
                }
                _gridPosition = newGridPos;
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
                if (!CanMoveTo(newGridPos))
                {
                    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                    transform.position = _targetWorldPosition + _visualOffset;

                    Vector2 impactDir = new Vector2(_sprintDirection.x, _sprintDirection.y);
                    GameFeelParticles.WallExplosion(transform.position + (Vector3)(impactDir * CellSize * 0.5f), impactDir);

                    EndSprint();
                    return;
                }
                _gridPosition = newGridPos;
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
