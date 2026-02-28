// GridCharacter.cs
// Base class for any character that moves on the grid.
// Both Robot and Dog will inherit from this, sharing this core functionality.

using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Enemies;

namespace DogAndRobot.Characters
{
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

        // Visual offset from grid position (used when joined)
private Vector3 _visualOffset = Vector3.zero;

// Visual scale multiplier (used when joined)
private float _visualScale = 1f;
        
        // === SETTINGS ACCESS ===
        // Properties that read from central settings, with fallback defaults
        private float CellSize => SettingsManager.Instance?.settings?.cellSize ?? 1f;
        private float MoveSpeed => SettingsManager.Instance?.settings?.moveSpeed ?? 10f;
        private float ArrivalThreshold => SettingsManager.Instance?.settings?.arrivalThreshold ?? 0.01f;
        
        // === UNITY LIFECYCLE METHODS ===
        
        protected virtual void Awake()
        {
            _gridPosition = GridPosition.FromWorldPosition(transform.position, CellSize);
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition;
        }
        
        protected virtual void Update()
        {
            UpdateVisualPosition();
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
        // Try to attack the enemy
        DamageType damageType = GetDamageType();
        bool hit = enemy.TryTakeHit(damageType, direction);

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
    
    // Check if the move is valid (we'll expand this later with collision)
    if (CanMoveTo(newPosition))
    {
        // Update our logical grid position immediately
        _gridPosition = newPosition;
        
        // Set the target for smooth visual movement
        _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
        
        // Flag that we're in motion (useful for animations, preventing input, etc.)
        IsMoving = true;
        
        // Let the caller know the move succeeded
        return true;
    }
    
    return false;
}

/// <summary>
/// Finds an enemy at the given grid position, if any.
/// </summary>
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
/// Returns the damage type this character deals.
/// Override in child classes.
/// </summary>
public virtual DamageType GetDamageType()
{
    return DamageType.Robot; // Default
}
        
        public void TeleportTo(GridPosition position)
        {
            _gridPosition = position;
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition + _visualOffset;
            IsMoving = false;
        }
        
        protected virtual bool CanMoveTo(GridPosition position)
        {
            return true;
        }
        
        private void UpdateVisualPosition()
        {
            if (!IsMoving) return;

            Vector3 target = _targetWorldPosition + _visualOffset;

            transform.position = Vector3.Lerp(
                transform.position,
                target,
                MoveSpeed * Time.deltaTime
            );

            float distanceToTarget = Vector3.Distance(transform.position, target);
            if (distanceToTarget < ArrivalThreshold)
            {
                transform.position = target;
                IsMoving = false;
            }
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

/// <summary>
/// Sets a visual scale multiplier.
/// Used to shrink characters when joined.
/// scaleX and scaleY can be set independently.
/// </summary>
public void SetVisualScale(float scaleX, float scaleY)
{
    transform.localScale = new Vector3(scaleX, scaleY, 1f);
}

    }
}