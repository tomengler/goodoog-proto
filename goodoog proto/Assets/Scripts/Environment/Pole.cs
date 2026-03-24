// Pole.cs
// A stationary grid object that characters can grab onto.
// Uses same static registry pattern as Enemy.cs.

using UnityEngine;
using System.Collections.Generic;
using DogAndRobot.Core;

namespace DogAndRobot.Environment
{
    public class Pole : MonoBehaviour
    {
        // === STATIC REGISTRY ===
        private static readonly List<Pole> _allPoles = new List<Pole>();
        public static IReadOnlyList<Pole> All => _allPoles;

        public static Pole FindAtPosition(GridPosition pos)
        {
            for (int i = 0; i < _allPoles.Count; i++)
            {
                if (_allPoles[i]._gridPosition.Equals(pos))
                    return _allPoles[i];
            }
            return null;
        }

        [SerializeField] private GridPosition _gridPosition;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private static readonly Color DefaultColor = new Color(0.3f, 0.3f, 0.3f, 1f); // dark gray

        public GridPosition GridPosition => _gridPosition;

        void Awake()
        {
            _allPoles.Add(this);
            float cellSize = SettingsManager.Instance.settings.cellSize;
            _gridPosition = GridPosition.FromWorldPosition(transform.position, cellSize);
            transform.position = _gridPosition.ToWorldPosition(cellSize);

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
                _spriteRenderer.color = DefaultColor;
        }

        void OnDestroy()
        {
            _allPoles.Remove(this);
        }

        public void SetHolderColor(Color color)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = color;
        }

        public void ResetColor()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = DefaultColor;
        }
    }
} // namespace DogAndRobot.Environment
