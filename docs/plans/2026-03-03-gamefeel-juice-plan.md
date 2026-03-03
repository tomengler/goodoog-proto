# Gamefeel Juice Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add snappy, punchy visual feedback to all movement and combat interactions.

**Architecture:** A `GameFeelManager` singleton provides static coroutine-based effects (hitstop, screen shake, squash/stretch, flash). A `GameFeelSettings` ScriptableObject holds all tuning values. Enemy and GridCharacter call into the manager at key moments. Layer C (particles, post-processing, time-scale) is optional and additive.

**Tech Stack:** Unity 2D, C#, coroutines, SpriteRenderer, URP post-processing (Layer C only)

---

### Task 1: Create GameFeelSettings ScriptableObject

**Files:**
- Create: `goodoog proto/Assets/Scripts/Core/GameFeelSettings.cs`

**Step 1: Create the settings class**

```csharp
using UnityEngine;

namespace DogAndRobot.Core
{
    [CreateAssetMenu(fileName = "GameFeelSettings", menuName = "Dog and Robot/Game Feel Settings")]
    public class GameFeelSettings : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("How far the character overshoots the target tile")]
        public float moveOvershootDistance = 0.08f;
        [Tooltip("How fast the overshoot snaps back")]
        public float moveOvershootSpeed = 30f;

        [Header("Hit Stop")]
        [Tooltip("Duration of freeze frame on successful hit")]
        public float hitStopDuration = 0.05f;

        [Header("Screen Shake")]
        [Tooltip("Duration of screen shake on hit")]
        public float hitShakeDuration = 0.1f;
        [Tooltip("Intensity of screen shake on hit")]
        public float hitShakeIntensity = 0.03f;
        [Tooltip("Duration of screen shake on enemy death")]
        public float deathShakeDuration = 0.15f;
        [Tooltip("Intensity of screen shake on enemy death")]
        public float deathShakeIntensity = 0.05f;

        [Header("Squash & Stretch")]
        [Tooltip("How much to squash on the attack axis (0.7 = 70% of original)")]
        public float squashAmount = 0.6f;
        [Tooltip("How much to stretch on the perpendicular axis")]
        public float stretchAmount = 1.3f;
        [Tooltip("Duration of squash/stretch effect")]
        public float squashStretchDuration = 0.1f;

        [Header("Enemy Flash")]
        [Tooltip("Duration of white flash on hit")]
        public float flashDuration = 0.05f;

        [Header("Health Bar")]
        [Tooltip("Scale multiplier for segment pop on removal")]
        public float healthSegmentPopScale = 1.5f;
        [Tooltip("Duration of segment pop animation")]
        public float healthSegmentPopDuration = 0.1f;
        [Tooltip("Duration of health bar reject wobble")]
        public float healthBarWobbleDuration = 0.15f;
        [Tooltip("Intensity of health bar reject wobble")]
        public float healthBarWobbleIntensity = 0.05f;

        [Header("Wrong Hit")]
        [Tooltip("Duration of enemy shake on wrong hit")]
        public float wrongHitShakeDuration = 0.15f;
        [Tooltip("Intensity of enemy shake on wrong hit")]
        public float wrongHitShakeIntensity = 0.05f;
        [Tooltip("How far the attacker recoils on wrong hit")]
        public float recoilDistance = 0.15f;
        [Tooltip("Duration of attacker recoil")]
        public float recoilDuration = 0.15f;

        [Header("Enemy Death")]
        [Tooltip("Duration of death scale-down")]
        public float deathScaleDuration = 0.15f;
    }
}
```

**Step 2: Create the asset via Unity MCP**

Use `manage_asset` to create a `GameFeelSettings.asset` at `Assets/ScriptableObjects/GameFeelSettings.asset`. All default values are baked into the class.

**Step 3: Compile and check for errors**

Run: `refresh_unity` then `read_console` for errors.
Expected: clean compile.

**Step 4: Commit**

```
git add "goodoog proto/Assets/Scripts/Core/GameFeelSettings.cs"
git commit -m "feat: add GameFeelSettings ScriptableObject"
```

