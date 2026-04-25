// ============================================================================
// PlayerVoiceMovement.cs
// Forward-only movement driven by microphone loudness with mouse rotation.
// Uses CharacterController for reliable ground movement without physics jitter.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using HotShipHai.Voice;

namespace HotShipHai.Player
{
    /// <summary>
    /// Moves the player FORWARD based on microphone loudness.
    /// Rotation is controlled by horizontal mouse movement.
    /// No backward movement. No jumping.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerVoiceMovement : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("References")]
        [Tooltip("The MicrophoneInput (or any IMicrophoneInputProvider) component. " +
            "If left empty, will auto-find on the same GameObject.")]
        [SerializeField] private MicrophoneInput microphoneInput;

        [Header("Movement")]
        [Tooltip("Minimum forward speed when voice is just above threshold.")]
        [SerializeField] private float minMoveSpeed = 1f;

        [Tooltip("Maximum forward speed at full loudness.")]
        [SerializeField] private float maxMoveSpeed = 8f;

        [Tooltip("How quickly the character accelerates/decelerates. " +
            "Higher = snappier response.")]
        [Range(1f, 20f)]
        [SerializeField] private float acceleration = 8f;

        [Header("Mouse Rotation")]
        [Tooltip("Horizontal mouse sensitivity for player rotation.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float mouseSensitivity = 3f;

        [Tooltip("Smoothing for rotation. Lower = smoother turns.")]
        [Range(1f, 30f)]
        [SerializeField] private float rotationSmoothing = 15f;

        [Header("Gravity")]
        [Tooltip("Gravity force applied when not grounded.")]
        [SerializeField] private float gravity = -20f;

        // ====================================================================
        // Internal State
        // ====================================================================

        private CharacterController _controller;
        private IMicrophoneInputProvider _inputProvider;

        private float _currentSpeed;
        private float _targetYRotation;
        private float _verticalVelocity;
        private Vector3 _lastMoveDirection;

        // ====================================================================
        // Public Read-Only Properties (for UI)
        // ====================================================================

        /// <summary>Current actual movement speed.</summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>Target speed based on mic loudness (before smoothing).</summary>
        public float TargetSpeed { get; private set; }

        /// <summary>Whether the character is currently moving.</summary>
        public bool IsMoving => _currentSpeed > 0.01f;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_controller == null)
            {
                Debug.LogError("[PlayerVoiceMovement] CharacterController not found! " +
                    "Add one to the Player GameObject.");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            // Lock the cursor to the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Find the microphone input provider
            ResolveInputProvider();

            // Initialize rotation to current facing
            _targetYRotation = transform.eulerAngles.y;
        }

        private void Update()
        {
            HandleMouseRotation();
            HandleVoiceMovement();
            ApplyGravity();
            ApplyMovement();
        }

        // ====================================================================
        // Mouse Rotation
        // ====================================================================

        /// <summary>
        /// Rotates the player left/right based on horizontal mouse input.
        /// </summary>
        private void HandleMouseRotation()
        {
            // Read mouse delta from New Input System
            if (Mouse.current == null) return;

            float mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity * 0.1f;

            // Accumulate target rotation
            _targetYRotation += mouseX;

            // Smoothly interpolate current rotation toward target
            float currentY = transform.eulerAngles.y;
            float smoothedY = Mathf.LerpAngle(
                currentY,
                _targetYRotation,
                Time.deltaTime * rotationSmoothing
            );

            transform.rotation = Quaternion.Euler(0f, smoothedY, 0f);
        }

        // ====================================================================
        // Voice-Driven Movement
        // ====================================================================

        /// <summary>
        /// Computes forward speed based on microphone loudness.
        /// Only moves forward — never backward.
        /// </summary>
        private void HandleVoiceMovement()
        {
            if (_inputProvider == null || !_inputProvider.IsDeviceReady)
            {
                TargetSpeed = 0f;
                _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, Time.deltaTime * acceleration);
                return;
            }

            if (_inputProvider.IsVoiceActive)
            {
                // Map loudness [0, 1] → speed [minMoveSpeed, maxMoveSpeed]
                float loudness = _inputProvider.Loudness;
                TargetSpeed = Mathf.Lerp(minMoveSpeed, maxMoveSpeed, loudness);
            }
            else
            {
                TargetSpeed = 0f;
            }

            // Smoothly accelerate / decelerate
            _currentSpeed = Mathf.Lerp(
                _currentSpeed,
                TargetSpeed,
                Time.deltaTime * acceleration
            );

            // Kill tiny residual speed to stop cleanly
            if (_currentSpeed < 0.01f)
            {
                _currentSpeed = 0f;
            }
        }

        // ====================================================================
        // Gravity
        // ====================================================================

        /// <summary>
        /// Applies gravity when the character is not grounded.
        /// No jumping is implemented by design.
        /// </summary>
        private void ApplyGravity()
        {
            if (_controller.isGrounded)
            {
                // Small downward force to keep grounded (prevents floating)
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }

        // ====================================================================
        // Apply Final Movement
        // ====================================================================

        /// <summary>
        /// Combines forward movement + gravity and moves the CharacterController.
        /// </summary>
        private void ApplyMovement()
        {
            // Forward is always the player's local forward direction
            Vector3 moveDir = transform.forward * _currentSpeed;

            // Add gravity
            moveDir.y = _verticalVelocity;

            // Move via CharacterController
            _controller.Move(moveDir * Time.deltaTime);

            _lastMoveDirection = moveDir;
        }

        // ====================================================================
        // Input Provider Resolution
        // ====================================================================

        /// <summary>
        /// Finds the IMicrophoneInputProvider. Checks the assigned reference first,
        /// then searches the same GameObject, then searches children.
        /// </summary>
        private void ResolveInputProvider()
        {
            // Use assigned reference if available
            if (microphoneInput != null)
            {
                _inputProvider = microphoneInput;
                return;
            }

            // Try to find on same GameObject
            _inputProvider = GetComponent<IMicrophoneInputProvider>();

            if (_inputProvider != null) return;

            // Try to find on children
            _inputProvider = GetComponentInChildren<IMicrophoneInputProvider>();

            if (_inputProvider != null) return;

            // Try to find anywhere in scene
            var found = FindAnyObjectByType<MicrophoneInput>();
            if (found != null)
            {
                _inputProvider = found;
                Debug.Log("[PlayerVoiceMovement] Found MicrophoneInput in scene.");
                return;
            }

            Debug.LogError("[PlayerVoiceMovement] No IMicrophoneInputProvider found! " +
                "Assign a MicrophoneInput component.");
        }

        // ====================================================================
        // Public Methods
        // ====================================================================

        /// <summary>
        /// Allows hot-swapping the input provider at runtime.
        /// Call this to switch from loudness detection to AI classification, etc.
        /// </summary>
        public void SetInputProvider(IMicrophoneInputProvider provider)
        {
            _inputProvider = provider;
            Debug.Log($"[PlayerVoiceMovement] Input provider changed to: " +
                $"{provider?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Unlocks the cursor (useful for menus / pause).
        /// </summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Re-locks the cursor to center screen.
        /// </summary>
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
