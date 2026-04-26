// ============================================================================
// VoicePatternAnalyzer.cs
// Detects voice patterns from filtered microphone data:
//   - Any sound above threshold → at minimum Walk
//   - Sustained voice → Walk (speed scales with loudness)
//   - Rapid bursts / volume fluctuation → Run
//   - Silence → Idle
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Analyses filtered loudness data to determine the player's movement intent.
    /// <para>
    /// <b>Walk</b> = any sustained voice input above threshold.
    /// </para>
    /// <para>
    /// <b>Run</b> = rapid volume fluctuations OR repeated bursts with gaps.
    /// Detects BOTH: (1) peaks during continuous sound via rising-edge detection,
    /// and (2) traditional burst gaps via falling-edge detection.
    /// </para>
    /// <para>
    /// <b>Idle</b> = silence or noise below threshold.
    /// </para>
    /// </summary>
    public class VoicePatternAnalyzer : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings — Sustained Voice (Walk)
        // ====================================================================

        [Header("Walk Detection — Sustained Voice")]
        [Tooltip("Minimum continuous sound duration (seconds) before Walk is triggered.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float sustainTimeForWalk = 0.12f;

        // ====================================================================
        // Inspector Settings — Burst / Fluctuation Pattern (Run)
        // ====================================================================

        [Header("Run Detection — Rapid Bursts / Fluctuations")]
        [Tooltip("Number of distinct peaks required to trigger Run.")]
        [Range(2, 8)]
        [SerializeField] private int burstCountForRun = 3;

        [Tooltip("Time window (seconds) in which peaks are counted.")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float burstWindowTime = 2.0f;

        [Tooltip("Minimum loudness level that qualifies as a peak.")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float peakThreshold = 0.08f;

        [Tooltip("Minimum time (seconds) between peaks to avoid double-counting.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float peakCooldown = 0.1f;

        [Tooltip("Minimum loudness DROP from a local peak to register as a valley. " +
            "Used for rising-edge detection during continuous sound.")]
        [Range(0.01f, 0.15f)]
        [SerializeField] private float valleyDepth = 0.03f;

        // ====================================================================
        // Inspector Settings — Idle Timeout
        // ====================================================================

        [Header("Idle Transition")]
        [Tooltip("How long (seconds) after voice stops before returning to Idle.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float idleTimeout = 0.35f;

        // ====================================================================
        // Public Read-Only Properties
        // ====================================================================

        /// <summary>The current detected movement state.</summary>
        public VoiceMovementState CurrentState => _currentState;

        /// <summary>How long (seconds) voice has been continuously sustained.</summary>
        public float SustainDuration => _sustainDuration;

        /// <summary>Number of peaks detected within the burst window.</summary>
        public int PeakCount => _activePeakCount;

        /// <summary>Confidence level of the current detection [0, 1].</summary>
        public float Confidence => _confidence;

        // ====================================================================
        // Internal State
        // ====================================================================

        private VoiceMovementState _currentState = VoiceMovementState.Idle;
        private float _confidence;

        // Sustain tracking
        private float _sustainDuration;
        private bool _wasSoundActive;

        // Peak tracking — TWO methods combined:
        // Method 1: Rising-edge detection (peaks DURING continuous sound)
        // Method 2: Falling-edge detection (peaks when sound drops to silence)
        private float[] _peakTimestamps;
        private int _peakWriteIndex;
        private int _activePeakCount;
        private float _lastPeakTime;

        // Rising-edge detection state
        private float _localMax;           // Local maximum loudness
        private float _localMin;           // Local minimum after a peak
        private bool _wasRising;           // Was loudness rising last frame?
        private bool _waitingForValley;    // Have we seen a peak, waiting for valley?

        // Falling-edge detection state (traditional burst gaps)
        private bool _isInBurst;
        private float _burstStartTime;
        private float _burstMaxLoudness;

        // Idle timeout
        private float _timeSinceLastSound;
        private VoiceMovementState _latentState = VoiceMovementState.Idle;

        // ====================================================================
        // Constants
        // ====================================================================

        private const int MAX_PEAKS = 32;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

        private void Awake()
        {
            _peakTimestamps = new float[MAX_PEAKS];
        }

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// Call every frame with the filtered loudness and threshold status
        /// from <see cref="NoiseFilter"/>.
        /// </summary>
        public void Analyze(float filteredLoudness, bool isAboveThreshold)
        {
            float time = Time.time;

            if (isAboveThreshold)
            {
                _timeSinceLastSound = 0f;

                if (!_wasSoundActive)
                {
                    // ── Sound just started ──────────────────────────────────
                    _sustainDuration = 0f;
                    _burstStartTime = time;
                    _burstMaxLoudness = filteredLoudness;
                    _isInBurst = true;

                    // Reset rising-edge state for new sound segment
                    _localMax = filteredLoudness;
                    _localMin = filteredLoudness;
                    _wasRising = true;
                    _waitingForValley = false;
                }

                _sustainDuration += Time.deltaTime;

                // ── Rising-edge peak detection (during continuous sound) ────
                // Detect peaks by finding local maxima followed by valleys.
                // This catches rapid fluctuations like "จิ๊ด จิ๊ด จิ๊ด" even
                // when the sound never fully drops to silence.
                DetectRisingEdgePeaks(filteredLoudness, time);

                // Track burst max for falling-edge detection
                _burstMaxLoudness = Mathf.Max(_burstMaxLoudness, filteredLoudness);
            }
            else
            {
                _timeSinceLastSound += Time.deltaTime;

                // ── Falling-edge peak detection (sound dropped to silence) ──
                // Traditional burst detection: sound was above threshold,
                // now it dropped. Register as a peak if loud enough.
                if (_wasSoundActive && _isInBurst)
                {
                    if (_burstMaxLoudness >= peakThreshold &&
                        (time - _lastPeakTime) >= peakCooldown)
                    {
                        RegisterPeak(time);
                    }
                }

                _isInBurst = false;

                // Decay sustain after brief silence
                if (_timeSinceLastSound > 0.1f)
                {
                    _sustainDuration = 0f;
                    _localMax = 0f;
                    _localMin = 0f;
                    _waitingForValley = false;
                }
            }

            _wasSoundActive = isAboveThreshold;

            // ── Count active peaks in window ────────────────────────────────
            _activePeakCount = CountPeaksInWindow(time);

            // ── Determine state ─────────────────────────────────────────────
            DetermineState(filteredLoudness, isAboveThreshold);
        }

        /// <summary>
        /// Reset all analysis state.
        /// </summary>
        public void ResetState()
        {
            _currentState = VoiceMovementState.Idle;
            _confidence = 0f;
            _sustainDuration = 0f;
            _activePeakCount = 0;
            _wasSoundActive = false;
            _isInBurst = false;
            _lastPeakTime = 0f;
            _timeSinceLastSound = 0f;
            _latentState = VoiceMovementState.Idle;
            _peakWriteIndex = 0;
            _localMax = 0f;
            _localMin = 0f;
            _wasRising = false;
            _waitingForValley = false;

            if (_peakTimestamps != null)
            {
                for (int i = 0; i < _peakTimestamps.Length; i++)
                    _peakTimestamps[i] = 0f;
            }
        }

        // ====================================================================
        // Rising-Edge Peak Detection
        // ====================================================================

        /// <summary>
        /// Detects peaks DURING continuous sound by tracking local maxima and
        /// valleys. A peak is registered when loudness rises to a local max,
        /// then drops by at least <see cref="valleyDepth"/>.
        /// This is what catches rapid "จิ๊ด จิ๊ด จิ๊ด" even when the signal
        /// never drops below the noise gate.
        /// </summary>
        private void DetectRisingEdgePeaks(float loudness, float time)
        {
            bool isRising = loudness > _localMax;

            if (isRising)
            {
                // Loudness is climbing — update the local max
                _localMax = loudness;

                // If we were waiting for a valley after a previous peak,
                // and loudness rose again, check if the dip was deep enough
                if (_waitingForValley)
                {
                    float dip = _localMax - _localMin;
                    if (dip >= valleyDepth && _localMin < _localMax * 0.85f)
                    {
                        // We had a real valley — the previous peak was valid
                        // Now we're rising to a NEW peak
                        if ((time - _lastPeakTime) >= peakCooldown &&
                            _localMax >= peakThreshold)
                        {
                            RegisterPeak(time);
                        }
                        _waitingForValley = false;
                    }
                }
            }
            else
            {
                // Loudness is falling or stable
                if (_wasRising && loudness < _localMax && _localMax >= peakThreshold)
                {
                    // Just passed a local maximum — start waiting for valley
                    _waitingForValley = true;
                    _localMin = loudness;
                }

                if (_waitingForValley)
                {
                    _localMin = Mathf.Min(_localMin, loudness);
                }
            }

            _wasRising = isRising;
        }

        // ====================================================================
        // State Determination
        // ====================================================================

        /// <summary>
        /// Core decision logic. Priority: Run > Walk > Idle.
        /// Walk is the DEFAULT for any sound above threshold.
        /// Run requires enough peaks (frequent fluctuations or bursts).
        /// </summary>
        private void DetermineState(float loudness, bool isActive)
        {
            VoiceMovementState newState = VoiceMovementState.Idle;
            float newConfidence = 0f;

            // ── Check for RUN (burst pattern or rapid fluctuations) ─────────
            if (_activePeakCount >= burstCountForRun)
            {
                newState = VoiceMovementState.Run;
                newConfidence = Mathf.Clamp01(
                    (float)_activePeakCount / (burstCountForRun + 2)
                );
            }
            // ── Check for WALK (any sustained voice above threshold) ────────
            // Much simpler now: if sound is active for sustainTimeForWalk, it's Walk.
            // No stability check — the noise filter already handled noise rejection.
            else if (isActive && _sustainDuration >= sustainTimeForWalk)
            {
                newState = VoiceMovementState.Walk;
                newConfidence = Mathf.Clamp01(
                    _sustainDuration / (sustainTimeForWalk * 2f)
                );
            }

            // ── Apply idle timeout ──────────────────────────────────────────
            if (newState != VoiceMovementState.Idle)
            {
                _latentState = newState;
                _currentState = newState;
                _confidence = newConfidence;
            }
            else if (_timeSinceLastSound < idleTimeout &&
                     _latentState != VoiceMovementState.Idle)
            {
                // Hold the last active state briefly to prevent flickering
                _currentState = _latentState;
                _confidence = Mathf.Lerp(_confidence, 0f, Time.deltaTime * 4f);
            }
            else
            {
                _currentState = VoiceMovementState.Idle;
                _latentState = VoiceMovementState.Idle;
                _confidence = 0f;
            }
        }

        // ====================================================================
        // Peak Tracking
        // ====================================================================

        private void RegisterPeak(float time)
        {
            _peakTimestamps[_peakWriteIndex] = time;
            _peakWriteIndex = (_peakWriteIndex + 1) % MAX_PEAKS;
            _lastPeakTime = time;
        }

        private int CountPeaksInWindow(float currentTime)
        {
            float windowStart = currentTime - burstWindowTime;
            int count = 0;

            for (int i = 0; i < MAX_PEAKS; i++)
            {
                if (_peakTimestamps[i] > windowStart && _peakTimestamps[i] <= currentTime)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
