// ============================================================================
// LoudnessDetectionProvider.cs
// Default IVoiceDetectionProvider that wires the full audio pipeline:
//   MicrophoneInput → NoiseFilter → VoicePatternAnalyzer
// Swap this component with FutureAIModelProvider (or your own) to change
// the detection backend without touching the movement system.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Default voice detection provider using real-time microphone analysis.
    /// Orchestrates the audio processing pipeline each frame:
    /// <list type="number">
    ///   <item>MicrophoneInput captures raw audio → RMS loudness</item>
    ///   <item>NoiseFilter applies threshold gating + smoothing + debounce</item>
    ///   <item>VoicePatternAnalyzer detects Walk / Run / Idle patterns</item>
    /// </list>
    /// </summary>
    public class LoudnessDetectionProvider : MonoBehaviour, IVoiceDetectionProvider
    {
        // ====================================================================
        // Inspector References
        // ====================================================================

        [Header("Pipeline Components")]
        [Tooltip("MicrophoneInput that captures raw audio. Auto-resolved if empty.")]
        [SerializeField] private MicrophoneInput micInput;

        [Tooltip("NoiseFilter that cleans the raw signal. Auto-resolved if empty.")]
        [SerializeField] private NoiseFilter noiseFilter;

        [Tooltip("VoicePatternAnalyzer that detects Walk/Run/Idle. Auto-resolved if empty.")]
        [SerializeField] private VoicePatternAnalyzer patternAnalyzer;

        // ====================================================================
        // IVoiceDetectionProvider — Primary Output
        // ====================================================================

        public VoiceMovementState CurrentState =>
            patternAnalyzer != null ? patternAnalyzer.CurrentState : VoiceMovementState.Idle;

        public float Confidence =>
            patternAnalyzer != null ? patternAnalyzer.Confidence : 0f;

        // ====================================================================
        // IVoiceDetectionProvider — Diagnostic Data
        // ====================================================================

        public float Loudness =>
            noiseFilter != null ? noiseFilter.FilteredLoudness : 0f;

        public bool IsVoiceActive =>
            noiseFilter != null && noiseFilter.IsAboveThreshold;

        public float SustainDuration =>
            patternAnalyzer != null ? patternAnalyzer.SustainDuration : 0f;

        public int PeakCount =>
            patternAnalyzer != null ? patternAnalyzer.PeakCount : 0;

        // ====================================================================
        // IVoiceDetectionProvider — Device Status
        // ====================================================================

        public bool IsDeviceReady =>
            micInput != null && micInput.IsRecording;

        public string DeviceName =>
            micInput != null ? micInput.ActiveDeviceName : "None";

        // ====================================================================
        // Additional Properties (for debug UI)
        // ====================================================================

        /// <summary>Raw loudness before noise filtering.</summary>
        public float RawLoudness =>
            micInput != null ? micInput.RawLoudness : 0f;

        /// <summary>Whether noise was actively filtered this frame.</summary>
        public bool IsNoiseFiltered =>
            noiseFilter != null && noiseFilter.IsNoiseFiltered;

        /// <summary>The noise floor threshold from the filter.</summary>
        public float NoiseFloor =>
            noiseFilter != null ? noiseFilter.NoiseFloor : 0f;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Update()
        {
            if (micInput == null || noiseFilter == null || patternAnalyzer == null)
                return;

            // ── Pipeline: MicInput → NoiseFilter → PatternAnalyzer ──────────
            // Step 1: MicrophoneInput.Update() already ran (script execution order)
            //         and computed RawLoudness.

            // Step 2: Feed raw loudness through the noise filter
            noiseFilter.ProcessLoudness(micInput.RawLoudness);

            // Step 3: Feed filtered data into the pattern analyzer
            patternAnalyzer.Analyze(
                noiseFilter.FilteredLoudness,
                noiseFilter.IsAboveThreshold
            );
        }

        // ====================================================================
        // IVoiceDetectionProvider — Lifecycle
        // ====================================================================

        public void Initialize()
        {
            ResolveDependencies();

            if (micInput != null && !micInput.IsRecording)
            {
                micInput.StartRecording();
            }

            if (noiseFilter != null)
            {
                noiseFilter.Reset();
            }

            if (patternAnalyzer != null)
            {
                patternAnalyzer.ResetState();
            }

            Debug.Log("[LoudnessDetectionProvider] Initialized pipeline: " +
                $"Mic={micInput != null}, Filter={noiseFilter != null}, " +
                $"Analyzer={patternAnalyzer != null}");
        }

        public void Shutdown()
        {
            if (micInput != null)
            {
                micInput.StopRecording();
            }

            Debug.Log("[LoudnessDetectionProvider] Shutdown.");
        }

        // ====================================================================
        // Dependency Resolution
        // ====================================================================

        /// <summary>
        /// Auto-finds pipeline components if not assigned in Inspector.
        /// Searches: this GameObject → children → scene.
        /// </summary>
        private void ResolveDependencies()
        {
            if (micInput == null)
            {
                micInput = GetComponent<MicrophoneInput>();
                if (micInput == null)
                    micInput = GetComponentInChildren<MicrophoneInput>();
                if (micInput == null)
                    micInput = FindAnyObjectByType<MicrophoneInput>();
            }

            if (noiseFilter == null)
            {
                noiseFilter = GetComponent<NoiseFilter>();
                if (noiseFilter == null)
                    noiseFilter = GetComponentInChildren<NoiseFilter>();
                if (noiseFilter == null)
                    noiseFilter = FindAnyObjectByType<NoiseFilter>();
            }

            if (patternAnalyzer == null)
            {
                patternAnalyzer = GetComponent<VoicePatternAnalyzer>();
                if (patternAnalyzer == null)
                    patternAnalyzer = GetComponentInChildren<VoicePatternAnalyzer>();
                if (patternAnalyzer == null)
                    patternAnalyzer = FindAnyObjectByType<VoicePatternAnalyzer>();
            }

            // Log warnings for missing components
            if (micInput == null)
                Debug.LogError("[LoudnessDetectionProvider] MicrophoneInput not found!");
            if (noiseFilter == null)
                Debug.LogError("[LoudnessDetectionProvider] NoiseFilter not found!");
            if (patternAnalyzer == null)
                Debug.LogError("[LoudnessDetectionProvider] VoicePatternAnalyzer not found!");
        }
    }
}
