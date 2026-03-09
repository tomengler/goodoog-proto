// Enemy.cs
// Base class for all enemies.
// Handles health as a sequence of damage types, taking hits, and knockback.

using UnityEngine;
using System.Collections.Generic;
using DogAndRobot.Core;
using DogAndRobot.Characters;

namespace DogAndRobot.Enemies
{
    public class Enemy : MonoBehaviour
    {
        // === STATIC REGISTRY ===
        private static readonly List<Enemy> _allEnemies = new List<Enemy>();
        public static IReadOnlyList<Enemy> All => _allEnemies;

        public static Enemy FindAtPosition(GridPosition position)
        {
            for (int i = 0; i < _allEnemies.Count; i++)
            {
                if (_allEnemies[i]._gridPosition == position)
                    return _allEnemies[i];
            }
            return null;
        }

        [Header("Grid Settings")]
        [SerializeField] private GridPosition _gridPosition;

        public GridPosition GridPosition => _gridPosition;
        
        // The sequence of damage types needed to kill this enemy
        // First element = next hit needed
        protected List<DamageType> _damageSequence = new List<DamageType>();
        
        // Visual references (we'll set these up in Unity)
        [Header("Health Display")]
        [SerializeField] private Transform _healthBarContainer;
        [SerializeField] private GameObject _healthSegmentPrefab;
        
        // Segment visuals
        private List<GameObject> _healthSegments = new List<GameObject>();
        private GameObject _launchSegment;

        // Colors for damage types — matched to character sprite colors
        [Header("Damage Type Colors")]
        public Color dogDamageColor = new Color(1f, 0.831f, 0.631f); // #FFD4A1
        public Color robotDamageColor = new Color(0.631f, 0.655f, 1f); // #A1A7FF
        public Color launchSegmentColor = new Color(0.7f, 0.2f, 1f); // Purple

        [Header("Enemy Body Colors")]
        public Color dogBodyColor = new Color(1f, 0.831f, 0.631f); // #FFD4A1
        public Color robotBodyColor = new Color(0.631f, 0.655f, 1f); // #A1A7FF

        // Inner body visual (colored square inside black outer square)
        private SpriteRenderer _innerBodySr;

        // Hit string UI visibility
        private static bool _showHitStrings = true;

        // Fired when this enemy dies
        public static event System.Action OnEnemyDefeated;

        // Vulnerable state
        private bool _isVulnerable;
        private float _vulnerableTimer;
        private Coroutine _flashCoroutine;
        private float[] _flashIntervalRef = new float[1];
        // Store original damage sequence for recovery
        private List<DamageType> _originalDamageSequence = new List<DamageType>();

        public bool IsVulnerable => _isVulnerable;

        // Launch state
        private bool _isLaunched;
        private Vector2 _launchDirection;
        private float _launchSpeed;
        private float _launchAcceleration = 40f;

        // Settings access
        private float CellSize => SettingsManager.Instance?.settings?.cellSize ?? 1f;
        
        protected virtual void Awake()
        {
            _allEnemies.Add(this);
            // Initialize grid position from world position
            _gridPosition = GridPosition.FromWorldPosition(transform.position, CellSize);
            transform.position = _gridPosition.ToWorldPosition(CellSize);
        }

        protected virtual void OnDestroy()
        {
            _allEnemies.Remove(this);
        }
        
        protected virtual void Start()
        {
            SetupInnerBody();
            GenerateDamageSequence();
            // Store original sequence for recovery
            _originalDamageSequence.AddRange(_damageSequence);
            CreateHealthBar();
            UpdateBodyColor();
        }

        private void SetupInnerBody()
        {
            // Make outer square black
            SpriteRenderer outerSr = GetComponent<SpriteRenderer>();
            if (outerSr == null) return;
            outerSr.color = Color.black;

            // Create inner colored square at 50% size
            GameObject inner = new GameObject("InnerBody");
            inner.transform.SetParent(transform);
            inner.transform.localPosition = Vector3.zero;
            inner.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            _innerBodySr = inner.AddComponent<SpriteRenderer>();
            _innerBodySr.sprite = outerSr.sprite;
            _innerBodySr.sortingLayerName = outerSr.sortingLayerName;
            _innerBodySr.sortingOrder = outerSr.sortingOrder + 1;
        }
        
