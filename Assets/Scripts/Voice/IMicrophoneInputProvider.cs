// ============================================================================
// IMicrophoneInputProvider.cs
// Interface for microphone input providers — enables swapping loudness
// detection with Teachable Machine, AI voice triggers, or any custom system.
// Compatible with Unity 6000.0.72f1
// ============================================================================

namespace HotShipHai.Voice
{
    /// <summary>
    /// Abstraction layer for microphone / sound input.
    /// Implement this interface to create custom input providers
    /// (e.g., Teachable Machine, Whisper, sound classification).
    /// </summary>
    public interface IMicrophoneInputProvider
    {
        /// <summary>
        /// Normalized loudness value in the range [0, 1].
        /// 0 = silence, 1 = maximum loudness.
        /// </summary>
        float Loudness { get; }

        /// <summary>
        /// Whether the provider detects active voice / sound input
        /// above the configured threshold.
        /// </summary>
        bool IsVoiceActive { get; }

        /// <summary>
        /// Whether the input device is available and recording.
        /// </summary>
        bool IsDeviceReady { get; }

        /// <summary>
        /// Human-readable name of the active input device.
        /// </summary>
        string DeviceName { get; }

        /// <summary>
        /// Initialize or re-initialize the provider.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Clean up resources.
        /// </summary>
        void Shutdown();
    }
}