---

### Task 2: Create GameFeelManager Singleton

**Files:**
- Create: `goodoog proto/Assets/Scripts/Core/GameFeelManager.cs`

**Step 1: Create the manager class with all Layer A effects**

```csharp
using UnityEngine;
using System.Collections;

namespace DogAndRobot.Core
{
    public class GameFeelManager : MonoBehaviour
    {
        public static GameFeelManager Instance { get; private set; }

        [SerializeField] private GameFeelSettings _settings;

        private Camera _camera;
        private Vector3 _cameraBasePosition;
        private bool _isHitStopped;

        public bool IsHitStopped => _isHitStopped;
        public GameFeelSettings Settings => _settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _camera = Camera.main;
            _cameraBasePosition = _camera.transform.position;
        }

        // === HIT STOP ===

        public static void HitStop(float duration = -1f)
        {
            if (Instance == null) return;
            float d = duration < 0 ? Instance._settings.hitStopDuration : duration;
            Instance.StartCoroutine(Instance.HitStopRoutine(d));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            _isHitStopped = true;
            yield return new WaitForSecondsRealtime(duration);
            _isHitStopped = false;
        }

        // === SCREEN SHAKE ===

        public static void ScreenShake(float duration = -1f, float intensity = -1f)
        {
            if (Instance == null) return;
            float d = duration < 0 ? Instance._settings.hitShakeDuration : duration;
            float i = intensity < 0 ? Instance._settings.hitShakeIntensity : intensity;
            Instance.StartCoroutine(Instance.ScreenShakeRoutine(d, i));
        }

        private IEnumerator ScreenShakeRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                _camera.transform.position = _cameraBasePosition + new Vector3(x, y, 0);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _camera.transform.position = _cameraBasePosition;
        }

        // === SQUASH & STRETCH (direction-aware) ===

        public static void Squash(Transform target, Vector2 attackDirection, float duration = -1f)
        {
            if (Instance == null) return;
            float d = duration < 0 ? Instance._settings.squashStretchDuration : duration;
            Instance.StartCoroutine(Instance.SquashRoutine(target, attackDirection, d,
                Instance._settings.squashAmount, Instance._settings.stretchAmount));
        }

        public static void Stretch(Transform target, Vector2 attackDirection, float duration = -1f)
        {
            if (Instance == null) return;
            float d = duration < 0 ? Instance._settings.squashStretchDuration : duration;
            Instance.StartCoroutine(Instance.SquashRoutine(target, attackDirection, d,
                Instance._settings.stretchAmount, Instance._settings.squashAmount));
        }

        private IEnumerator SquashRoutine(Transform target, Vector2 attackDir, float duration,
            float alongAxis, float perpAxis)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;

            // Determine scale based on attack direction
            float scaleX, scaleY;
            bool isHorizontal = Mathf.Abs(attackDir.x) > Mathf.Abs(attackDir.y);
            if (isHorizontal)
            {
                scaleX = originalScale.x * alongAxis;
                scaleY = originalScale.y * perpAxis;
            }
            else
            {
                scaleX = originalScale.x * perpAxis;
                scaleY = originalScale.y * alongAxis;
            }

            target.localScale = new Vector3(scaleX, scaleY, originalScale.z);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                target.localScale = Vector3.Lerp(
                    new Vector3(scaleX, scaleY, originalScale.z),
                    originalScale,
                    t * t // ease-in for snappy return
                );
                yield return null;
            }

            if (target != null)
                target.localScale = originalScale;
        }

        // === SPRITE FLASH ===

        public static void Flash(SpriteRenderer renderer, float duration = -1f)
        {
            if (Instance == null || renderer == null) return;
            float d = duration < 0 ? Instance._settings.flashDuration : duration;
            Instance.StartCoroutine(Instance.FlashRoutine(renderer, d));
        }

        private IEnumerator FlashRoutine(SpriteRenderer renderer, float duration)
        {
            if (renderer == null) yield break;

            Color originalColor = renderer.color;
            renderer.color = Color.white;
            yield return new WaitForSeconds(duration);

            if (renderer != null)
                renderer.color = originalColor;
        }

        // === HEALTH BAR SEGMENT POP ===

        public static void SegmentPop(GameObject segment, float popScale = -1f, float duration = -1f)
        {
            if (Instance == null || segment == null) return;
            float s = popScale < 0 ? Instance._settings.healthSegmentPopScale : popScale;
            float d = duration < 0 ? Instance._settings.healthSegmentPopDuration : duration;
            Instance.StartCoroutine(Instance.SegmentPopRoutine(segment, s, d));
        }

        private IEnumerator SegmentPopRoutine(GameObject segment, float popScale, float duration)
        {
            if (segment == null) yield break;

            Transform t = segment.transform;
            Vector3 originalScale = t.localScale;
            t.localScale = originalScale * popScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (segment == null) yield break;
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                t.localScale = Vector3.Lerp(originalScale * popScale, Vector3.zero, progress);
                yield return null;
            }

            if (segment != null)
                Destroy(segment);
        }

        // === OBJECT SHAKE (for enemy wrong-hit and health bar wobble) ===

        public static void ObjectShake(Transform target, Vector2 axis, float duration = -1f, float intensity = -1f)
        {
            if (Instance == null || target == null) return;
            float d = duration < 0 ? Instance._settings.wrongHitShakeDuration : duration;
            float i = intensity < 0 ? Instance._settings.wrongHitShakeIntensity : intensity;
            Instance.StartCoroutine(Instance.ObjectShakeRoutine(target, axis.normalized, d, i));
        }

        private IEnumerator ObjectShakeRoutine(Transform target, Vector2 axis, float duration, float intensity)
        {
            if (target == null) yield break;

            Vector3 originalLocalPos = target.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / duration);
                float offset = Mathf.Sin(elapsed * 60f) * intensity * decay;
                target.localPosition = originalLocalPos + new Vector3(axis.x, axis.y, 0) * offset;
                yield return null;
            }

            if (target != null)
                target.localPosition = originalLocalPos;
        }

        // === RECOIL (attacker slides back then returns) ===

        public static void Recoil(Transform target, Vector2 direction, float distance = -1f, float duration = -1f)
        {
            if (Instance == null || target == null) return;
            float dist = distance < 0 ? Instance._settings.recoilDistance : distance;
            float d = duration < 0 ? Instance._settings.recoilDuration : duration;
            Instance.StartCoroutine(Instance.RecoilRoutine(target, direction.normalized, dist, d));
        }

        private IEnumerator RecoilRoutine(Transform target, Vector2 direction, float distance, float duration)
        {
            if (target == null) yield break;

            Vector3 originalPos = target.position;
            Vector3 recoilPos = originalPos - new Vector3(direction.x, direction.y, 0) * distance;

            // Snap to recoil position
            target.position = recoilPos;

            // Lerp back
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t * t; // ease-in for snappy return
                target.position = Vector3.Lerp(recoilPos, originalPos, t);
                yield return null;
            }

            if (target != null)
                target.position = originalPos;
        }

        // === DEATH SCALE ===

        public static void DeathScale(Transform target, float duration = -1f)
        {
            if (Instance == null || target == null) return;
            float d = duration < 0 ? Instance._settings.deathScaleDuration : duration;
            Instance.StartCoroutine(Instance.DeathScaleRoutine(target, d));
        }

        private IEnumerator DeathScaleRoutine(Transform target, float duration)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Squish vertically first, then scale both to zero
                float scaleX = Mathf.Lerp(originalScale.x * 1.3f, 0f, t * t);
                float scaleY = Mathf.Lerp(originalScale.y * 0.5f, 0f, t);
                target.localScale = new Vector3(scaleX, scaleY, originalScale.z);
                yield return null;
            }
        }

        // === HEALTH BAR SCATTER ===

        public static void HealthBarScatter(Transform container, float duration = -1f)
        {
            if (Instance == null || container == null) return;
            float d = duration < 0 ? Instance._settings.healthSegmentPopDuration : duration;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                Instance.StartCoroutine(Instance.ScatterSegmentRoutine(child, d));
            }
        }

        private IEnumerator ScatterSegmentRoutine(Transform segment, float duration)
        {
            if (segment == null) yield break;

            float elapsed = 0f;
            Vector3 originalScale = segment.localScale;
            while (elapsed < duration)
            {
                if (segment == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                segment.localScale = Vector3.Lerp(originalScale, Vector3.zero, t * t);
                yield return null;
            }

            if (segment != null)
                Destroy(segment.gameObject);
        }
    }
}
```

