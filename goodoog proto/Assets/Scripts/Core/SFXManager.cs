using UnityEngine;

namespace DogAndRobot.Core
{
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        private AudioClip[] _lightHits;
        private AudioClip[] _mediumHits;
        private AudioClip[] _heavyHits;
        private AudioClip[] _heavyHitsFar;
        private AudioClip[] _blocks;
        private AudioClip[] _guardBreaks;
        private AudioClip[] _sprintStarts;
        private AudioClip[] _steps;
        private AudioClip[] _wallExplosions;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _lightHits = LoadClips("LightHit");
            _mediumHits = LoadClips("MediumHit");
            _heavyHits = LoadClips("HeavyHit", "HeavyHitFar");
            _heavyHitsFar = LoadClips("HeavyHitFar");
            _blocks = LoadClips("Block");
            _guardBreaks = LoadClips("GuardBreak");
            _sprintStarts = LoadClips("SprintStart");
            _steps = LoadClips("Step");
            _wallExplosions = LoadClips("WallExplosion");
        }

        private AudioClip[] LoadClips(string prefix, string exclude = null)
        {
            var all = Resources.LoadAll<AudioClip>("SFX");
            var matches = new System.Collections.Generic.List<AudioClip>();
            foreach (var clip in all)
            {
                if (clip.name.StartsWith(prefix))
                {
                    if (exclude != null && clip.name.StartsWith(exclude))
                        continue;
                    matches.Add(clip);
                }
            }
            if (matches.Count == 0)
                Debug.LogWarning($"SFXManager: No clips found with prefix '{prefix}'");
            return matches.ToArray();
        }

        /// <summary>
        /// Spawns a temporary AudioSource that plays the clip and self-destructs.
        /// Each sound gets its own consistent pitch for its full duration.
        /// </summary>
        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            GameObject go = new GameObject("SFX_" + clip.name);
            go.transform.position = transform.position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
            Destroy(go, clip.length / Mathf.Abs(pitch) + 0.1f);
        }

        private void PlayRandom(AudioClip[] clips, float volume = 1f, float pitchVariation = 0.1f)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            PlayOneShot(clip, volume, pitch);
        }

        public static void PlayLightHit() => Instance?.PlayRandom(Instance._lightHits);
        public static void PlayMediumHit() => Instance?.PlayRandom(Instance._mediumHits);
        public static void PlayHeavyHit() => Instance?.PlayRandom(Instance._heavyHits);
        public static void PlayHeavyHitFar() => Instance?.PlayRandom(Instance._heavyHitsFar);
        public static void PlayBlock() => Instance?.PlayRandom(Instance._blocks);
        public static void PlayGuardBreak() => Instance?.PlayRandom(Instance._guardBreaks);
        public static void PlaySprintStart() => Instance?.PlayRandom(Instance._sprintStarts);
        public static void PlayStep() => Instance?.PlayRandom(Instance._steps, 1f, 0.15f);
        public static void PlayWallExplosion() => Instance?.PlayRandom(Instance._wallExplosions);
    }
}