        private void UpdateBodyColor()
        {
            if (_innerBodySr == null) return;

            if (_damageSequence.Count == 0)
                return; // Vulnerable state handles its own color

            _innerBodySr.color = _damageSequence[0] == DamageType.Dog ? dogBodyColor : robotBodyColor;
        }

        /// <summary>
        /// Override this in child classes to create different sequences.
        /// </summary>
        protected virtual void GenerateDamageSequence()
        {
            // Default: 5 random hits
            for (int i = 0; i < 5; i++)
            {
                DamageType type = Random.value > 0.5f ? DamageType.Dog : DamageType.Robot;
                _damageSequence.Add(type);
            }
        }
        
        /// <summary>
        /// Creates the segmented health bar display.
        /// </summary>
        private void CreateHealthBar()
        {
            if (_healthBarContainer == null || _healthSegmentPrefab == null)
            {
                Debug.LogWarning("Enemy: Health bar container or prefab not set!");
                return;
            }
            
            // Respect hit string toggle
            _healthBarContainer.gameObject.SetActive(_showHitStrings);

            // Clear any existing segments
            foreach (var segment in _healthSegments)
            {
                Destroy(segment);
            }
            _healthSegments.Clear();

            if (_launchSegment != null)
            {
                Destroy(_launchSegment);
                _launchSegment = null;
            }
            
            // Create a segment for each hit in the sequence, plus one purple launch segment
            float segmentWidth = 0.18f;
            int totalCount = _damageSequence.Count + 1; // +1 for launch segment
            float totalWidth = segmentWidth * totalCount;
            float startX = -totalWidth / 2f + segmentWidth / 2f;

            for (int i = 0; i < _damageSequence.Count; i++)
            {
                GameObject segment = Instantiate(_healthSegmentPrefab, _healthBarContainer);
                segment.transform.localPosition = new Vector3(startX + i * segmentWidth, 0, 0);

                // Color based on damage type
                SpriteRenderer sr = segment.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = _damageSequence[i] == DamageType.Dog ? dogDamageColor : robotDamageColor;
                }

                _healthSegments.Add(segment);
            }

            // Purple launch segment at the end
            _launchSegment = Instantiate(_healthSegmentPrefab, _healthBarContainer);
            _launchSegment.transform.localPosition = new Vector3(startX + _damageSequence.Count * segmentWidth, 0, 0);
            SpriteRenderer launchSr = _launchSegment.GetComponent<SpriteRenderer>();
            if (launchSr != null) launchSr.color = launchSegmentColor;
        }
        
        /// <summary>
        /// Returns the next damage type needed without consuming it, or null if sequence is empty.
        /// </summary>
        public DamageType? PeekNextDamageType()
        {
            if (_damageSequence.Count == 0) return null;
            return _damageSequence[0];
        }

        /// <summary>
        /// Called when a character tries to attack this enemy.
        /// Returns true if the attack was successful (correct damage type).
        /// </summary>
        public bool TryTakeHit(DamageType damageType, GridPosition attackDirection, Transform attacker = null, bool suppressAudio = false)
        {
            if (_damageSequence.Count == 0)
                return false;

            // Check if the damage type matches what's needed
            if (_damageSequence[0] != damageType)
            {
                Debug.Log($"Wrong damage type! Needed {_damageSequence[0]}, got {damageType}");
                OnWrongHit(attackDirection, attacker);
                return false;
            }

            // Correct hit!
            Debug.Log($"Hit! {_damageSequence.Count - 1} hits remaining");

            if (!suppressAudio)
                SFXManager.PlayLightHit();

            // Remove the first damage type from sequence
            _damageSequence.RemoveAt(0);

            // Update body color to reflect new required damage type
            UpdateBodyColor();

            // Juice: direction as Vector2 for effects
            Vector2 attackDir = new Vector2(attackDirection.x, attackDirection.y);

            // Juice: hitstop
            GameFeelManager.HitStop();

            // Juice: attacker squash
            if (attacker != null)
                GameFeelManager.Squash(attacker, attackDir);

            // Juice: enemy stretch in knockback direction
            GameFeelManager.Stretch(transform, attackDir);

            // Juice: flash white (inner body)
            if (_innerBodySr != null)
                GameFeelManager.Flash(_innerBodySr);

            // Juice: screen shake
            GameFeelManager.ScreenShake();

            // Layer C: hit particles
            Color particleColor = damageType == DamageType.Dog ? dogDamageColor : robotDamageColor;
            GameFeelParticles.HitBurst(transform.position, attackDir, particleColor);

            // Arcady impact flash (DBFZ-style cross + ring + speed lines)
            GameFeelParticles.ImpactFlash(transform.position, attackDir, particleColor, 1.3f);

            // Layer C: chromatic aberration
            GameFeelManager.ChromaticPulse(0.3f, 0.08f);

            // Remove the first health segment with pop effect
            if (_healthSegments.Count > 0)
            {
                GameFeelManager.SegmentPop(_healthSegments[0]);
                _healthSegments.RemoveAt(0);
            }

            // Try to knock back
            TryKnockback(attackDirection);

            // Check if sequence depleted — enter vulnerable instead of immediate launch
            if (_damageSequence.Count == 0)
            {
                EnterVulnerable();
            }

            return true;
        }
        
