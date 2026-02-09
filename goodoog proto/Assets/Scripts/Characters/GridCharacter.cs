// GridCharacter.cs
// Base class for any character that moves on the grid.
// Both Robot and Dog will inherit from this, sharing this core functionality.

using UnityEngine;
using DogAndRobot.Core;

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
        
        public bool TryMove(GridPosition direction)
        {
            GridPosition newPosition = _gridPosition + direction;
            
            if (CanMoveTo(newPosition))
            {
                _gridPosition = newPosition;
                _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
                IsMoving = true;
                return true;
            }
            
            return false;
        }
        
        public void TeleportTo(GridPosition position)
        {
            _gridPosition = position;
            _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
            transform.position = _targetWorldPosition;
            IsMoving = false;
        }
        
        protected virtual bool CanMoveTo(GridPosition position)
        {
            return true;
        }
        
        private void UpdateVisualPosition()
        {
            if (!IsMoving) return;
            
            transform.position = Vector3.Lerp(
                transform.position,
                _targetWorldPosition,
                MoveSpeed * Time.deltaTime
            );
            
            float distanceToTarget = Vector3.Distance(transform.position, _targetWorldPosition);
            if (distanceToTarget < ArrivalThreshold)
            {
                transform.position = _targetWorldPosition;
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