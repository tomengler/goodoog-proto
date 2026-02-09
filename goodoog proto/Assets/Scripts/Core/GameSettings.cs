using UnityEngine;

namespace DogAndRobot.Core
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Dog and Robot/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("Size of each grid cell in world units")]
        public float cellSize = 1f;
        
        [Header("Movement Settings")]
        [Tooltip("How fast characters visually move to their target position")]
        public float moveSpeed = 10f;
        
        [Tooltip("How close to target before considered 'arrived'")]
        public float arrivalThreshold = 0.01f;
        
        [Header("Input Settings")]
        [Tooltip("Minimum time between moves (prevents accidental double-moves)")]
        public float inputCooldown = 0.15f;
        
        [Header("Link/Separation Settings")]
        [Tooltip("Time window (in seconds) for detecting opposing inputs to separate")]
        public float separationInputWindow = 0.15f;
        
        [Header("Joined Visual Settings")]
        [Tooltip("How far each character offsets from center when joined")]
        public float joinedOffset = 0.25f;
        
        [Tooltip("Scale of each character when joined (0.5 = half size)")]
        public float joinedScale = 0.5f;
    }
}