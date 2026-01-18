// InputSwapManager.cs
// Monitors the Robot and Dog positions and swaps their control schemes
// when they cross over each other horizontally.
// This keeps controls feeling natural: left character = left-hand keys (WASD),
// right character = right-hand keys (IJKL).

using UnityEngine;
using DogAndRobot.Characters;
using DogAndRobot.Input;

namespace DogAndRobot.Core
{
    public class InputSwapManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("When enabled, controls swap based on character positions. When disabled, Robot always uses WASD, Dog always uses IJKL.")]
        public bool enableDynamicSwap = true;

        [Header("Character References")]
        public Robot robot;
        public Dog dog;

        [Header("Debug")]
        [SerializeField] private bool _controlsAreSwapped = false;

        // References to the input handlers
        private CharacterInputHandler _robotInput;
        private CharacterInputHandler _dogInput;

        private void Start()
        {
            // Get the input handlers from each character
            _robotInput = robot.GetComponent<CharacterInputHandler>();
            _dogInput = dog.GetComponent<CharacterInputHandler>();

            if (_robotInput == null || _dogInput == null)
            {
                Debug.LogError("InputSwapManager: Could not find input handlers on characters!");
                enabled = false; // Disable this script if setup failed
                return;
            }

            // Do an initial check in case they start in an unusual configuration
            CheckAndSwapIfNeeded();
        }

        private void ResetToDefaultControls()
        {
            _robotInput.profile = InputProfile.WASD;
            _robotInput.ConfigureKeys();

            _dogInput.profile = InputProfile.IJKL;
            _dogInput.ConfigureKeys();

            _controlsAreSwapped = false;

            Debug.Log("Controls reset to default (Robot=WASD, Dog=IJKL)");
        }

        private void Update()
        {
            if (enableDynamicSwap)
            {
                CheckAndSwapIfNeeded();
            }
            else if (_controlsAreSwapped)
            {
                // If swapping was just disabled and controls are currently swapped,
                // reset to default configuration
                ResetToDefaultControls();
            }
        }

        private void CheckAndSwapIfNeeded()
        {
            // Compare horizontal (X) positions
            // Dog is to the LEFT of Robot when Dog's X is less than Robot's X
            bool dogIsOnLeft = dog.GridPosition.x < robot.GridPosition.x;

            // Normal state: Robot on left (WASD), Dog on right (IJKL)
            // Swapped state: Dog on left (WASD), Robot on right (IJKL)

            // We also need to handle when they're on the same X position
            // In that case, keep whatever state we're currently in (no swap)
            bool dogAndRobotOnSameX = dog.GridPosition.x == robot.GridPosition.x;

            if (dogAndRobotOnSameX)
            {
                // Don't swap when vertically aligned - would cause flickering
                return;
            }

            // Should controls be swapped? (Dog is on left = swapped)
            bool shouldBeSwapped = dogIsOnLeft;

            // Only take action if the state needs to change
            if (shouldBeSwapped != _controlsAreSwapped)
            {
                SwapControls();
            }
        }

        private void SwapControls()
        {
            _controlsAreSwapped = !_controlsAreSwapped;

            if (_controlsAreSwapped)
            {
                // Dog is on left, so Dog gets WASD, Robot gets IJKL
                _dogInput.profile = InputProfile.WASD;
                _dogInput.ConfigureKeys();

                _robotInput.profile = InputProfile.IJKL;
                _robotInput.ConfigureKeys();
            }
            else
            {
                // Normal: Robot on left gets WASD, Dog on right gets IJKL
                _robotInput.profile = InputProfile.WASD;
                _robotInput.ConfigureKeys();

                _dogInput.profile = InputProfile.IJKL;
                _dogInput.ConfigureKeys();
            }

            Debug.Log($"Controls swapped! Swapped state: {_controlsAreSwapped}");
        }
    }
}