**Step 2: Compile and check for errors**

Run: `refresh_unity` then `read_console` for errors.
Expected: clean compile.

**Step 3: Add GameFeelManager to the scene**

Use Unity MCP `manage_gameobject` to create a `GameFeelManager` GameObject with the `DogAndRobot.Core.GameFeelManager` component. Wire the `GameFeelSettings` asset reference.

**Step 4: Commit**

```
git add "goodoog proto/Assets/Scripts/Core/GameFeelManager.cs"
git commit -m "feat: add GameFeelManager with all Layer A effects"
```

---

### Task 3: Integrate Movement Overshoot into GridCharacter

**Files:**
- Modify: `goodoog proto/Assets/Scripts/Characters/GridCharacter.cs:143-161` (UpdateVisualPosition)

**Step 1: Replace UpdateVisualPosition with overshoot logic**

The current lerp-based movement needs to be replaced with a two-phase approach: move toward an overshoot point past the target, then snap back. The movement should also respect hitstop.

Replace `UpdateVisualPosition()` (lines 143-161) with:

```csharp
private float _moveProgress = 0f;
private Vector3 _moveStartPosition;

private void UpdateVisualPosition()
{
    if (!IsMoving) return;

    // Respect hitstop
    if (GameFeelManager.Instance != null && GameFeelManager.Instance.IsHitStopped) return;

    Vector3 target = _targetWorldPosition + _visualOffset;

    // On first frame of movement, record start position
    if (_moveProgress == 0f)
    {
        _moveStartPosition = transform.position;
    }

    _moveProgress += MoveSpeed * Time.deltaTime;

    if (_moveProgress >= 1f)
    {
        transform.position = target;
        IsMoving = false;
        _moveProgress = 0f;
        return;
    }

    // Ease-out with overshoot: goes slightly past 1.0 then settles
    float t = EaseOutBack(_moveProgress);
    transform.position = Vector3.LerpUnclamped(_moveStartPosition, target, t);
}

private static float EaseOutBack(float t)
{
    // Attempt to read overshoot from GameFeelSettings; fall back to 1.5
    float overshoot = 1.5f;
    if (GameFeelManager.Instance != null && GameFeelManager.Instance.Settings != null)
    {
        // Convert moveOvershootDistance to an easing overshoot factor
        // Default moveOvershootDistance=0.08 maps well to overshoot~1.5
        overshoot = GameFeelManager.Instance.Settings.moveOvershootDistance * 18f;
    }
    float c = overshoot + 1f;
    return 1f + c * Mathf.Pow(t - 1f, 3) + overshoot * Mathf.Pow(t - 1f, 2);
}
```

