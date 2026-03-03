using UnityEngine;
using DogAndRobot.Enemies;

namespace DogAndRobot.Core
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _enemyPrefab;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void OnEnable()
        {
            Enemy.OnEnemyDefeated += SpawnNewEnemy;
        }

        private void OnDisable()
        {
            Enemy.OnEnemyDefeated -= SpawnNewEnemy;
        }

        private void SpawnNewEnemy()
        {
            GridPosition spawnPos = GetRandomScreenGridPosition();
            GameObject enemyObj = Instantiate(_enemyPrefab);
            enemyObj.name = "Enemy";

            Enemy enemy = enemyObj.GetComponent<Enemy>();
            enemy.TeleportTo(spawnPos);
        }

        private GridPosition GetRandomScreenGridPosition()
        {
            // Use wall interior bounds if available
            if (WallManager.Instance != null)
            {
                WallManager.Instance.GetInteriorBounds(out int minX, out int maxX, out int minY, out int maxY);

                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int x = Random.Range(minX, maxX + 1);
                    int y = Random.Range(minY, maxY + 1);
                    GridPosition pos = new GridPosition(x, y);
                    if (!WallManager.Instance.IsWall(pos))
                        return pos;
                }

                return new GridPosition(minX, minY);
            }

            // Fallback: camera-based spawning
            float cellSize = SettingsManager.Instance?.settings?.cellSize ?? 1f;

            float camHeight = _camera.orthographicSize;
            float camWidth = camHeight * _camera.aspect;
            Vector3 camPos = _camera.transform.position;

            float fMinX = camPos.x - camWidth + cellSize;
            float fMaxX = camPos.x + camWidth - cellSize;
            float fMinY = camPos.y - camHeight + cellSize;
            float fMaxY = camPos.y + camHeight - cellSize;

            int gridMinX = Mathf.CeilToInt(fMinX / cellSize);
            int gridMaxX = Mathf.FloorToInt(fMaxX / cellSize);
            int gridMinY = Mathf.CeilToInt(fMinY / cellSize);
            int gridMaxY = Mathf.FloorToInt(fMaxY / cellSize);

            return new GridPosition(
                Random.Range(gridMinX, gridMaxX + 1),
                Random.Range(gridMinY, gridMaxY + 1));
        }
    }
}
