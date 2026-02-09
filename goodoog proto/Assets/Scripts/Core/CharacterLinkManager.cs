// CharacterLinkManager.cs
// Manages the joined/separated state of Robot and Dog.
// When joined: both characters occupy the same space and move as one.
// When separated: characters move independently.
// To separate: input opposing directions simultaneously.
// To rejoin: move into the same grid space.

using UnityEngine;
using System.Collections;
using DogAndRobot.Characters;
using DogAndRobot.Input;

namespace DogAndRobot.Core
{
    public class CharacterLinkManager : MonoBehaviour
    {
        [Header("Character References")]
        public Robot robot;
        public Dog dog;
        
        [Header("State")]
        [SerializeField] private bool _isJoined = true;
        
        // Public property so other scripts can check the state
        public bool IsJoined => _isJoined;
        
        // Settings access
        private float SeparationInputWindow => SettingsManager.Instance?.settings?.separationInputWindow ?? 0.15f;
        private float JoinedOffset => SettingsManager.Instance?.settings?.joinedOffset ?? 0.25f;
        private float JoinedScale => SettingsManager.Instance?.settings?.joinedScale ?? 0.5f;
        
        // Input handler references
        private CharacterInputHandler _robotInput;
        private CharacterInputHandler _dogInput;
        
        // Track recent inputs for separation detection
        private GridPosition _lastRobotDirection = GridPosition.Zero;
        private GridPosition _lastDogDirection = GridPosition.Zero;
        private float _lastRobotInputTime = -999f;
        private float _lastDogInputTime = -999f;
        
        // Track position before movement for clean separation
        private GridPosition _positionBeforeInput;
        private bool _hasPendingMovement = false;
        
        // Setup flag
        private bool _setupComplete = false;
        
        private void Awake()
        {
            // We'll get the input handlers in Start, after characters have initialized
        }
        
        private void Start()
        {
            // Give characters a frame to set up their input handlers
            StartCoroutine(DelayedSetup());
        }
        
        private IEnumerator DelayedSetup()
        {
            // Wait one frame for Robot and Dog Awake() to run
            yield return null;
            
            _robotInput = robot.GetComponent<CharacterInputHandler>();
            _dogInput = dog.GetComponent<CharacterInputHandler>();
            
            if (_robotInput == null || _dogInput == null)
            {
                Debug.LogError("CharacterLinkManager: Could not find input handlers!");
                enabled = false;
                yield break;
            }
            
            // Start joined - make sure dog is at robot's position
            if (_isJoined)
            {
                dog.TeleportTo(robot.GridPosition);
                UpdateJoinedVisuals();
            }
            
            _setupComplete = true;
        }
        
        private void Update()
        {
            // Don't do anything until setup is complete
            if (!_setupComplete) return;
            
            // We handle all input here instead of in the individual character scripts
            HandleLinkedInput();
            
            // Check if separated characters have rejoined
            CheckForRejoin();
        }
        
        private void HandleLinkedInput()
        {
            // Get input from both handlers
            GridPosition robotDirection = _robotInput.GetMovementInput();
            GridPosition dogDirection = _dogInput.GetMovementInput();
            
            if (_isJoined)
            {
                HandleJoinedInput(robotDirection, dogDirection);
            }
            else
            {
                HandleSeparateInput(robotDirection, dogDirection);
            }
        }
        