Also update `TeleportTo` to reset `_moveProgress`:

```csharp
public void TeleportTo(GridPosition position)
{
    _gridPosition = position;
    _targetWorldPosition = _gridPosition.ToWorldPosition(CellSize);
    transform.position = _targetWorldPosition + _visualOffset;
    IsMoving = false;
    _moveProgress = 0f;
}
```

**Step 2: Compile and test**

Run: `refresh_unity` then `read_console` for errors.
Expected: clean compile. Play mode: characters overshoot target tile slightly then snap back.

**Step 3: Commit**

```
git add "goodoog proto/Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add movement overshoot easing to GridCharacter"
```

---

### Task 4: Integrate Hit Juice into Enemy

**Files:**
- Modify: `goodoog proto/Assets/Scripts/Enemies/Enemy.cs`

**Step 1: Add attacker reference tracking**

Enemy needs to know who's attacking it so it can apply effects to the attacker. Modify `TryTakeHit` to accept an optional attacker transform. Update the signature and callers.

In `Enemy.cs`, change `TryTakeHit`:

```csharp
public bool TryTakeHit(DamageType damageType, GridPosition attackDirection, Transform attacker = null)
```

In `GridCharacter.cs` line 72, update the call:

```csharp
bool hit = enemy.TryTakeHit(damageType, direction, transform);
```

