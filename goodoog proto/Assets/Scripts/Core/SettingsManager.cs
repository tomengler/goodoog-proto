// SettingsManager.cs
// Provides global access to GameSettings from any script.
// Lives in the scene and holds a reference to the settings asset.

using UnityEngine;

namespace DogAndRobot.Core
{
    public class SettingsManager : MonoBehaviour
    {
        [Header("Settings Reference")]
        public GameSettings settings;

        // Static instance so any script can access settings via SettingsManager.Instance.settings
        public static SettingsManager Instance { get; private set; }

        private void Awake()
        {
            // Simple singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple SettingsManagers found! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}