        private void HandleJoinedInput(GridPosition robotDir, GridPosition dogDir)
        {
            // Record inputs with timestamps
            if (robotDir != GridPosition.Zero)
            {
                _lastRobotDirection = robotDir;
                _lastRobotInputTime = Time.time;
            }
            
            if (dogDir != GridPosition.Zero)
            {
                _lastDogDirection = dogDir;
                _lastDogInputTime = Time.time;
            }
            
            // Check if both inputs happened within our window
            bool bothInputsRecent = (Time.time - _lastRobotInputTime) < SeparationInputWindow
                                 && (Time.time - _lastDogInputTime) < SeparationInputWindow;
            
            if (bothInputsRecent && AreOpposingDirections(_lastRobotDirection, _lastDogDirection))
            {
                // Snap back to position before the first input
                if (_hasPendingMovement)
                {
                    robot.TeleportTo(_positionBeforeInput);
                    dog.TeleportTo(_positionBeforeInput);
                }
                
                // Clear visual offsets before separating
                ClearVisualOffsets();
                
                // Now separate from that clean position
                Separate(_lastRobotDirection, _lastDogDirection);
                
                _lastRobotDirection = GridPosition.Zero;
                _lastDogDirection = GridPosition.Zero;
                _hasPendingMovement = false;
                return;
            }
            
            // Handle normal movement
            GridPosition moveDirection = GridPosition.Zero;
            
            if (robotDir != GridPosition.Zero)
            {
                moveDirection = robotDir;
            }
            else if (dogDir != GridPosition.Zero)
            {
                moveDirection = dogDir;
            }
            
            if (moveDirection != GridPosition.Zero)
            {
                // Remember where we were before moving
                if (!_hasPendingMovement)
                {
                    _positionBeforeInput = robot.GridPosition;
                    _hasPendingMovement = true;
                }
                
                robot.TryMove(moveDirection);
                dog.TryMove(moveDirection);
            }
            
            // Clear pending state once the window has passed
            if (_hasPendingMovement && 
                (Time.time - _lastRobotInputTime) >= SeparationInputWindow &&
                (Time.time - _lastDogInputTime) >= SeparationInputWindow)
            {
                _hasPendingMovement = false;
            }
        }
        
        private void HandleSeparateInput(GridPosition robotDir, GridPosition dogDir)
        {
            // When separated, each character moves independently
            if (robotDir != GridPosition.Zero)
            {
                robot.TryMove(robotDir);
            }
            
            if (dogDir != GridPosition.Zero)
            {
                dog.TryMove(dogDir);
            }
        }
        
        private bool AreOpposingDirections(GridPosition dir1, GridPosition dir2)
        {
            // Both must have input
            if (dir1 == GridPosition.Zero || dir2 == GridPosition.Zero)
                return false;
            
            // Check if they're exact opposites
            // Opposing means they sum to zero: Left + Right = (-1,0) + (1,0) = (0,0)
            GridPosition sum = dir1 + dir2;
            return sum == GridPosition.Zero;
        }
        
        private void Separate(GridPosition robotDir, GridPosition dogDir)
        {
            Debug.Log("Separating characters!");
            
            _isJoined = false;
            
            // Move each character in their input direction
            robot.TryMove(robotDir);
            dog.TryMove(dogDir);
        }
        
        private void CheckForRejoin()
        {
            // Only check if currently separated
            if (_isJoined) return;
            
            // If both characters are on the same grid position, rejoin
            if (robot.GridPosition == dog.GridPosition)
            {
                Debug.Log("Characters rejoined!");
                _isJoined = true;
                UpdateJoinedVisuals();
            }
        }
        
        private void UpdateJoinedVisuals()
{
    if (!_isJoined) return;
    
    // Squish horizontally to half width, keep full height
    robot.SetVisualScale(JoinedScale, 1f);
    dog.SetVisualScale(JoinedScale, 1f);
    
    // Offset so they sit next to each other within the cell
    Vector3 robotOffset = new Vector3(-JoinedOffset, 0, 0);
    Vector3 dogOffset = new Vector3(JoinedOffset, 0, 0);
    
    robot.SetVisualOffset(robotOffset);
    dog.SetVisualOffset(dogOffset);
    
    // Force position update to apply offset immediately
    robot.TeleportTo(robot.GridPosition);
    dog.TeleportTo(dog.GridPosition);
}
        
        private void ClearVisualOffsets()
{
    robot.SetVisualOffset(Vector3.zero);
    dog.SetVisualOffset(Vector3.zero);
    
    robot.SetVisualScale(1f, 1f);
    dog.SetVisualScale(1f, 1f);
}
        
        /// <summary>
        /// Force the characters to join at the robot's position.
        /// Useful for spawning, level resets, etc.
        /// </summary>
        public void ForceJoin()
        {
            dog.TeleportTo(robot.GridPosition);
            _isJoined = true;
            UpdateJoinedVisuals();
        }
        
        /// <summary>
        /// Force the characters to separate without movement.
        /// </summary>
        public void ForceSeparate()
        {
            _isJoined = false;
            ClearVisualOffsets();
        }
    }
}   