        /// <summary>
        /// Attempts to knock the enemy back one grid space.
        /// </summary>
        protected virtual void TryKnockback(GridPosition direction)
        {
            GridPosition newPosition = _gridPosition + direction;

            // Block knockback into walls
            if (WallManager.Instance != null && WallManager.Instance.IsWall(newPosition))
                return;

            _gridPosition = newPosition;
            transform.position = _gridPosition.ToWorldPosition(CellSize);
        }
        
        /// <summary>
        /// Called when hit with wrong damage type.
        /// </summary>
        protected virtual void OnWrongHit(GridPosition attackDirection, Transform attacker)
        {
            Vector2 attackDir = new Vector2(attackDirection.x, attackDirection.y);

            SFXManager.PlayBlock();

            // Enemy shakes in place
            GameFeelManager.ObjectShake(transform, attackDir);

            // Health bar wobbles
            if (_healthBarContainer != null)
            {
                var settings = GameFeelManager.Instance?.Settings;
                GameFeelManager.ObjectShake(_healthBarContainer, Vector2.right,
                    settings?.healthBarWobbleDuration ?? 0.15f,
                    settings?.healthBarWobbleIntensity ?? 0.05f);
            }

            // Attacker recoils
            if (attacker != null)
                GameFeelManager.Recoil(attacker, attackDir);

            // Layer C: block sparks
            GameFeelParticles.BlockSparks(transform.position);
        }
        
        private void Update()
        {
            // Toggle hit string UI with Q
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                _showHitStrings = !_showHitStrings;
                foreach (var enemy in _allEnemies)
                {
                    if (enemy._healthBarContainer != null)
                        enemy._healthBarContainer.gameObject.SetActive(_showHitStrings && !enemy._isLaunched);
                }
            }

            // Vulnerable countdown
            if (_isVulnerable)
            {
                _vulnerableTimer -= Time.deltaTime;

                // Speed up flash in last 2 seconds
                var feelSettings = GameFeelManager.Instance?.Settings;
                float normalInterval = feelSettings?.vulnerableFlashInterval ?? 0.3f;
                float urgentInterval = feelSettings?.vulnerableUrgentFlashInterval ?? 0.1f;
                _flashIntervalRef[0] = _vulnerableTimer <= 2f ? urgentInterval : normalInterval;

                if (_vulnerableTimer <= 0f)
                {
                    RecoverFromVulnerable();
                    return;
                }
            }

            if (!_isLaunched) return;

            // Accelerate and move continuously in world space
            _launchSpeed += _launchAcceleration * Time.deltaTime;
            transform.position += (Vector3)_launchDirection * _launchSpeed * Time.deltaTime;

