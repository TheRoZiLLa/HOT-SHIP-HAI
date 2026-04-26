// ============================================================================
// VoiceMovementState.cs
// Enum representing the detected voice-driven movement intent.
// Compatible with Unity 6000.0.72f1
// ============================================================================

namespace HotShipHai.Voice
{
    /// <summary>
    /// Movement states derived from voice pattern analysis.
    /// Used by IVoiceDetectionProvider implementations and consumed by
    /// PlayerVoiceMovement to drive CharacterController speed.
    /// </summary>
    public enum VoiceMovementState
    {
        /// <summary>No valid voice input — character stands still.</summary>
        Idle = 0,

        /// <summary>Sustained continuous voice detected — character walks forward.</summary>
        Walk = 1,

        /// <summary>Repeated rapid voice bursts detected — character runs forward.</summary>
        Run = 2
    }
}
