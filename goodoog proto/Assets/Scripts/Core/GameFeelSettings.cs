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
        [Tooltip("How much to squash on the attack axis (0.6 = 60% of original)")]
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

        [Header("Enemy Launch")]
        [Tooltip("Speed of enemy launch toward wall (world units/sec)")]
        public float launchSpeed = 20f;
        [Header("Sprint Feel")]
        [Tooltip("How much to squash along sprint movement axis")]
        public float sprintSquashAmount = 0.8f;
        [Tooltip("How much to stretch perpendicular to sprint movement")]
        public float sprintStretchAmount = 1.2f;
        [Tooltip("Screen shake intensity during sprint")]
        public float sprintShakeIntensity = 0.01f;
        [Tooltip("Time between skid particle spawns during braking")]
        public float brakeSkidInterval = 0.03f;
    }
}
