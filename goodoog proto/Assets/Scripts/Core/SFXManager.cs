using UnityEngine;

namespace DogAndRobot.Core
{
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        private AudioClip[] _lightHits;
        private AudioClip[] _mediumHits;
        private AudioClip[] _heavyHits;
        private AudioClip[] _blocks;
        private AudioClip[] _guardBreaks;

        private AudioSource _source;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            _lightHits = LoadClips("LightHit");
            _mediumHits = LoadClips("MediumHit");
            _heavyHits = LoadClips("HeavyHit");
            _blocks = LoadClips("Block");
            _guardBreaks = LoadClips("GuardBreak");
        }

        private AudioClip[] LoadClips(string prefix)
        {
            var all = Resources.LoadAll<AudioClip>("SFX");
            var matches = new System.Collections.Generic.List<AudioClip>();
            foreach (var clip in all)
            {
                if (clip.name.StartsWith(prefix))
                    matches.Add(clip);
            }
            if (matches.Count == 0)
                Debug.LogWarning($"SFXManager: No clips found with prefix '{prefix}'");
            return matches.ToArray();
        }

        private void PlayRandom(AudioClip[] clips, float volume = 1f, float pitchVariation = 0.1f)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            _source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            _source.PlayOneShot(clip, volume);
        }

        public static void PlayLightHit() => Instance?.PlayRandom(Instance._lightHits);
        public static void PlayMediumHit() => Instance?.PlayRandom(Instance._mediumHits);
        public static void PlayHeavyHit() => Instance?.PlayRandom(Instance._heavyHits);
        public static void PlayBlock() => Instance?.PlayRandom(Instance._blocks);
        public static void PlayGuardBreak() => Instance?.PlayRandom(Instance._guardBreaks);
    }
}
