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
        
        // Colors for damage types
        [Header("Damage Type Colors")]
        public Color dogDamageColor = new Color(1f, 0.5f, 0f); // Orange
        public Color robotDamageColor = new Color(0.3f, 0.5f, 1f); // Blue

        // Fired when this enemy dies
        public static event System.Action OnEnemyDefeated;

        // Launch state
        private bool _isLaunched;
        private Vector2 _launchDirection;
        private float _launchSpeed;
        private float _launchAcceleration = 40f;

        // Settings access
        private float CellSize => SettingsManager.Instance?.settings?.cellSize ?? 1f;
        
        protected virtual void Awake()
        {
            // Initialize grid position from world position
            _gridPosition = GridPosition.FromWorldPosition(transform.position, CellSize);
            transform.position = _gridPosition.ToWorldPosition(CellSize);
        }
        
        protected virtual void Start()
        {
            GenerateDamageSequence();
            CreateHealthBar();
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
            
            // Clear any existing segments
            foreach (var segment in _healthSegments)
            {
                Destroy(segment);
            }
            _healthSegments.Clear();
            
            // Create a segment for each hit in the sequence
            float segmentWidth = 0.18f;
            float totalWidth = segmentWidth * _damageSequence.Count;
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
        }
        
        /// <summary>
        /// Called when a character tries to attack this enemy.
        /// Returns true if the attack was successful (correct damage type).
        /// </summary>
        public bool TryTakeHit(DamageType damageType, GridPosition attackDirection, Transform attacker = null)
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

            // Remove the first damage type from sequence
            _damageSequence.RemoveAt(0);

            // Juice: direction as Vector2 for effects
            Vector2 attackDir = new Vector2(attackDirection.x, attackDirection.y);

            // Juice: hitstop
            GameFeelManager.HitStop();

            // Juice: attacker squash
            if (attacker != null)
                GameFeelManager.Squash(attacker, attackDir);

            // Juice: enemy stretch in knockback direction
            GameFeelManager.Stretch(transform, attackDir);

            // Juice: flash white
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                GameFeelManager.Flash(sr);

            // Juice: screen shake
            GameFeelManager.ScreenShake();

            // Layer C: hit particles
            Color particleColor = damageType == DamageType.Dog ? dogDamageColor : robotDamageColor;
            GameFeelParticles.HitBurst(transform.position, attackDir, particleColor);

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

            // Check if dead — launch instead of immediate death
            if (_damageSequence.Count == 0)
            {
                StartLaunch(attackDirection);
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

        private void StartLaunch(GridPosition direction)
        {
            _isLaunched = true;
            _launchDirection = new Vector2(direction.x, direction.y).normalized;

            var settings = GameFeelManager.Instance?.Settings;
            _launchSpeed = settings?.launchSpeed ?? 20f;

            // Hide health bar
            if (_healthBarContainer != null)
                _healthBarContainer.gameObject.SetActive(false);

            // Stretch in launch direction
            GameFeelManager.Stretch(transform, _launchDirection);

            // Screen shake
            GameFeelManager.ScreenShake();
        }

        private void ExplodeOnWall()
        {
            _isLaunched = false;

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