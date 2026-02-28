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
            float cellSize = SettingsManager.Instance?.settings?.cellSize ?? 1f;

            // Get camera bounds in world space
            float camHeight = _camera.orthographicSize;
            float camWidth = camHeight * _camera.aspect;
            Vector3 camPos = _camera.transform.position;

            // Shrink bounds by 1 cell so enemies don't spawn at screen edges
            float minX = camPos.x - camWidth + cellSize;
            float maxX = camPos.x + camWidth - cellSize;
            float minY = camPos.y - camHeight + cellSize;
            float maxY = camPos.y + camHeight - cellSize;

            // Pick a random grid-aligned position
            int gridMinX = Mathf.CeilToInt(minX / cellSize);
            int gridMaxX = Mathf.FloorToInt(maxX / cellSize);
            int gridMinY = Mathf.CeilToInt(minY / cellSize);
            int gridMaxY = Mathf.FloorToInt(maxY / cellSize);

            int x = Random.Range(gridMinX, gridMaxX + 1);
            int y = Random.Range(gridMinY, gridMaxY + 1);

            return new GridPosition(x, y);
        }
    }
}
