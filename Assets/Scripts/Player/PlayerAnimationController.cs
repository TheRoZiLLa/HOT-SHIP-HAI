// ============================================================================
// PlayerAnimationController.cs
// Bridges the PlayerVoiceMovement system with a Unity Animator.
// Supports both Blend Tree (Speed float) and State Machine (IsWalking/IsRunning bools) setups.
// ============================================================================

using UnityEngine;
using HotShipHai.Voice;

namespace HotShipHai.Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [Tooltip("Reference to the player's movement script. Auto-detects if left empty.")]
        [SerializeField] private PlayerVoiceMovement playerMovement;

        [Header("Animator Parameters (Case-Sensitive)")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string isWalkingParameter = "IsWalking";
        [SerializeField] private string isRunningParameter = "IsRunning";

        [Header("Blend Tree Settings")]
        [Tooltip("Smoothing time for the Speed parameter to prevent snappy animation changes. Set to 0 for instant changes.")]
        [SerializeField] private float speedDampTime = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private Animator _animator;

        private int _speedHash;
        private int _isMovingHash;
        private int _isWalkingHash;
        private int _isRunningHash;

        private bool _missingMovementWarningShown = false;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            
            // Cache parameter hashes
            _speedHash = Animator.StringToHash(speedParameter);
            _isMovingHash = Animator.StringToHash(isMovingParameter);
            _isWalkingHash = Animator.StringToHash(isWalkingParameter);
            _isRunningHash = Animator.StringToHash(isRunningParameter);
            
            // Auto-assign if missing
            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerVoiceMovement>();
                if (playerMovement == null)
                {
                    playerMovement = FindAnyObjectByType<PlayerVoiceMovement>();
                }
            }
        }

        private void Update()
        {
            if (playerMovement == null)
            {
                if (!_missingMovementWarningShown)
                {
                    Debug.LogWarning("[PlayerAnimationController] PlayerVoiceMovement reference is missing! Please drag it into the inspector.");
                    _missingMovementWarningShown = true;
                }
                return;
            }

            if (_animator == null) return;

            // Debug log to check the speed value
            if (showDebugLogs && playerMovement.CurrentSpeed > 0.1f)
            {
                Debug.Log($"[PlayerAnimationController] Sending Speed to Animator: {playerMovement.CurrentSpeed:F2}");
            }

            // METHOD 1: Float-based (Best for 1D Blend Trees)
            if (speedDampTime > 0f)
            {
                _animator.SetFloat(_speedHash, playerMovement.CurrentSpeed, speedDampTime, Time.deltaTime);
            }
            else
            {
                _animator.SetFloat(_speedHash, playerMovement.CurrentSpeed);
            }
            
            _animator.SetBool(_isMovingHash, playerMovement.IsMoving);


            // METHOD 2: Bool-based (Best for distinct State Machines)
            bool isWalking = playerMovement.MovementState == VoiceMovementState.Walk && playerMovement.IsMoving;
            bool isRunning = playerMovement.MovementState == VoiceMovementState.Run && playerMovement.IsMoving;

            _animator.SetBool(_isWalkingHash, isWalking);
            _animator.SetBool(_isRunningHash, isRunning);
        }
    }
}
