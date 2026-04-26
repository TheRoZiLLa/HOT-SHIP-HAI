// ============================================================================
// NoiseFilter.cs
// Filters raw microphone loudness to eliminate background noise.
// Applies threshold gating, exponential smoothing, and debounce.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Processes raw microphone loudness to produce a clean, stable signal.
    /// Eliminates false triggers from fans, keyboards, breathing, and room noise.
    /// Feed the output into <see cref="VoicePatternAnalyzer"/>.
    /// </summary>
    public class NoiseFilter : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("Threshold Gate")]
        [Tooltip("Minimum raw loudness to pass through the filter. " +
            "Anything below this is treated as silence. " +
            "Increase if background noise triggers movement.")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float noiseFloor = 0.015f;

        [Header("Smoothing")]
        [Tooltip("Smoothing speed for loudness transitions. " +
            "Lower = smoother (less jitter), Higher = more responsive.")]
        [Range(1f, 30f)]
        [SerializeField] private float smoothingSpeed = 25f;

        [Header("Debounce")]
        [Tooltip("Minimum time (seconds) that loudness must stay above threshold " +
            "before it counts as intentional voice input. Prevents brief noise spikes.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float debounceTime = 0.02f;

        // ====================================================================
        // Public Read-Only Properties
        // ====================================================================

        /// <summary>Filtered and smoothed loudness in [0, 1].</summary>
        public float FilteredLoudness => _filteredLoudness;

        /// <summary>Whether the filtered signal is above threshold (after debounce).</summary>
        public bool IsAboveThreshold => _isAboveThreshold;

        /// <summary>Whether noise was actively filtered this frame (for debug display).</summary>
        public bool IsNoiseFiltered => _isNoiseFiltered;

        /// <summary>The configured noise floor value.</summary>
        public float NoiseFloor => noiseFloor;

        // ====================================================================
        // Internal State
        // ====================================================================

        private float _filteredLoudness;
        private bool _isAboveThreshold;
        private bool _isNoiseFiltered;

        // Debounce tracking
        private float _timeAboveThreshold;
        private bool _rawAboveThreshold;

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// Call this every frame with the raw loudness from MicrophoneInput.
        /// Processes the value through threshold gate → smoothing → debounce.
        /// </summary>
        /// <param name="rawLoudness">Raw RMS loudness from MicrophoneInput [0, 1].</param>
        public void ProcessLoudness(float rawLoudness)
        {
            // ── Step 1: Threshold Gate ──────────────────────────────────────
            // Any signal below the noise floor is clamped to zero.
            float gatedLoudness;

            if (rawLoudness < noiseFloor)
            {
                gatedLoudness = 0f;
                _isNoiseFiltered = rawLoudness > 0.001f; // Was there noise we filtered?
                _rawAboveThreshold = false;
                _timeAboveThreshold = 0f;
            }
            else
            {
                // Re-map so the output starts from 0 right at the noise floor
                gatedLoudness = (rawLoudness - noiseFloor) / (1f - noiseFloor);
                gatedLoudness = Mathf.Clamp01(gatedLoudness);
                _isNoiseFiltered = false;
                _rawAboveThreshold = true;
            }

            // ── Step 2: Exponential Smoothing ───────────────────────────────
            // Prevents jittery output from fluctuating microphone signal.
            _filteredLoudness = Mathf.Lerp(
                _filteredLoudness,
                gatedLoudness,
                Time.deltaTime * smoothingSpeed
            );

            // Kill tiny residual to get a clean zero
            if (_filteredLoudness < 0.001f)
            {
                _filteredLoudness = 0f;
            }

            // ── Step 3: Debounce ────────────────────────────────────────────
            // Signal must be above threshold for debounceTime before counting.
            if (_rawAboveThreshold)
            {
                _timeAboveThreshold += Time.deltaTime;
                _isAboveThreshold = _timeAboveThreshold >= debounceTime;
            }
            else
            {
                _timeAboveThreshold = 0f;
                _isAboveThreshold = false;
            }
        }

        /// <summary>
        /// Reset all internal state. Call when re-initializing the pipeline.
        /// </summary>
        public void Reset()
        {
            _filteredLoudness = 0f;
            _isAboveThreshold = false;
            _isNoiseFiltered = false;
            _timeAboveThreshold = 0f;
            _rawAboveThreshold = false;
        }
    }
}
