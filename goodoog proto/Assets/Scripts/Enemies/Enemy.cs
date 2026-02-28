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
        public bool TryTakeHit(DamageType damageType, GridPosition attackDirection)
        {
            if (_damageSequence.Count == 0)
                return false;
            
            // Check if the damage type matches what's needed
            if (_damageSequence[0] != damageType)
            {
                Debug.Log($"Wrong damage type! Needed {_damageSequence[0]}, got {damageType}");
                OnWrongHit();
                return false;
            }
            
            // Correct hit!
            Debug.Log($"Hit! {_damageSequence.Count - 1} hits remaining");
            
            // Remove the first damage type from sequence
            _damageSequence.RemoveAt(0);
            
            // Remove the first health segment
            if (_healthSegments.Count > 0)
            {
                Destroy(_healthSegments[0]);
                _healthSegments.RemoveAt(0);
            }
            
            // Try to knock back
            TryKnockback(attackDirection);
            
            // Check if dead
            if (_damageSequence.Count == 0)
            {
                Die();
            }
            
            return true;
        }
        
        /// <summary>
        /// Attempts to knock the enemy back one grid space.
        /// </summary>
        protected virtual void TryKnockback(GridPosition direction)
        {
            GridPosition newPosition = _gridPosition + direction;
            
            // TODO: Add collision checks here (walls, other enemies, etc.)
            
            _gridPosition = newPosition;
            
            // Smoothly move to new position (for now, just teleport)
            transform.position = _gridPosition.ToWorldPosition(CellSize);
        }
        
        /// <summary>
        /// Called when hit with wrong damage type.
        /// </summary>
        protected virtual void OnWrongHit()
        {
            // Override in child classes for different reactions
            // Could play a sound, flash, push the player back, etc.
        }
        
        /// <summary>
        /// Called when the enemy's health reaches zero.
        /// </summary>
        protected virtual void Die()
        {
            Debug.Log($"{gameObject.name} defeated!");
            Destroy(gameObject);
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