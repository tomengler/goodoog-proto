using UnityEngine;

namespace DogAndRobot.Core
{
    public class GameFeelParticles : MonoBehaviour
    {
        public static GameFeelParticles Instance { get; private set; }

        [SerializeField] private GameObject _squareParticlePrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static void HitBurst(Vector3 position, Vector2 direction, Color color, int count = 6)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.05f, 0.12f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = color;

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;

                float angle = Mathf.Atan2(direction.y, direction.x) + Random.Range(-0.5f, 0.5f);
                float force = Random.Range(3f, 6f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-360f, 360f);

                Destroy(p, 0.5f);
            }
        }

        public static void DeathBurst(Vector3 position, int count = 12)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.8f);

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float force = Random.Range(3f, 8f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-360f, 360f);

                Destroy(p, 0.6f);
            }
        }

        public static void BlockSparks(Vector3 position, int count = 3)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.03f, 0.06f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 0.9f, 0.5f);

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float force = Random.Range(2f, 4f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;

                Destroy(p, 0.3f);
            }
        }

        public static void WallExplosion(Vector3 position, Vector2 impactDirection, int count = 15)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;

            // Spray away from the wall (opposite of impact direction)
            Vector2 sprayDir = -impactDirection;
            float baseAngle = Mathf.Atan2(sprayDir.y, sprayDir.x);

            Color[] warmColors = new Color[]
            {
                new Color(1f, 0.4f, 0.1f),  // Orange
                new Color(1f, 0.7f, 0.2f),  // Yellow-orange
                new Color(1f, 0.2f, 0.1f),  // Red-orange
                new Color(1f, 0.85f, 0.4f), // Warm yellow
            };

            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = warmColors[Random.Range(0, warmColors.Length)];

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;

                float angle = baseAngle + Random.Range(-1.2f, 1.2f);
                float force = Random.Range(4f, 10f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-540f, 540f);

                Destroy(p, 0.7f);
            }
        }
        /// <summary>
        /// Small smoke particles spawned each frame during braking, creating a skid trail.
        /// </summary>
        public static void SprintSkid(Vector3 position, Vector2 direction, int count = 3)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;

            Color[] dustColors = new Color[]
            {
                new Color(0.6f, 0.55f, 0.5f, 0.6f),
                new Color(0.5f, 0.45f, 0.4f, 0.5f),
                new Color(0.7f, 0.65f, 0.6f, 0.5f),
            };

            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.04f, 0.08f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = dustColors[Random.Range(0, dustColors.Length)];

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0.5f;

                // Drift opposite to movement direction
                float baseAngle = Mathf.Atan2(-direction.y, -direction.x);
                float angle = baseAngle + Random.Range(-0.8f, 0.8f);
                float force = Random.Range(1f, 3f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;

                Destroy(p, 0.3f);
            }
        }

        /// <summary>
        /// Large burst of white/yellow particles when enemy enters vulnerable state.
        /// </summary>
        public static void VulnerableBurst(Vector3 position, int count = 9)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;

            Color[] colors = new Color[]
            {
                new Color(1f, 1f, 1f, 0.9f),
                new Color(1f, 1f, 0.7f, 0.9f),
                new Color(1f, 0.95f, 0.5f, 0.8f),
            };

            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = colors[Random.Range(0, colors.Length)];

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1.5f;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float force = Random.Range(4f, 8f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-360f, 360f);

                Destroy(p, 0.6f);
            }
        }

        /// <summary>
        /// Directional burst for charged/joint launch attacks. Bigger and faster than normal hit burst.
        /// </summary>
        public static void LaunchChargeBurst(Vector3 position, Vector2 direction, int count = 11)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;

            float baseAngle = Mathf.Atan2(direction.y, direction.x);

            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.1f, 0.18f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.9f);

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1.5f;

                float angle = baseAngle + Random.Range(-0.7f, 0.7f);
                float force = Random.Range(6f, 12f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-540f, 540f);

                Destroy(p, 0.5f);
            }
        }

        /// <summary>
        /// Burst of particles when sprint starts, shooting backward from the character.
        /// </summary>
        public static void SprintBurst(Vector3 position, Vector2 direction, int count = 5)
        {
            if (Instance == null || Instance._squareParticlePrefab == null) return;

            // Particles burst backward (opposite of sprint direction)
            float baseAngle = Mathf.Atan2(-direction.y, -direction.x);

            for (int i = 0; i < count; i++)
            {
                GameObject p = Instantiate(Instance._squareParticlePrefab, position, Quaternion.identity);
                p.transform.localScale = Vector3.one * Random.Range(0.06f, 0.1f);

                SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.7f);

                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb == null) rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1f;

                float angle = baseAngle + Random.Range(-0.6f, 0.6f);
                float force = Random.Range(4f, 7f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;
                rb.angularVelocity = Random.Range(-360f, 360f);

                Destroy(p, 0.4f);
            }
        }
    }
}
