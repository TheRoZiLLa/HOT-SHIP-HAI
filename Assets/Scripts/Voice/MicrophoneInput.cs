// ============================================================================
// MicrophoneInput.cs
// Captures real-time microphone audio and computes RMS loudness.
// Implements IMicrophoneInputProvider for hot-swappable input systems.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Captures microphone audio using Unity's Microphone API and computes
    /// a smoothed RMS loudness value in [0, 1]. Attach to any GameObject.
    /// </summary>
    public class MicrophoneInput : MonoBehaviour, IMicrophoneInputProvider
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("Microphone Settings")]
        [Tooltip("Leave empty to use the default system microphone.")]
        [SerializeField] private string preferredDeviceName = "";

        [Tooltip("Recording sample rate in Hz. 44100 is CD quality.")]
        [SerializeField] private int sampleRate = 44100;

        [Header("Loudness Processing")]
        [Tooltip("Number of samples to analyse per frame. Higher = more accurate but slower.")]
        [Range(64, 2048)]
        [SerializeField] private int sampleWindow = 512;

        [Tooltip("Minimum loudness to consider as voice input (filters background noise).")]
        [Range(0f, 0.1f)]
        [SerializeField] private float voiceThreshold = 0.01f;

        [Tooltip("Smoothing factor for loudness transitions. Lower = smoother, Higher = snappier.")]
        [Range(1f, 30f)]
        [SerializeField] private float smoothingSpeed = 12f;

        [Tooltip("Multiplier to amplify quiet microphones. Increase if your mic is too quiet.")]
        [Range(1f, 20f)]
        [SerializeField] private float sensitivity = 5f;

        // ====================================================================
        // IMicrophoneInputProvider Implementation
        // ====================================================================

        public float Loudness => _smoothedLoudness;
        public bool IsVoiceActive => _smoothedLoudness > voiceThreshold;
        public bool IsDeviceReady => _isRecording;
        public string DeviceName => _activeMicDevice ?? "None";

        // ====================================================================
        // Internal State
        // ====================================================================

        private AudioClip _micClip;
        private string _activeMicDevice;
        private bool _isRecording;
        private float _rawLoudness;
        private float _smoothedLoudness;
        private float[] _sampleBuffer;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

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
            if (!_isRecording) return;

            _rawLoudness = CalculateRmsLoudness();
            _rawLoudness = Mathf.Clamp01(_rawLoudness * sensitivity);

            // Apply threshold — below threshold means silence
            if (_rawLoudness < voiceThreshold)
            {
                _rawLoudness = 0f;
            }

            // Smooth transitions to avoid jittery movement
            _smoothedLoudness = Mathf.Lerp(
                _smoothedLoudness,
                _rawLoudness,
                Time.deltaTime * smoothingSpeed
            );
        }

        // ====================================================================
        // IMicrophoneInputProvider Methods
        // ====================================================================

        public void Initialize()
        {
            // Clean up any existing recording
            Shutdown();

            // Find available microphone
            _activeMicDevice = FindMicrophone();

            if (string.IsNullOrEmpty(_activeMicDevice))
            {
                Debug.LogWarning("[MicrophoneInput] No microphone detected! " +
                    "Voice movement will not work.");
                return;
            }

            // Allocate sample buffer
            _sampleBuffer = new float[sampleWindow];

            // Start recording — loop with a 1-second buffer for low latency
            _micClip = Microphone.Start(_activeMicDevice, true, 1, sampleRate);

            if (_micClip == null)
            {
                Debug.LogError("[MicrophoneInput] Failed to start microphone: " +
                    _activeMicDevice);
                return;
            }

            _isRecording = true;
            Debug.Log($"[MicrophoneInput] Recording started on: {_activeMicDevice} " +
                $"@ {sampleRate}Hz");
        }

        public void Shutdown()
        {
            if (_isRecording && !string.IsNullOrEmpty(_activeMicDevice))
            {
                Microphone.End(_activeMicDevice);
                Debug.Log("[MicrophoneInput] Recording stopped.");
            }

            _isRecording = false;
            _smoothedLoudness = 0f;
            _rawLoudness = 0f;

            if (_micClip != null)
            {
                Destroy(_micClip);
                _micClip = null;
            }
        }

        // ====================================================================
        // Loudness Calculation
        // ====================================================================

        /// <summary>
        /// Computes the Root Mean Square (RMS) loudness from the current
        /// microphone position in the circular recording buffer.
        /// </summary>
        private float CalculateRmsLoudness()
        {
            if (_micClip == null) return 0f;

            int micPosition = Microphone.GetPosition(_activeMicDevice);

            // Not enough data yet
            if (micPosition < sampleWindow) return 0f;

            // Read samples from the clip at the current recording head
            int startPosition = micPosition - sampleWindow;
            if (startPosition < 0) startPosition = 0;

            _micClip.GetData(_sampleBuffer, startPosition);

            // Calculate RMS
            float sum = 0f;
            for (int i = 0; i < _sampleBuffer.Length; i++)
            {
                sum += _sampleBuffer[i] * _sampleBuffer[i];
            }

            return Mathf.Sqrt(sum / _sampleBuffer.Length);
        }

        // ====================================================================
        // Device Selection
        // ====================================================================

        /// <summary>
        /// Finds the best available microphone device.
        /// Prefers the user-specified device, falls back to the first available.
        /// </summary>
        private string FindMicrophone()
        {
            string[] devices = Microphone.devices;

            if (devices.Length == 0)
            {
                return null;
            }

            // Log all available devices for debugging
            Debug.Log($"[MicrophoneInput] Found {devices.Length} microphone(s):");
            for (int i = 0; i < devices.Length; i++)
            {
                Debug.Log($"  [{i}] {devices[i]}");
            }

            // Try to match preferred device
            if (!string.IsNullOrEmpty(preferredDeviceName))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].Contains(preferredDeviceName))
                    {
                        Debug.Log($"[MicrophoneInput] Using preferred device: {devices[i]}");
                        return devices[i];
                    }
                }

                Debug.LogWarning($"[MicrophoneInput] Preferred device '{preferredDeviceName}' " +
                    $"not found. Falling back to default.");
            }

            // Use first available (system default)
            Debug.Log($"[MicrophoneInput] Using default device: {devices[0]}");
            return devices[0];
        }

        // ====================================================================
        // Public Getters (for UI / Debug)
        // ====================================================================

        /// <summary>Returns the raw (unsmoothed) loudness for debug display.</summary>
        public float RawLoudness => _rawLoudness;

        /// <summary>Returns the configured voice threshold.</summary>
        public float VoiceThreshold => voiceThreshold;
    }
}
