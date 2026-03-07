using UnityEngine;
using System.Collections.Generic;

namespace DogAndRobot.Core
{
    public class WallManager : MonoBehaviour
    {
        public static WallManager Instance { get; private set; }

        [Header("Wall Settings")]
        [SerializeField] private GameObject _wallTilePrefab;
        [SerializeField] private Color _wallColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        [SerializeField] private int _arenaWidth = 27;
        [SerializeField] private int _arenaHeight = 14;

        private HashSet<GridPosition> _wallPositions = new HashSet<GridPosition>();

        private float CellSize => SettingsManager.Instance?.settings?.cellSize ?? 1f;

        // Half-extents for centering the arena around origin
        private int HalfW => _arenaWidth / 2;
        private int HalfH => _arenaHeight / 2;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            BuildWalls();
        }

        private void BuildWalls()
        {
            int minX = -HalfW;
            int maxX = -HalfW + _arenaWidth - 1;
            int minY = -HalfH;
            int maxY = -HalfH + _arenaHeight - 1;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    bool isBorder = (x == minX || x == maxX || y == minY || y == maxY);
                    if (!isBorder) continue;

                    GridPosition pos = new GridPosition(x, y);
                    _wallPositions.Add(pos);

                    if (_wallTilePrefab != null)
                    {
                        GameObject tile = Instantiate(_wallTilePrefab, transform);
                        tile.transform.position = pos.ToWorldPosition(CellSize);
                        tile.transform.localScale = Vector3.one * CellSize;
                        tile.name = $"Wall_{x}_{y}";

                        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            sr.color = _wallColor;
                            sr.sortingOrder = -1;
                        }
                    }
                }
            }
        }

        public bool IsWall(GridPosition position)
        {
            return _wallPositions.Contains(position);
        }

        /// <summary>
        /// Returns the interior bounds for spawning (excludes walls).
        /// </summary>
        public void GetInteriorBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = -HalfW + 1;
            maxX = -HalfW + _arenaWidth - 2;
            minY = -HalfH + 1;
            maxY = -HalfH + _arenaHeight - 2;
        }
    }
}
