// ============================================================================
// VoiceDebugUI.cs
// IMGUI-based debug overlay for the voice detection pipeline.
// Supports both LoudnessDetectionProvider and TeachableMachineProvider.
// Toggle visibility with F1.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using HotShipHai.Voice;
using HotShipHai.Player;

namespace HotShipHai.UI
{
    /// <summary>
    /// Real-time debug overlay showing all voice detection metrics.
    /// Auto-detects whether the active provider is AI-backed or loudness-based.
    /// <para>Press <b>F1</b> to toggle visibility.</para>
    /// </summary>
    public class VoiceDebugUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("References (auto-resolved if empty)")]
        [SerializeField] private PlayerVoiceMovement playerMovement;
        [SerializeField] private MicrophoneInput micInput;

        [Header("Display Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool showOnStart = true;

        // ====================================================================
        // Internal State
        // ====================================================================

        private bool _isVisible;
        private Rect _windowRect = new Rect(10, 10, 340, 480);

        // Cached provider references
        private IVoiceDetectionProvider _activeProvider;
        private LoudnessDetectionProvider _loudnessProvider;
        private TeachableMachineProvider _aiProvider;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _stateIdleStyle;
        private GUIStyle _stateWalkStyle;
        private GUIStyle _stateRunStyle;
        private GUIStyle _barBgStyle;
        private GUIStyle _barFillStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private bool _stylesInitialized;

        // ====================================================================
        // MonoBehaviour Lifecycle
        // ====================================================================

        private void Start()
        {
            _isVisible = showOnStart;
            ResolveDependencies();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
            }

            // Re-resolve provider if it changed at runtime
            if (playerMovement != null)
            {
                _activeProvider = playerMovement.ActiveProvider;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;
            InitStyles();

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindow,
                "🎤 Voice Debug  [F1]",
                GUILayout.MinWidth(320)
            );
        }

        // ====================================================================
        // Window Drawing
        // ====================================================================

        private void DrawWindow(int windowId)
        {
            // ── Mic Status ──────────────────────────────────────────────────
            GUILayout.Label("MICROPHONE", _headerStyle);

            bool deviceReady = micInput != null && micInput.IsRecording;
            string deviceName = micInput != null ? micInput.ActiveDeviceName : "Not Found";

            DrawRow("Device:", deviceName);
            DrawRow("Status:", deviceReady
                ? "<color=#55FF55>● ACTIVE</color>"
                : "<color=#FF5555>● INACTIVE</color>");

            GUILayout.Space(4);

            // ── Loudness ────────────────────────────────────────────────────
            GUILayout.Label("LOUDNESS", _headerStyle);

            float rawLoudness = 0f;
            float filteredLoudness = 0f;
            bool noiseFiltered = false;
            float noiseFloor = 0f;

            if (_loudnessProvider != null)
            {
                rawLoudness = _loudnessProvider.RawLoudness;
                filteredLoudness = _loudnessProvider.Loudness;
                noiseFiltered = _loudnessProvider.IsNoiseFiltered;
                noiseFloor = _loudnessProvider.NoiseFloor;
            }
            else if (_aiProvider != null)
            {
                rawLoudness = _aiProvider.RawLoudness;
                filteredLoudness = _aiProvider.Loudness;
                noiseFiltered = _aiProvider.IsNoiseFiltered;
                noiseFloor = _aiProvider.NoiseFloor;
            }
            else if (micInput != null)
            {
                rawLoudness = micInput.RawLoudness;
            }

            DrawRow("Raw:", $"{rawLoudness:F4}");
            DrawRow("Filtered:", $"{filteredLoudness:F4}");
            DrawLoudnessBar(filteredLoudness);
            DrawRow("Noise Gate:", noiseFiltered
                ? "<color=#FFAA33>FILTERED</color>"
                : "<color=#AAAAAA>Pass</color>");
            DrawRow("Noise Floor:", $"{noiseFloor:F3}");

            GUILayout.Space(4);

            // ── Detection State ─────────────────────────────────────────────
            VoiceMovementState state = VoiceMovementState.Idle;
            float confidence = 0f;
            float sustain = 0f;
            int peaks = 0;

            if (_activeProvider != null)
            {
                state = _activeProvider.CurrentState;
                confidence = _activeProvider.Confidence;
                sustain = _activeProvider.SustainDuration;
                peaks = _activeProvider.PeakCount;
            }

            GUILayout.Label("DETECTION", _headerStyle);
            DrawStateLabel(state);
            DrawRow("Confidence:", $"{confidence:P0}");
            DrawRow("Sustain:", $"{sustain:F2}s");
            DrawRow("Peaks:", $"{peaks}");

            // ── AI Classification (if using TeachableMachineProvider) ────────
            if (_aiProvider != null)
            {
                GUILayout.Space(4);
                GUILayout.Label("AI CLASSIFICATION", _headerStyle);

                // TFLite model status
                DrawRow("Model:", _aiProvider.IsModelLoaded
                    ? "<color=#55FF55>● TFLite LOADED</color>"
                    : "<color=#FF5555>● FALLBACK (heuristic)</color>");
                DrawRow("TFLite:", _aiProvider.TFLiteVersion);
                DrawRow("Input:", _aiProvider.ModelInputShape);

                DrawRow("AI Result:", $"<color=#FFD700>{_aiProvider.LastClassification}</color>");

                // Show per-class confidence bars
                float[] confidences = _aiProvider.LastConfidences;
                string[] labels = _aiProvider.ClassLabels;
                if (confidences != null && labels != null)
                {
                    for (int i = 0; i < Mathf.Min(labels.Length, confidences.Length); i++)
                    {
                        DrawConfidenceRow(labels[i], confidences[i]);
                    }
                }
            }

            GUILayout.Space(4);

            // ── Movement ────────────────────────────────────────────────────
            GUILayout.Label("MOVEMENT", _headerStyle);

            float currentSpeed = playerMovement != null ? playerMovement.CurrentSpeed : 0f;
            float targetSpeed = playerMovement != null ? playerMovement.TargetSpeed : 0f;

            DrawRow("Speed:", $"{currentSpeed:F2}");
            DrawRow("Target:", $"{targetSpeed:F2}");
            DrawRow("Moving:", playerMovement != null && playerMovement.IsMoving
                ? "<color=#55FF55>Yes</color>"
                : "<color=#AAAAAA>No</color>");

            GUILayout.Space(4);

            // ── Provider ────────────────────────────────────────────────────
            GUILayout.Label("PROVIDER", _headerStyle);

            string providerName = "None";
            if (_activeProvider != null)
            {
                providerName = _activeProvider.GetType().Name;
                if (_aiProvider != null)
                    providerName = "<color=#FFD700>🤖 " + providerName + "</color>";
                else
                    providerName = "<color=#55AAFF>🎤 " + providerName + "</color>";
            }
            DrawRow("Type:", providerName);

            GUILayout.Space(2);
            GUI.DragWindow();
        }

        // ====================================================================
        // UI Helpers
        // ====================================================================

        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(100));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawConfidenceRow(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {label}:", _labelStyle, GUILayout.Width(130));

            // Mini bar
            Rect barRect = GUILayoutUtility.GetRect(120, 12);
            barRect.width = 120;
            GUI.Box(barRect, GUIContent.none, _barBgStyle);

            Rect fillRect = barRect;
            fillRect.width *= Mathf.Clamp01(value);
            Color prev = GUI.color;
            GUI.color = value > 0.5f ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
            GUI.Box(fillRect, GUIContent.none, _barFillStyle);
            GUI.color = prev;

            GUILayout.Label($"{value:P0}", _valueStyle, GUILayout.Width(45));
            GUILayout.EndHorizontal();
        }

        private void DrawLoudnessBar(float value)
        {
            Rect barRect = GUILayoutUtility.GetRect(280, 16);
            barRect.x += 4;
            barRect.width -= 8;

            GUI.Box(barRect, GUIContent.none, _barBgStyle);

            Rect fillRect = barRect;
            fillRect.width *= Mathf.Clamp01(value);

            Color fillColor;
            if (value < 0.3f)
                fillColor = Color.Lerp(new Color(0.2f, 0.8f, 0.4f), new Color(1f, 0.8f, 0.2f), value / 0.3f);
            else if (value < 0.7f)
                fillColor = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.3f, 0.2f), (value - 0.3f) / 0.4f);
            else
                fillColor = new Color(1f, 0.3f, 0.2f);

            Color prevColor = GUI.color;
            GUI.color = fillColor;
            GUI.Box(fillRect, GUIContent.none, _barFillStyle);
            GUI.color = prevColor;
        }

        private void DrawStateLabel(VoiceMovementState state)
        {
            GUIStyle style;
            string text;

            switch (state)
            {
                case VoiceMovementState.Walk:
                    style = _stateWalkStyle;
                    text = "▶  WALK";
                    break;
                case VoiceMovementState.Run:
                    style = _stateRunStyle;
                    text = "▶▶ RUN";
                    break;
                default:
                    style = _stateIdleStyle;
                    text = "■  IDLE";
                    break;
            }

            GUILayout.Label(text, style);
        }

        // ====================================================================
        // Style Initialization
        // ====================================================================

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                normal = { textColor = new Color(0.6f, 0.8f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                normal = { textColor = Color.white }
            };

            _stateIdleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };

            _stateWalkStyle = new GUIStyle(_stateIdleStyle)
            {
                normal = { textColor = new Color(0.3f, 0.9f, 0.4f) }
            };

            _stateRunStyle = new GUIStyle(_stateIdleStyle)
            {
                normal = { textColor = new Color(1f, 0.5f, 0.2f) }
            };

            _barBgStyle = new GUIStyle(GUI.skin.box);
            _barFillStyle = new GUIStyle();
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
            _barFillStyle.normal.background = whiteTex;
        }

        // ====================================================================
        // Dependency Resolution
        // ====================================================================

        private void ResolveDependencies()
        {
            if (playerMovement == null)
                playerMovement = FindAnyObjectByType<PlayerVoiceMovement>();

            if (micInput == null)
                micInput = FindAnyObjectByType<MicrophoneInput>();

            // Cache typed providers for accessing extra properties
            _loudnessProvider = FindAnyObjectByType<LoudnessDetectionProvider>();
            _aiProvider = FindAnyObjectByType<TeachableMachineProvider>();

            if (_activeProvider == null && playerMovement != null)
                _activeProvider = playerMovement.ActiveProvider;
        }
    }
}
