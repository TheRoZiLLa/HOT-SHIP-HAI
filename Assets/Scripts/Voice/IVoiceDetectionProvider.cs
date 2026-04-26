// ============================================================================
// IVoiceDetectionProvider.cs
// Abstraction layer for voice-driven movement detection.
// Implement this interface to create providers backed by:
//   - Microphone loudness + pattern analysis (default)
//   - Teachable Machine models
//   - ONNX runtime inference
//   - Custom voice recognition / classification
// Compatible with Unity 6000.0.72f1
// ============================================================================

namespace HotShipHai.Voice
{
    /// <summary>
    /// Contract for any voice detection system that drives player movement.
    /// The movement controller consumes <see cref="CurrentState"/> to decide
    /// whether the character should idle, walk, or run.
    /// </summary>
    public interface IVoiceDetectionProvider
    {
        // ====================================================================
        // Primary Output — consumed by PlayerVoiceMovement
        // ====================================================================

        /// <summary>
        /// The current detected movement intent: Idle, Walk, or Run.
        /// This is the ONLY value the movement system needs to read.
        /// </summary>
        VoiceMovementState CurrentState { get; }

        /// <summary>
        /// Confidence level of the current detection in [0, 1].
        /// 0 = no confidence (guessing), 1 = fully confident.
        /// Useful for blending or requiring minimum confidence before acting.
        /// </summary>
        float Confidence { get; }

        // ====================================================================
        // Diagnostic Data — consumed by Debug UI
        // ====================================================================

        /// <summary>
        /// Normalized loudness value in [0, 1].
        /// 0 = silence, 1 = maximum loudness.
        /// </summary>
        float Loudness { get; }

        /// <summary>
        /// Whether the provider detects any active sound above threshold.
        /// </summary>
        bool IsVoiceActive { get; }

        /// <summary>
        /// How long (in seconds) the current voice input has been sustained
        /// without dropping below threshold.
        /// </summary>
        float SustainDuration { get; }

        /// <summary>
        /// Number of detected loudness peaks (bursts) within the analysis window.
        /// </summary>
        int PeakCount { get; }

        // ====================================================================
        // Device Status
        // ====================================================================

        /// <summary>
        /// Whether the input device is available and actively recording.
        /// </summary>
        bool IsDeviceReady { get; }

        /// <summary>
        /// Human-readable name of the active input device.
        /// </summary>
        string DeviceName { get; }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        /// <summary>
        /// Initialize or re-initialize the provider.
        /// Called once at startup and can be called again to reset.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Release resources (microphone, model, buffers).
        /// </summary>
        void Shutdown();
    }
}
