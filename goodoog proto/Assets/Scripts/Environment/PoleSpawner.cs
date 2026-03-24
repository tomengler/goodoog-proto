using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Environment;
using DogAndRobot.Enemies;

namespace DogAndRobot.Environment
{
    public class PoleSpawner : MonoBehaviour
    {
        [SerializeField] GameObject _polePrefab;
        [SerializeField] int _poleCount = 3;
        [SerializeField] int _maxAttempts = 100;
        [SerializeField] Transform[] _characterStartTransforms; // assign Robot & Dog transforms

        void Start()
        {
            SpawnPoles();
        }

        void SpawnPoles()
        {
            WallManager.Instance.GetInteriorBounds(out int minX, out int maxX, out int minY, out int maxY);
            int spawned = 0;
            int attempts = 0;

            while (spawned < _poleCount && attempts < _maxAttempts)
            {
                attempts++;
                int x = Random.Range(minX, maxX + 1);
                int y = Random.Range(minY, maxY + 1);
                var pos = new GridPosition(x, y);

                if (!IsValidPolePosition(pos))
                    continue;

                float cellSize = SettingsManager.Instance.settings.cellSize;
                Instantiate(_polePrefab, pos.ToWorldPosition(cellSize), Quaternion.identity);
                spawned++;
            }

            if (spawned < _poleCount)
                Debug.LogWarning($"PoleSpawner: Only spawned {spawned}/{_poleCount} poles after {_maxAttempts} attempts");
        }

        bool IsValidPolePosition(GridPosition pos)
        {
            if (WallManager.Instance.IsWall(pos)) return false;
            if (Enemy.FindAtPosition(pos) != null) return false;
            if (Pole.FindAtPosition(pos) != null) return false;

            // Check character start positions
            float cellSize = SettingsManager.Instance.settings.cellSize;
            foreach (var t in _characterStartTransforms)
            {
                if (t != null && GridPosition.FromWorldPosition(t.position, cellSize).Equals(pos))
                    return false;
            }

            // Check all 8 neighbors (cardinal + diagonal)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var neighbor = new GridPosition(pos.x + dx, pos.y + dy);
                    if (WallManager.Instance.IsWall(neighbor)) return false;
                    if (Pole.FindAtPosition(neighbor) != null) return false;
                    if (Enemy.FindAtPosition(neighbor) != null) return false;
                }
            }

            return true;
        }
    }
} // namespace DogAndRobot.Environment
