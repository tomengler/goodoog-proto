using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace DogAndRobot.Core
{
    public class GameFeelManager : MonoBehaviour
    {
        public static GameFeelManager Instance { get; private set; }

        [SerializeField] private GameFeelSettings _settings;
        [SerializeField] private Volume _postProcessVolume;

        private Camera _camera;
        private Vector3 _cameraBasePosition;
        private bool _isHitStopped;
        private ChromaticAberration _chromaticAberration;

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

            if (_postProcessVolume != null)
                _postProcessVolume.profile.TryGet(out _chromaticAberration);
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

            bool isHorizontal = Mathf.Abs(attackDir.x) > Mathf.Abs(attackDir.y);
            float scaleX, scaleY;
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
                    t * t
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

            target.position = recoilPos;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t * t;
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

        // === CHROMATIC ABERRATION PULSE (Layer C) ===

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

        // === TIME-SCALE PUNCH (Layer C) ===

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
    }
}