**Step 2: Add juice to successful hit path**

In `Enemy.TryTakeHit`, after `_damageSequence.RemoveAt(0)` (line 128), replace the health segment destruction and add juice calls:

```csharp
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

// Remove the first health segment with pop effect
if (_healthSegments.Count > 0)
{
    GameFeelManager.SegmentPop(_healthSegments[0]);
    _healthSegments.RemoveAt(0);
}
```

Note: `SegmentPop` now handles the destruction, so remove the old `Destroy(_healthSegments[0])` call.

**Step 3: Add juice to wrong hit path**

Replace `OnWrongHit()` body:

```csharp
protected virtual void OnWrongHit(GridPosition attackDirection, Transform attacker)
{
    Vector2 attackDir = new Vector2(attackDirection.x, attackDirection.y);

    // Enemy shakes in place
    GameFeelManager.ObjectShake(transform, attackDir);

    // Health bar wobbles
    if (_healthBarContainer != null)
        GameFeelManager.ObjectShake(_healthBarContainer, Vector2.right,
            GameFeelManager.Instance?.Settings?.healthBarWobbleDuration ?? 0.15f,
            GameFeelManager.Instance?.Settings?.healthBarWobbleIntensity ?? 0.05f);

    // Attacker recoils
    if (attacker != null)
        GameFeelManager.Recoil(attacker, attackDir);
}
```

Update the call site in `TryTakeHit` line 120:

```csharp
OnWrongHit(attackDirection, attacker);
```

**Step 4: Add juice to death**

Replace `Die()`:

```csharp
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

    // Delayed destroy to let effects play
    Destroy(gameObject, settings?.deathScaleDuration ?? 0.15f);
}
```

**Step 5: Compile and test**

Run: `refresh_unity` then `read_console` for errors.
Expected: clean compile. Play mode: hits feel punchy with hitstop, flash, squash/stretch. Wrong hits cause enemy shake and attacker recoil. Deaths have scale-down and screen shake.

**Step 6: Commit**

```
git add "goodoog proto/Assets/Scripts/Enemies/Enemy.cs" "goodoog proto/Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: integrate Layer A juice into hit, wrong-hit, and death flows"
```

---

### Task 5: Add Layer C - Particles

**Files:**
- Create: `goodoog proto/Assets/Scripts/Core/GameFeelParticles.cs`
- Modify: `goodoog proto/Assets/Scripts/Enemies/Enemy.cs` (add particle calls)

**Step 1: Create a particle spawner component**

```csharp
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

                // Spread particles in a cone around the hit direction
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
    }
}
```

**Step 2: Add particle calls to Enemy.cs**

In the successful hit section of `TryTakeHit`, after the existing juice calls:

```csharp
// Layer C: hit particles
Color particleColor = damageType == DamageType.Dog ? dogDamageColor : robotDamageColor;
GameFeelParticles.HitBurst(transform.position, attackDir, particleColor);
```

In `OnWrongHit`, add:

```csharp
// Layer C: block sparks
GameFeelParticles.BlockSparks(transform.position);
```

In `Die`, add:

```csharp
// Layer C: death burst
GameFeelParticles.DeathBurst(transform.position);
```

**Step 3: Add GameFeelParticles to scene**

Use Unity MCP to create a `GameFeelParticles` GameObject with the component, wire the Square prefab reference.

**Step 4: Compile and test**

Expected: particles burst on hits, sparks on wrong hits, explosion on death. If `GameFeelParticles` is missing from the scene or prefab is null, effects silently skip (graceful degradation).

**Step 5: Commit**

