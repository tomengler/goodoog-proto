// GameSettings.cs
// A ScriptableObject that holds all tweakable game values in one place.
// ScriptableObjects are assets that live in your project—changes made
// during Play mode are saved (unlike normal component values).

using UnityEngine;

namespace DogAndRobot.Core
{
    // This attribute lets you create new GameSettings assets from the Unity menu
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
    }
}