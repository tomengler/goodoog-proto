// Dog.cs
// The Dog character - energetic, curious, maybe easily distracted.

using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Input;

namespace DogAndRobot.Characters
{
    public class Dog : GridCharacter
    {
        [Header("Dog Settings")]
        [Tooltip("Reference to the Robot (for leash mechanics)")]
        public Robot robot;

        // Reference to our input handler
        private CharacterInputHandler _inputHandler;

        protected override void Awake()
        {
            // Call the parent class's Awake first
            base.Awake();

            // Get or add the input handler component
            _inputHandler = GetComponent<CharacterInputHandler>();
            if (_inputHandler == null)
            {
                _inputHandler = gameObject.AddComponent<CharacterInputHandler>();
            }

            // Make sure we're using IJKL
            _inputHandler.profile = InputProfile.IJKL;

            // Force the keys to reconfigure based on the new profile
            _inputHandler.ConfigureKeys();
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
            // if (robot != null && WouldBreakLeash(position)) return false;

            return true;
        }

        public override DamageType GetDamageType()
{
    return DamageType.Dog;
}
    }
}