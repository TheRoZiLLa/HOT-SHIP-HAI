// ============================================================================
// TeachableMachineProvider.cs
// AI-backed voice detection using a Teachable Machine TFLite sound classifier.
// Runs REAL TFLite inference: Mic PCM → Mel Spectrogram → TFLite → State.
//
// Architecture:
//   MicrophoneInput → AudioSpectrogram → TFLite Interpreter → VoiceMovementState
//   MicrophoneInput → NoiseFilter → Loudness → Speed scaling
//
// The AI handles WHAT state (Walk/Run/Idle).
// The volume system handles HOW FAST within that state.
//
// Requires: com.github.asus4.tflite UPM package
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using TensorFlowLite;

namespace HotShipHai.Voice
{
    /// <summary>
    /// AI-powered voice detection provider using a Teachable Machine TFLite model.
    /// <para>
    /// This provider captures live audio from <see cref="MicrophoneInput"/>,
    /// computes mel spectrograms via <see cref="AudioSpectrogram"/>, and feeds
    /// them into a TFLite interpreter for classification as Background/Walk/Run.
    /// </para>
    /// <para>
    /// <b>Setup:</b>
    /// <list type="number">
    ///   <item>Install com.github.asus4.tflite via UPM</item>
    ///   <item>Place your .tflite model + labels.txt in Assets/AI/V1</item>
    ///   <item>Add this component + MicrophoneInput + NoiseFilter to a GameObject</item>
    ///   <item>Assign to PlayerVoiceMovement or call SetInputProvider()</item>
    /// </list>
    /// </para>
    /// </summary>
    public class TeachableMachineProvider : MonoBehaviour, IVoiceDetectionProvider
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("AI Model Configuration")]
        [Tooltip("Labels file with one class per line: '0 Background Noise', '1 RUN', '2 Walk'")]
        [SerializeField] private TextAsset labelsFile;

        [Tooltip("The TFLite model file. Assign from Assets/AI/V1/.")]
        [SerializeField] private string tfliteModelFileName = "soundclassifier_with_metadata.tflite";

        [Tooltip("Subfolder inside StreamingAssets or Assets where the model lives.")]
        [SerializeField] private string modelSubFolder = "AI/V1";

        [Header("Classification Settings")]
        [Tooltip("Minimum confidence to accept a classification. " +
            "Below this, the result is treated as Background/Idle.")]
        [Range(0.3f, 0.99f)]
        [SerializeField] private float minimumConfidence = 0.6f;

        [Tooltip("How often to run classification (seconds). " +
            "Lower = more responsive but more CPU.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float classificationInterval = 0.1f;

        [Tooltip("Number of classification results to average for stability. " +
            "Higher = more stable but slower to change state.")]
        [Range(1, 10)]
        [SerializeField] private int smoothingWindowSize = 3;

        [Header("Spectrogram Settings")]
        [Tooltip("FFT window size. Must be power of 2. Standard for Teachable Machine.")]
        [SerializeField] private int fftSize = 1024;

        [Tooltip("Hop length between FFT windows (in samples).")]
        [SerializeField] private int hopLength = 512;

        [Tooltip("Number of mel frequency bins. Will be auto-detected from model if possible.")]
        [SerializeField] private int melBins = 232;

        [Tooltip("Number of time frames in the spectrogram. Will be auto-detected from model if possible.")]
        [SerializeField] private int timeFrames = 43;

        [Tooltip("Audio sample rate for spectrogram computation.")]
        [SerializeField] private int sampleRate = 44100;

        [Header("Pipeline References")]
        [SerializeField] private MicrophoneInput micInput;
        [SerializeField] private NoiseFilter noiseFilter;

        [Header("Idle Transition")]
        [Tooltip("How long after AI detects background before returning to Idle.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float idleTimeout = 0.15f;

        // ====================================================================
        // IVoiceDetectionProvider Implementation
        // ====================================================================

        public VoiceMovementState CurrentState => _currentState;
        public float Confidence => _confidence;
        public float Loudness => noiseFilter != null ? noiseFilter.FilteredLoudness : 0f;
        public bool IsVoiceActive => noiseFilter != null && noiseFilter.IsAboveThreshold;
        public float SustainDuration => _sustainDuration;
        public int PeakCount => 0; // AI doesn't use peak counting
        public bool IsDeviceReady => micInput != null && micInput.IsRecording;
        public string DeviceName => micInput != null ? micInput.ActiveDeviceName : "AI Model";

        // ====================================================================
        // Additional Properties (for debug UI)
        // ====================================================================

        /// <summary>Raw loudness before filtering.</summary>
        public float RawLoudness => micInput != null ? micInput.RawLoudness : 0f;

        /// <summary>Whether noise was filtered this frame.</summary>
        public bool IsNoiseFiltered => noiseFilter != null && noiseFilter.IsNoiseFiltered;

        /// <summary>Noise floor threshold.</summary>
        public float NoiseFloor => noiseFilter != null ? noiseFilter.NoiseFloor : 0f;

        /// <summary>The AI's raw classification result this frame.</summary>
        public string LastClassification => _lastClassName;

        /// <summary>Raw confidence values per class from last inference.</summary>
        public float[] LastConfidences => _lastConfidences;

        /// <summary>Class labels loaded from file.</summary>
        public string[] ClassLabels => _classLabels;

        /// <summary>Whether the TFLite model loaded successfully.</summary>
        public bool IsModelLoaded => _interpreter != null;

        /// <summary>Human-readable description of the model's input tensor shape.</summary>
        public string ModelInputShape => _modelInputShapeStr;

        /// <summary>TFLite runtime version string.</summary>
        public string TFLiteVersion => _tfliteVersion;

        // ====================================================================
        // Internal State
        // ====================================================================

        private VoiceMovementState _currentState = VoiceMovementState.Idle;
        private float _confidence;
        private float _sustainDuration;
        private float _lastClassificationTime;
        private string _lastClassName = "None";
        private float[] _lastConfidences;
        private string[] _classLabels;

        // TFLite model
        private Interpreter _interpreter;
        private string _tfliteVersion = "N/A";
        private string _modelInputShapeStr = "N/A";
        private int _modelInputSize;    // Total float count for input tensor
        private int _modelOutputSize;   // Total float count for output tensor
        private float[] _inputBuffer;   // Flattened input for TFLite (raw waveform or spectrogram)
        private float[] _outputBuffer;  // Model output probabilities
        private bool _useRawWaveform;   // True if model expects [1, N] raw PCM, false for [1, T, M, 1] spectrogram

        // Audio buffer for spectrogram / raw waveform
        private float[] _audioSampleBuffer;

        // Classification smoothing
        private Queue<VoiceMovementState> _stateHistory;
        private Queue<float> _confidenceHistory;

        // Idle timeout
        private float _timeSinceLastActive;
        private VoiceMovementState _latentState = VoiceMovementState.Idle;

        // Spectrogram computation
        private AudioSpectrogram _spectrogram;

        // ====================================================================
        // Label-to-State Mapping
        // ====================================================================

        // Maps from your labels.txt:
        //   0 = "Background Noise" → Idle
        //   1 = "RUN"              → Run
        //   2 = "Walk"             → Walk
        private static readonly Dictionary<string, VoiceMovementState> LabelToState =
            new Dictionary<string, VoiceMovementState>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Background Noise", VoiceMovementState.Idle },
            { "Background", VoiceMovementState.Idle },
            { "RUN", VoiceMovementState.Run },
            { "Walk", VoiceMovementState.Walk },
        };

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
            if (micInput == null || !micInput.IsRecording) return;

            // Feed noise filter
            if (noiseFilter != null)
            {
                noiseFilter.ProcessLoudness(micInput.RawLoudness);
            }

            // Track sustain duration
            if (noiseFilter != null && noiseFilter.IsAboveThreshold)
            {
                _sustainDuration += Time.deltaTime;
            }
            else
            {
                _sustainDuration = Mathf.Max(0f, _sustainDuration - Time.deltaTime * 5f);
            }

            // Run classification at interval
            if (Time.time - _lastClassificationTime >= classificationInterval)
            {
                _lastClassificationTime = Time.time;
                RunClassification();
            }
        }

        // ====================================================================
        // IVoiceDetectionProvider Lifecycle
        // ====================================================================

        public void Initialize()
        {
            ResolveDependencies();
            LoadLabels();
            LoadTFLiteModel();

            // Initialize based on model input type
            if (_useRawWaveform)
            {
                // Model has internal preprocessing — feed raw PCM directly
                // Audio buffer sized to model input (e.g. 44032 samples)
                _audioSampleBuffer = new float[_modelInputSize];
                Debug.Log($"[TeachableMachineProvider] Raw waveform mode: buffer = {_modelInputSize} samples");
            }
            else
            {
                // Model expects spectrogram — compute mel spectrogram from audio
                _spectrogram = new AudioSpectrogram(fftSize, hopLength, melBins, timeFrames, sampleRate);
                int requiredSamples = (timeFrames - 1) * hopLength + fftSize;
                _audioSampleBuffer = new float[Mathf.Max(requiredSamples, sampleRate)];
                Debug.Log($"[TeachableMachineProvider] Spectrogram mode: {timeFrames}×{melBins}");
            }

            // Initialize smoothing
            _stateHistory = new Queue<VoiceMovementState>();
            _confidenceHistory = new Queue<float>();

            if (micInput != null && !micInput.IsRecording)
            {
                micInput.StartRecording();
            }

            Debug.Log("[TeachableMachineProvider] Initialized. " +
                $"Labels: {(_classLabels != null ? string.Join(", ", _classLabels) : "None")}, " +
                $"Model: {(IsModelLoaded ? "LOADED" : "NOT LOADED")}");
        }

        public void Shutdown()
        {
            _currentState = VoiceMovementState.Idle;
            _confidence = 0f;

            if (_interpreter != null)
            {
                _interpreter.Dispose();
                _interpreter = null;
                Debug.Log("[TeachableMachineProvider] TFLite interpreter disposed.");
            }

            Debug.Log("[TeachableMachineProvider] Shutdown.");
        }

        // ====================================================================
        // TFLite Model Loading
        // ====================================================================

        /// <summary>
        /// Loads the TFLite model from the project Assets folder.
        /// Inspects input/output tensor shapes and adapts spectrogram params.
        /// </summary>
        private void LoadTFLiteModel()
        {
            try
            {
                // Try to get TFLite version
                _tfliteVersion = Interpreter.GetVersion();
                Debug.Log($"[TeachableMachineProvider] TFLite version: {_tfliteVersion}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TeachableMachineProvider] Could not get TFLite version: {e.Message}");
            }

            // Resolve model file path
            byte[] modelData = LoadModelBytes();
            if (modelData == null || modelData.Length == 0)
            {
                Debug.LogError("[TeachableMachineProvider] Failed to load TFLite model data! " +
                    "Classification will use fallback heuristics.");
                return;
            }

            try
            {
                // Create interpreter
                var options = new InterpreterOptions();
                options.threads = 2; // Use 2 threads for inference
                _interpreter = new Interpreter(modelData, options);
                _interpreter.AllocateTensors();

                // ── Inspect input tensor ────────────────────────────────────
                var inputInfo = _interpreter.GetInputTensorInfo(0);
                Debug.Log($"[TeachableMachineProvider] Input tensor: {inputInfo}");

                int[] inputShape = inputInfo.shape;
                _modelInputShapeStr = "[" + string.Join(", ", inputShape) + "]";

                // Auto-detect spectrogram dimensions from model
                // Teachable Machine typically uses [1, timeFrames, melBins, 1]
                if (inputShape.Length == 4)
                {
                    int detectedTimeFrames = inputShape[1];
                    int detectedMelBins = inputShape[2];

                    if (detectedTimeFrames != timeFrames || detectedMelBins != melBins)
                    {
                        Debug.Log($"[TeachableMachineProvider] Auto-adapting spectrogram: " +
                            $"{timeFrames}×{melBins} → {detectedTimeFrames}×{detectedMelBins}");
                        timeFrames = detectedTimeFrames;
                        melBins = detectedMelBins;
                    }
                }
                else if (inputShape.Length == 2)
                {
                    // Model expects [1, N] raw waveform — has internal audio preprocessing
                    _useRawWaveform = true;
                    Debug.Log($"[TeachableMachineProvider] Model expects raw waveform input: " +
                        $"{inputShape[1]} samples ({(float)inputShape[1] / sampleRate:F3}s @ {sampleRate}Hz)");
                }

                // Calculate input buffer size
                _modelInputSize = 1;
                foreach (int dim in inputShape) _modelInputSize *= dim;
                _inputBuffer = new float[_modelInputSize];

                // ── Inspect output tensor ───────────────────────────────────
                var outputInfo = _interpreter.GetOutputTensorInfo(0);
                Debug.Log($"[TeachableMachineProvider] Output tensor: {outputInfo}");

                int[] outputShape = outputInfo.shape;
                _modelOutputSize = 1;
                foreach (int dim in outputShape) _modelOutputSize *= dim;
                _outputBuffer = new float[_modelOutputSize];
                _lastConfidences = new float[_modelOutputSize];

                Debug.Log($"[TeachableMachineProvider] ✅ TFLite model loaded successfully! " +
                    $"Input: {_modelInputShapeStr}, Output classes: {_modelOutputSize}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TeachableMachineProvider] Failed to create TFLite interpreter: {e.Message}\n{e.StackTrace}");
                _interpreter = null;
            }
        }

        /// <summary>
        /// Loads the .tflite model file as a byte array.
        /// Searches in Assets/{modelSubFolder}/ and StreamingAssets/{modelSubFolder}/.
        /// </summary>
        private byte[] LoadModelBytes()
        {
            // Try Assets path first (works in Editor)
            string assetPath = Path.Combine(Application.dataPath, modelSubFolder, tfliteModelFileName);
            if (File.Exists(assetPath))
            {
                Debug.Log($"[TeachableMachineProvider] Loading model from: {assetPath}");
                return File.ReadAllBytes(assetPath);
            }

            // Try StreamingAssets (works in builds)
            string streamingPath = Path.Combine(Application.streamingAssetsPath, modelSubFolder, tfliteModelFileName);
            if (File.Exists(streamingPath))
            {
                Debug.Log($"[TeachableMachineProvider] Loading model from: {streamingPath}");
                return File.ReadAllBytes(streamingPath);
            }

            // Try StreamingAssets root
            string streamingRoot = Path.Combine(Application.streamingAssetsPath, tfliteModelFileName);
            if (File.Exists(streamingRoot))
            {
                Debug.Log($"[TeachableMachineProvider] Loading model from: {streamingRoot}");
                return File.ReadAllBytes(streamingRoot);
            }

            Debug.LogError($"[TeachableMachineProvider] Model file not found! Searched:\n" +
                $"  1. {assetPath}\n" +
                $"  2. {streamingPath}\n" +
                $"  3. {streamingRoot}\n" +
                $"Place '{tfliteModelFileName}' in Assets/{modelSubFolder}/ or StreamingAssets/");

            return null;
        }

        // ====================================================================
        // Classification — REAL TFLite Inference
        // ====================================================================

        /// <summary>
        /// Runs one classification cycle using the TFLite model.
        /// Supports two input modes:
        ///   - Raw waveform [1, N]: feeds PCM samples directly (model has internal preprocessing)
        ///   - Spectrogram [1, T, M, 1]: computes mel spectrogram first
        /// Falls back to heuristic classification if model isn't loaded.
        /// </summary>
        private void RunClassification()
        {
            if (noiseFilter == null) return;

            // If model isn't loaded, use fallback heuristics
            if (_interpreter == null)
            {
                RunFallbackClassification();
                return;
            }

            // ── Step 1: Capture audio samples ───────────────────────────────
            if (micInput == null) return;

            int samplesWritten = micInput.GetLatestSamples(_audioSampleBuffer);
            if (samplesWritten == 0)
            {
                ApplyClassificationResult(VoiceMovementState.Idle, 0.5f, "No Audio");
                return;
            }

            // ── Step 2: Prepare input tensor ─────────────────────────────────
            if (_useRawWaveform)
            {
                // Model has internal audio preprocessing (audio_preproc_input)
                // Feed raw PCM samples directly — copy audio into input buffer
                int copyLen = Mathf.Min(samplesWritten, _inputBuffer.Length);
                Array.Copy(_audioSampleBuffer, 0, _inputBuffer, 0, copyLen);

                // Zero-pad if we got fewer samples than the model expects
                if (copyLen < _inputBuffer.Length)
                {
                    Array.Clear(_inputBuffer, copyLen, _inputBuffer.Length - copyLen);
                }
            }
            else
            {
                // Model expects mel spectrogram — compute it first
                float[] spectrogram = _spectrogram.Compute(_audioSampleBuffer);
                int copyLen = Mathf.Min(spectrogram.Length, _inputBuffer.Length);
                Array.Copy(spectrogram, 0, _inputBuffer, 0, copyLen);

                if (copyLen < _inputBuffer.Length)
                {
                    Array.Clear(_inputBuffer, copyLen, _inputBuffer.Length - copyLen);
                }
            }

            // ── Step 4: Run TFLite inference ────────────────────────────────
            try
            {
                _interpreter.SetInputTensorData(0, _inputBuffer);
                _interpreter.Invoke();
                _interpreter.GetOutputTensorData(0, _outputBuffer);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TeachableMachineProvider] Inference error: {e.Message}");
                RunFallbackClassification();
                return;
            }

            // ── Step 5: Interpret results ───────────────────────────────────
            // Find the class with highest probability
            int bestIndex = 0;
            float bestScore = _outputBuffer[0];

            for (int i = 1; i < _outputBuffer.Length; i++)
            {
                if (_outputBuffer[i] > bestScore)
                {
                    bestScore = _outputBuffer[i];
                    bestIndex = i;
                }
            }

            // Copy raw confidences for debug display
            Array.Copy(_outputBuffer, 0, _lastConfidences, 0,
                Mathf.Min(_outputBuffer.Length, _lastConfidences.Length));

            // Map index to class name and state
            string className = "Unknown";
            VoiceMovementState detectedState = VoiceMovementState.Idle;

            if (_classLabels != null && bestIndex < _classLabels.Length)
            {
                className = _classLabels[bestIndex];

                if (LabelToState.TryGetValue(className, out var mappedState))
                {
                    detectedState = mappedState;
                }
                else
                {
                    // Try partial match
                    string lower = className.ToLowerInvariant();
                    if (lower.Contains("walk"))
                        detectedState = VoiceMovementState.Walk;
                    else if (lower.Contains("run"))
                        detectedState = VoiceMovementState.Run;
                    else
                        detectedState = VoiceMovementState.Idle;
                }
            }
            else
            {
                // No labels — use index mapping directly
                // Default: 0=Background, 1=Run, 2=Walk (Teachable Machine order)
                detectedState = bestIndex switch
                {
                    1 => VoiceMovementState.Run,
                    2 => VoiceMovementState.Walk,
                    _ => VoiceMovementState.Idle
                };
                className = detectedState.ToString();
            }

            ApplyClassificationResult(detectedState, bestScore, className);
        }

        // ====================================================================
        // Fallback Classification (when model isn't loaded)
        // ====================================================================

        /// <summary>
        /// Heuristic-based fallback when the TFLite model can't be loaded.
        /// Uses loudness and sustain patterns as a simple classifier.
        /// </summary>
        private void RunFallbackClassification()
        {
            if (noiseFilter == null) return;

            float filteredLoudness = noiseFilter.FilteredLoudness;
            bool isAboveThreshold = noiseFilter.IsAboveThreshold;

            if (!isAboveThreshold || filteredLoudness < 0.01f)
            {
                ApplyClassificationResult(VoiceMovementState.Idle, 0.9f, "Background Noise");
                return;
            }

            float energy = filteredLoudness;
            float rawLoudness = micInput != null ? micInput.RawLoudness : 0f;
            float energyDelta = Mathf.Abs(rawLoudness - filteredLoudness);

            bool isSustained = _sustainDuration > 0.15f;
            bool hasHighEnergy = energy > 0.05f;
            bool hasFluctuation = energyDelta > 0.02f;

            VoiceMovementState detectedState;
            float detectedConfidence;
            string detectedClass;

            if (hasHighEnergy && isSustained && !hasFluctuation)
            {
                detectedState = VoiceMovementState.Walk;
                detectedConfidence = Mathf.Clamp01(0.6f + _sustainDuration * 0.3f);
                detectedClass = "Walk (fallback)";
            }
            else if (hasHighEnergy && hasFluctuation)
            {
                detectedState = VoiceMovementState.Run;
                detectedConfidence = Mathf.Clamp01(0.6f + energyDelta * 3f);
                detectedClass = "RUN (fallback)";
            }
            else if (hasHighEnergy)
            {
                detectedState = VoiceMovementState.Walk;
                detectedConfidence = 0.5f;
                detectedClass = "Walk (fallback)";
            }
            else
            {
                detectedState = VoiceMovementState.Idle;
                detectedConfidence = 0.8f;
                detectedClass = "Background (fallback)";
            }

            // Update fake confidence array for debug UI
            if (_lastConfidences == null || _lastConfidences.Length < 3)
                _lastConfidences = new float[3];

            _lastConfidences[0] = detectedState == VoiceMovementState.Idle ? detectedConfidence : 1f - detectedConfidence;
            _lastConfidences[1] = detectedState == VoiceMovementState.Run ? detectedConfidence : 0f;
            _lastConfidences[2] = detectedState == VoiceMovementState.Walk ? detectedConfidence : 0f;

            ApplyClassificationResult(detectedState, detectedConfidence, detectedClass);
        }

        // ====================================================================
        // Result Application (shared by TFLite and fallback)
        // ====================================================================

        /// <summary>
        /// Applies a classification result with smoothing and idle timeout.
        /// </summary>
        private void ApplyClassificationResult(VoiceMovementState state, float confidence, string className)
        {
            _lastClassName = className;

            // Add to smoothing window
            _stateHistory.Enqueue(state);
            _confidenceHistory.Enqueue(confidence);

            while (_stateHistory.Count > smoothingWindowSize)
            {
                _stateHistory.Dequeue();
                _confidenceHistory.Dequeue();
            }

            // Vote: pick the most common state in the window
            VoiceMovementState smoothedState = GetMajorityState();
            float avgConfidence = GetAverageConfidence();

            // Apply confidence threshold
            if (avgConfidence < minimumConfidence)
            {
                smoothedState = VoiceMovementState.Idle;
            }

            // Apply idle timeout
            if (smoothedState != VoiceMovementState.Idle)
            {
                _latentState = smoothedState;
                _currentState = smoothedState;
                _confidence = avgConfidence;
                _timeSinceLastActive = 0f;
            }
            else
            {
                _timeSinceLastActive += classificationInterval;

                if (_timeSinceLastActive < idleTimeout && _latentState != VoiceMovementState.Idle)
                {
                    _currentState = _latentState;
                    _confidence = Mathf.Lerp(_confidence, 0f, Time.deltaTime * 8f);
                }
                else
                {
                    _currentState = VoiceMovementState.Idle;
                    _latentState = VoiceMovementState.Idle;
                    _confidence = 0f;
                }
            }
        }

        // ====================================================================
        // Smoothing Helpers
        // ====================================================================

        private VoiceMovementState GetMajorityState()
        {
            int idleCount = 0, walkCount = 0, runCount = 0;

            foreach (var s in _stateHistory)
            {
                switch (s)
                {
                    case VoiceMovementState.Idle: idleCount++; break;
                    case VoiceMovementState.Walk: walkCount++; break;
                    case VoiceMovementState.Run: runCount++; break;
                }
            }

            if (runCount >= walkCount && runCount >= idleCount) return VoiceMovementState.Run;
            if (walkCount >= idleCount) return VoiceMovementState.Walk;
            return VoiceMovementState.Idle;
        }

        private float GetAverageConfidence()
        {
            if (_confidenceHistory.Count == 0) return 0f;

            float sum = 0f;
            foreach (var c in _confidenceHistory)
                sum += c;

            return sum / _confidenceHistory.Count;
        }

        // ====================================================================
        // Label Loading
        // ====================================================================

        private void LoadLabels()
        {
            if (labelsFile != null)
            {
                string[] lines = labelsFile.text.Split('\n');
                var labels = new List<string>();

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Format: "0 Background Noise" — skip the index number
                    int spaceIdx = trimmed.IndexOf(' ');
                    if (spaceIdx >= 0)
                    {
                        labels.Add(trimmed.Substring(spaceIdx + 1));
                    }
                    else
                    {
                        labels.Add(trimmed);
                    }
                }

                _classLabels = labels.ToArray();
                Debug.Log($"[TeachableMachineProvider] Loaded {_classLabels.Length} labels: " +
                    string.Join(", ", _classLabels));
            }
            else
            {
                _classLabels = new[] { "Background Noise", "RUN", "Walk" };
                Debug.LogWarning("[TeachableMachineProvider] No labels file assigned. " +
                    "Using default labels.");
            }
        }

        // ====================================================================
        // Dependency Resolution
        // ====================================================================

        private void ResolveDependencies()
        {
            if (micInput == null)
            {
                micInput = GetComponent<MicrophoneInput>();
                if (micInput == null) micInput = FindAnyObjectByType<MicrophoneInput>();
            }

            if (noiseFilter == null)
            {
                noiseFilter = GetComponent<NoiseFilter>();
                if (noiseFilter == null) noiseFilter = FindAnyObjectByType<NoiseFilter>();
            }

            if (micInput == null)
                Debug.LogError("[TeachableMachineProvider] MicrophoneInput not found!");
            if (noiseFilter == null)
                Debug.LogError("[TeachableMachineProvider] NoiseFilter not found!");
        }
    }
}
