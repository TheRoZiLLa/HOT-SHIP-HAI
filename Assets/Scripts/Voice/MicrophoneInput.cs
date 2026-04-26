// ============================================================================
// MicrophoneInput.cs
// Pure audio capture from the system microphone via Unity's Microphone API.
// Computes RMS loudness and exposes raw sample data for downstream consumers.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;

namespace HotShipHai.Voice
{
    /// <summary>
    /// Captures microphone audio and computes raw RMS loudness.
    /// This is a pure capture component — all filtering, pattern analysis,
    /// and state detection live in separate classes downstream.
    /// </summary>
    public class MicrophoneInput : MonoBehaviour
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

        [Tooltip("Multiplier to amplify quiet microphones. Increase if your mic is too quiet.")]
        [Range(1f, 20f)]
        [SerializeField] private float sensitivity = 5f;

        // ====================================================================
        // Public Read-Only Properties
        // ====================================================================

        /// <summary>Raw RMS loudness in [0, 1] (after sensitivity, before filtering).</summary>
        public float RawLoudness => _rawLoudness;

        /// <summary>Whether the microphone is currently recording.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>Name of the active microphone device.</summary>
        public string ActiveDeviceName => _activeMicDevice ?? "None";

        /// <summary>The configured sample rate.</summary>
        public int SampleRate => sampleRate;

        // ====================================================================
        // Internal State
        // ====================================================================

        private AudioClip _micClip;
        private string _activeMicDevice;
        private bool _isRecording;
        private float _rawLoudness;
        private float[] _sampleBuffer;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

        private void Start()
        {
            StartRecording();
        }

        private void OnDestroy()
        {
            StopRecording();
        }

        private void Update()
        {
            if (!_isRecording) return;
            _rawLoudness = Mathf.Clamp01(CalculateRmsLoudness() * sensitivity);
        }

        // ====================================================================
        // Recording Control
        // ====================================================================

        /// <summary>
        /// Begin capturing audio from the system microphone.
        /// Safe to call multiple times — stops existing recording first.
        /// </summary>
        public void StartRecording()
        {
            StopRecording();

            _activeMicDevice = FindMicrophone();

            if (string.IsNullOrEmpty(_activeMicDevice))
            {
                Debug.LogWarning("[MicrophoneInput] No microphone detected! " +
                    "Voice movement will not work.");
                return;
            }

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

        /// <summary>
        /// Stop capturing audio and release the microphone.
        /// </summary>
        public void StopRecording()
        {
            if (_isRecording && !string.IsNullOrEmpty(_activeMicDevice))
            {
                Microphone.End(_activeMicDevice);
                Debug.Log("[MicrophoneInput] Recording stopped.");
            }

            _isRecording = false;
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
    }
}