```
git add "goodoog proto/Assets/Scripts/Core/GameFeelParticles.cs" "goodoog proto/Assets/Scripts/Enemies/Enemy.cs"
git commit -m "feat: add Layer C hit/death/block particles"
```

---

### Task 6: Add Layer C - Post-Processing & Time-Scale

**Files:**
- Modify: `goodoog proto/Assets/Scripts/Core/GameFeelManager.cs` (add chromatic aberration pulse and time-scale punch)
- Scene setup: add URP post-processing volume

**Step 1: Add chromatic aberration pulse to GameFeelManager**

Add to `GameFeelManager.cs`:

```csharp
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
```

Add fields:

```csharp
[SerializeField] private Volume _postProcessVolume;
private ChromaticAberration _chromaticAberration;
```

In `Awake`, after camera setup:

```csharp
if (_postProcessVolume != null)
    _postProcessVolume.profile.TryGet(out _chromaticAberration);
```

Add methods:

```csharp
// === CHROMATIC ABERRATION PULSE ===

public static void ChromaticPulse(float intensity = 0.5f, float duration = 0.1f)
{
    if (Instance == null || Instance._chromaticAberration == null) return;
    Instance.StartCoroutine(Instance.ChromaticPulseRoutine(intensity, duration));
}

private IEnumerator ChromaticPulseRoutine(float intensity, float duration)
{
    _chromaticAberration.active = true;
    _chromaticAberration.intensity.value = intensity;

    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.unscaledDeltaTime;
        float t = elapsed / duration;
        _chromaticAberration.intensity.value = Mathf.Lerp(intensity, 0f, t * t);
        yield return null;
    }

    _chromaticAberration.intensity.value = 0f;
}

// === TIME-SCALE PUNCH ===

public static void TimeScalePunch(float scale = 0.7f, float duration = 0.1f)
{
    if (Instance == null) return;
    Instance.StartCoroutine(Instance.TimeScalePunchRoutine(scale, duration));
}

private IEnumerator TimeScalePunchRoutine(float scale, float duration)
{
    Time.timeScale = scale;
    yield return new WaitForSecondsRealtime(duration);
    Time.timeScale = 1f;
}
```

**Step 2: Add calls in Enemy.cs**

In the successful hit section:

```csharp
// Layer C: chromatic aberration
GameFeelManager.ChromaticPulse(0.3f, 0.08f);
```

In `Die`:

```csharp
// Layer C: stronger chromatic aberration
GameFeelManager.ChromaticPulse(0.6f, 0.12f);

// Layer C: time-scale punch
GameFeelManager.TimeScalePunch(0.7f, 0.1f);
```

**Step 3: Scene setup**

Use Unity MCP to create a Global Volume GameObject with a URP Volume component and a new Volume Profile. Add ChromaticAberration override to the profile. Wire the volume reference on GameFeelManager.

Note: if URP post-processing setup is complex via MCP, this can be done manually by the user. The code gracefully handles null `_postProcessVolume`.

**Step 4: Compile and test**

Expected: brief RGB split on hits, stronger on death. Brief slow-motion on enemy death.

**Step 5: Commit**

```
git add "goodoog proto/Assets/Scripts/Core/GameFeelManager.cs" "goodoog proto/Assets/Scripts/Enemies/Enemy.cs"
git commit -m "feat: add Layer C chromatic aberration pulse and time-scale punch"
```

---

### Task 7: Save Scene and Final Commit

**Step 1: Save the scene**

Use Unity MCP `manage_scene(action="save")`.

**Step 2: Final commit with all scene changes**

```
git add "goodoog proto/Assets/Scenes/SampleScene.unity"
git commit -m "feat: save scene with GameFeelManager and GameFeelParticles"
```

**Step 3: Verify in play mode**

Checklist:
- [ ] Movement has subtle overshoot
- [ ] Correct hits: hitstop, squash/stretch, flash, screen shake, health pop, particles
- [ ] Wrong hits: enemy shake, health bar wobble, attacker recoil, sparks
- [ ] Enemy death: scale-down, stronger shake, health scatter, death burst, chromatic pulse, time slow
- [ ] All effects respect attack direction
- [ ] No errors in console
