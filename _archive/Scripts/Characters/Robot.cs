// Robot.cs
// The Robot character - mechanical, precise, perhaps protective of the Dog.

using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Input;

namespace DogAndRobot.Characters
{
    public class Robot : GridCharacter
    {
        [Header("Robot Settings")]
        [Tooltip("Reference to the Dog (for leash mechanics)")]
        public Dog dog;

        // Reference to our input handler
        private CharacterInputHandler _inputHandler;

        protected override void Awake()
        {
            // Call the parent class's Awake first (important!)
            base.Awake();

            // Get or add the input handler component
            _inputHandler = GetComponent<CharacterInputHandler>();
            if (_inputHandler == null)
            {
                _inputHandler = gameObject.AddComponent<CharacterInputHandler>();
            }

            // Make sure we're using WASD
            _inputHandler.profile = InputProfile.WASD;
        }

        protected override void Update()
        {
            // Call parent Update (handles smooth movement)
            base.Update();

            // Process input
            HandleInput();
        }

        private void HandleInput()
        {
            // If we have a CharacterLinkManager, it handles our input instead
            if (FindFirstObjectByType<CharacterLinkManager>() != null)
                return;

            // Get movement direction from input handler
            GridPosition direction = _inputHandler.GetMovementInput();

            // If there's input, try to move
            if (direction != GridPosition.Zero)
            {
                TryMove(direction);
            }
        }

        protected override bool CanMoveTo(GridPosition position)
        {
            // Start with base class checks
            if (!base.CanMoveTo(position))
                return false;

            // Later: Add leash constraint checks here
            // if (dog != null && WouldBreakLeash(position)) return false;

            return true;
        }
    }
}