            // Check if current position overlaps a wall
            GridPosition currentGrid = GridPosition.FromWorldPosition(transform.position, CellSize);
            if (WallManager.Instance != null && WallManager.Instance.IsWall(currentGrid))
            {
                ExplodeOnWall();
            }
        }

        // === VULNERABLE STATE ===

        private void EnterVulnerable()
        {
            var settings = SettingsManager.Instance?.settings;
            _isVulnerable = true;
            _vulnerableTimer = settings?.vulnerableDuration ?? 5f;

            SFXManager.PlayGuardBreak();

            // Juice
            GameFeelManager.HitStop();
            GameFeelManager.ScreenShake();
            GameFeelManager.ChromaticPulse(0.3f, 0.08f);

            // Particles
            GameFeelParticles.VulnerableBurst(transform.position);

            // Start repeating flash — set base color to light grey so it pulses grey → white
            var feelSettings = GameFeelManager.Instance?.Settings;
            _flashIntervalRef[0] = feelSettings?.vulnerableFlashInterval ?? 0.3f;
            if (_innerBodySr != null)
            {
                _innerBodySr.color = new Color(0.75f, 0.75f, 0.75f);
                _flashCoroutine = GameFeelManager.PulseFlash(_innerBodySr, _flashIntervalRef, settings?.vulnerableDuration ?? 5f);
            }

            // Hide regular health segments but keep launch segment visible
            foreach (var seg in _healthSegments)
                if (seg != null) seg.SetActive(false);
        }

        /// <summary>
        /// Push the vulnerable enemy one tile. Any character can push, no damage type check.
        /// </summary>
        public bool TryPush(GridPosition direction)
        {
            if (!_isVulnerable) return false;

            TryKnockback(direction);

            // Small juice
            SFXManager.PlayLightHit();
            GameFeelManager.HitStop(0.03f);
            if (_innerBodySr != null)
                GameFeelManager.Flash(_innerBodySr);
            Vector2 dir2d = new Vector2(direction.x, direction.y);
            GameFeelParticles.HitBurst(transform.position, dir2d, Color.white, 3);

            return true;
        }

        /// <summary>
        /// Launch the vulnerable enemy (from charged or joint attack).
        /// chargeStrength 0-1 scales launch speed (1 = full power, joint attacks always use 1).
        /// </summary>
        public bool TryLaunch(GridPosition direction, float chargeStrength = 1f)
        {
            if (!_isVulnerable) return false;

            // Stop flash
            if (_flashCoroutine != null && GameFeelManager.Instance != null)
            {
                GameFeelManager.Instance.StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            // Restore inner body color to white for launch
            if (_innerBodySr != null)
                _innerBodySr.color = Color.white;

            _isVulnerable = false;

            // Pop the launch segment
            if (_launchSegment != null)
            {
                GameFeelManager.SegmentPop(_launchSegment);
                _launchSegment = null;
            }

            SFXManager.PlayHeavyHit();

            // Bigger juice for launch — scale with charge strength
            GameFeelManager.HitStop(0.05f + chargeStrength * 0.05f);
            GameFeelManager.ScreenShake(0.1f + chargeStrength * 0.1f, 0.03f + chargeStrength * 0.05f);
            GameFeelManager.ChromaticPulse(0.3f + chargeStrength * 0.4f, 0.08f + chargeStrength * 0.06f);
            GameFeelManager.TimeScalePunch(0.5f, 0.08f + chargeStrength * 0.06f);

            Vector2 dir2d = new Vector2(direction.x, direction.y);
            int particleCount = Mathf.RoundToInt(Mathf.Lerp(5f, 14f, chargeStrength));
            GameFeelParticles.LaunchChargeBurst(transform.position, dir2d, particleCount);
            GameFeelParticles.ImpactFlash(transform.position, dir2d, Color.white, 2f);

            // Reuse existing launch logic with scaled speed
            StartLaunch(direction, chargeStrength);
            return true;
        }

        private void RecoverFromVulnerable()
        {
            // Stop flash
            if (_flashCoroutine != null && GameFeelManager.Instance != null)
            {
                GameFeelManager.Instance.StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            // Restore inner body color (UpdateBodyColor will set the correct type color)
            _isVulnerable = false;

            // Regenerate health segments from original sequence
            var settings = SettingsManager.Instance?.settings;
            int recoveryCount = Mathf.RoundToInt(settings?.vulnerableRecoveryHealth ?? 1);
            recoveryCount = Mathf.Min(recoveryCount, _originalDamageSequence.Count);

            _damageSequence.Clear();
            for (int i = 0; i < recoveryCount; i++)
            {
                // Take from the end of the original sequence (last hits)
                _damageSequence.Add(_originalDamageSequence[_originalDamageSequence.Count - recoveryCount + i]);
            }

            // Rebuild health bar (CreateHealthBar destroys old segments and recreates all including launch segment)
            CreateHealthBar();
            UpdateBodyColor();

            // Small recovery effect
            GameFeelParticles.BlockSparks(transform.position);
        }

        private void StartLaunch(GridPosition direction, float chargeStrength = 1f)
        {
            _isLaunched = true;
            _launchDirection = new Vector2(direction.x, direction.y).normalized;

            var settings = GameFeelManager.Instance?.Settings;
            float baseSpeed = settings?.launchSpeed ?? 20f;
            // Scale launch speed: minimum 30% at low charge, 100% at full
            _launchSpeed = baseSpeed * Mathf.Lerp(0.3f, 1f, chargeStrength);

            // Hide health bar
            if (_healthBarContainer != null)
                _healthBarContainer.gameObject.SetActive(false);

            // Stretch in launch direction
            GameFeelManager.Stretch(transform, _launchDirection);

            // Screen shake
            GameFeelManager.ScreenShake();
        }

        private int DistanceToWall(GridPosition direction)
        {
            GridPosition pos = _gridPosition;
            int dist = 0;
            while (true)
            {
                pos = pos + direction;
                dist++;
                if (WallManager.Instance != null && WallManager.Instance.IsWall(pos))
                    return dist;
                if (dist > 50) break; // safety cap
            }
            return dist;
        }

        private void ExplodeOnWall()
        {
            _isLaunched = false;

            SFXManager.PlayWallExplosion();

            OnEnemyDefeated?.Invoke();

            var settings = GameFeelManager.Instance?.Settings;

            // Big screen shake
            GameFeelManager.ScreenShake(
                (settings?.deathShakeDuration ?? 0.15f) * 1.5f,
                (settings?.deathShakeIntensity ?? 0.05f) * 2f);

            // Chromatic pulse
            GameFeelManager.ChromaticPulse(0.8f, 0.15f);

            // Time-scale punch
            GameFeelManager.TimeScalePunch(0.5f, 0.15f);

            // Wall explosion particles
            GameFeelParticles.WallExplosion(transform.position, _launchDirection);
            GameFeelParticles.ImpactFlash(transform.position, -_launchDirection, Color.white, 2.5f);

            // Death burst
            GameFeelParticles.DeathBurst(transform.position);

            // Scatter remaining health segments
            if (_healthBarContainer != null)
            {
                _healthBarContainer.gameObject.SetActive(true);
                GameFeelManager.HealthBarScatter(_healthBarContainer);
            }

            Destroy(gameObject, settings?.deathScaleDuration ?? 0.15f);
        }

        /// <summary>
        /// Called when the enemy's health reaches zero.
        /// </summary>
        protected virtual void Die()
        {
            Debug.Log($"{gameObject.name} defeated!");
            OnEnemyDefeated?.Invoke();

            // Juice: death scale
            GameFeelManager.DeathScale(transform);

            // Juice: stronger screen shake
            var settings = GameFeelManager.Instance?.Settings;
            GameFeelManager.ScreenShake(
                settings?.deathShakeDuration ?? 0.15f,
                settings?.deathShakeIntensity ?? 0.05f);

            // Juice: scatter remaining health segments
            if (_healthBarContainer != null)
                GameFeelManager.HealthBarScatter(_healthBarContainer);

            // Layer C: death burst
            GameFeelParticles.DeathBurst(transform.position);

            // Layer C: stronger chromatic aberration
            GameFeelManager.ChromaticPulse(0.6f, 0.12f);

            // Layer C: time-scale punch
            GameFeelManager.TimeScalePunch(0.7f, 0.1f);

            // Delayed destroy to let effects play
            Destroy(gameObject, settings?.deathScaleDuration ?? 0.15f);
        }
        
        /// <summary>
        /// Teleport to a grid position.
        /// </summary>
        public void TeleportTo(GridPosition position)
        {
            _gridPosition = position;
            transform.position = _gridPosition.ToWorldPosition(CellSize);
        }
    }
}