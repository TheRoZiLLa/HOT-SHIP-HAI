// ============================================================================
// PlayerVoiceMovement.cs
// Forward-only movement driven by voice pattern detection.
// Uses IVoiceDetectionProvider for state-based movement (Idle/Walk/Run).
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using HotShipHai.Voice;

namespace HotShipHai.Player
{
    /// <summary>
    /// Moves the player forward based on detected voice patterns:
    /// <list type="bullet">
    ///   <item><b>Idle</b> — no movement (silence / noise / invalid patterns)</item>
    ///   <item><b>Walk</b> — sustained voice drives forward at walk speed</item>
    ///   <item><b>Run</b> — rapid voice bursts drive forward at run speed</item>
    /// </list>
    /// Rotation is controlled by horizontal mouse input.
    /// No backward movement. No jumping.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerVoiceMovement : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("Voice Detection Provider")]
        [Tooltip("The LoudnessDetectionProvider (or any IVoiceDetectionProvider). " +
            "Auto-found in scene if left empty.")]
        [SerializeField] private LoudnessDetectionProvider defaultProvider;

        [Header("Movement Speeds")]
        [Tooltip("Minimum walk speed at low voice volume.")]
        [SerializeField] private float minWalkSpeed = 1.5f;

        [Tooltip("Maximum walk speed at high voice volume.")]
        [SerializeField] private float maxWalkSpeed = 4f;

        [Tooltip("Minimum run speed at low voice volume.")]
        [SerializeField] private float minRunSpeed = 5f;

        [Tooltip("Maximum run speed at high voice volume.")]
        [SerializeField] private float maxRunSpeed = 9f;

        [Tooltip("How quickly the character accelerates / decelerates. " +
            "Higher = snappier response.")]
        [Range(1f, 30f)]
        [SerializeField] private float acceleration = 12f;

        [Tooltip("Deceleration multiplier relative to acceleration. " +
            "Higher = character stops faster when voice drops.")]
        [Range(1f, 5f)]
        [SerializeField] private float decelerationMultiplier = 3f;

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
        private IVoiceDetectionProvider _provider;

        private float _currentSpeed;
        private float _targetYRotation;
        private float _verticalVelocity;

        // ====================================================================
        // Public Read-Only Properties (for UI / Animation)
        // ====================================================================

        /// <summary>Current actual movement speed.</summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>Target speed based on voice state (before smoothing).</summary>
        public float TargetSpeed { get; private set; }

        /// <summary>Whether the character is currently moving.</summary>
        public bool IsMoving => _currentSpeed > 0.01f;

        /// <summary>Current movement state from the voice provider.</summary>
        public VoiceMovementState MovementState =>
            _provider != null ? _provider.CurrentState : VoiceMovementState.Idle;

        /// <summary>The active voice detection provider (for debug UI).</summary>
        public IVoiceDetectionProvider ActiveProvider => _provider;

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

            // Find the voice detection provider
            ResolveProvider();

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
        /// Sets forward speed based on voice state AND loudness.
        /// Louder voice = faster movement within the Walk/Run speed range.
        /// </summary>
        private void HandleVoiceMovement()
        {
            if (_provider == null || !_provider.IsDeviceReady)
            {
                TargetSpeed = 0f;
                _currentSpeed = Mathf.Lerp(_currentSpeed, 0f,
                    Time.deltaTime * acceleration * decelerationMultiplier);
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // HYBRID SYSTEM:
            //   AI (TeachableMachineProvider) → decides STATE (Idle/Walk/Run)
            //   Volume (MicrophoneInput)      → decides SPEED (quiet=slow, loud=fast)
            // ═══════════════════════════════════════════════════════════════

            // Volume: scale speed within the state's range
            float loudness = Mathf.Clamp01(_provider.Loudness);

            // AI State: determines which speed range to use
            switch (_provider.CurrentState)
            {
                case VoiceMovementState.Walk:
                    // AI says "Walk จิ๊ดดดด" → quiet voice = slow walk, loud voice = fast walk
                    TargetSpeed = Mathf.Lerp(minWalkSpeed, maxWalkSpeed, loudness);
                    break;

                case VoiceMovementState.Run:
                    // AI says "Run จิ๊ด จิ๊ด จิ๊ด" → quiet bursts = slow run, loud bursts = fast run
                    TargetSpeed = Mathf.Lerp(minRunSpeed, maxRunSpeed, loudness);
                    break;

                case VoiceMovementState.Idle:
                default:
                    TargetSpeed = 0f;
                    break;
            }

            // Accelerate faster when speeding up, decelerate faster when slowing
            bool isDecelerating = TargetSpeed < _currentSpeed;
            float rate = isDecelerating
                ? acceleration * decelerationMultiplier
                : acceleration;

            _currentSpeed = Mathf.Lerp(
                _currentSpeed,
                TargetSpeed,
                Time.deltaTime * rate
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
            Vector3 moveDir = transform.forward * _currentSpeed;
            moveDir.y = _verticalVelocity;
            _controller.Move(moveDir * Time.deltaTime);
        }

        // ====================================================================
        // Provider Resolution
        // ====================================================================

        /// <summary>
        /// Finds the IVoiceDetectionProvider. Checks assigned reference first,
        /// then searches the scene.
        /// </summary>
        private void ResolveProvider()
        {
            // Use assigned reference if available
            if (defaultProvider != null)
            {
                _provider = defaultProvider;
                Debug.Log("[PlayerVoiceMovement] Using assigned LoudnessDetectionProvider.");
                return;
            }

            // Search same GameObject
            _provider = GetComponent<IVoiceDetectionProvider>();
            if (_provider != null) return;

            // Search children
            _provider = GetComponentInChildren<IVoiceDetectionProvider>();
            if (_provider != null) return;

            // Search anywhere in scene — prefer AI provider over loudness
            var aiProvider = FindAnyObjectByType<TeachableMachineProvider>();
            if (aiProvider != null && aiProvider.enabled)
            {
                _provider = aiProvider;
                Debug.Log("[PlayerVoiceMovement] Found TeachableMachineProvider (AI) in scene.");
                return;
            }

            var loudnessProvider = FindAnyObjectByType<LoudnessDetectionProvider>();
            if (loudnessProvider != null)
            {
                _provider = loudnessProvider;
                Debug.Log("[PlayerVoiceMovement] Found LoudnessDetectionProvider in scene.");
                return;
            }

            Debug.LogError("[PlayerVoiceMovement] No IVoiceDetectionProvider found! " +
                "Add a TeachableMachineProvider or LoudnessDetectionProvider to the scene.");
        }

        // ====================================================================
        // Public Methods
        // ====================================================================

        /// <summary>
        /// Hot-swap the voice detection provider at runtime.
        /// Use this to switch from loudness detection to AI classification.
        /// </summary>
        public void SetInputProvider(IVoiceDetectionProvider provider)
        {
            _provider = provider;
            Debug.Log($"[PlayerVoiceMovement] Provider changed to: " +
                $"{provider?.GetType().Name ?? "null"}");
        }

        /// <summary>Unlock cursor (for menus / pause).</summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Re-lock cursor to center screen.</summary>
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
