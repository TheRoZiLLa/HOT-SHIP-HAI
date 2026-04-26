// ============================================================================
// VoiceDebugUI.cs
// IMGUI-based debug overlay for the voice detection pipeline.
// No Canvas setup required — just add this component to any GameObject.
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
    /// Uses Unity's Immediate Mode GUI (IMGUI) for zero-setup convenience.
    /// <para>Press <b>F1</b> to toggle visibility.</para>
    /// </summary>
    public class VoiceDebugUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector Settings
        // ====================================================================

        [Header("References (auto-resolved if empty)")]
        [SerializeField] private LoudnessDetectionProvider provider;
        [SerializeField] private PlayerVoiceMovement playerMovement;
        [SerializeField] private MicrophoneInput micInput;

        [Header("Display Settings")]
        [Tooltip("Key to toggle the debug panel.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Show the debug panel at startup.")]
        [SerializeField] private bool showOnStart = true;

        // ====================================================================
        // Internal State
        // ====================================================================

        private bool _isVisible;
        private Rect _windowRect = new Rect(10, 10, 340, 420);

        // Style caching (created once in OnGUI)
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
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitStyles();

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindow,
                "🎤 Voice Debug Panel  [F1 to hide]",
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

            GUILayout.Space(6);

            // ── Loudness ────────────────────────────────────────────────────
            GUILayout.Label("LOUDNESS", _headerStyle);

            float rawLoudness = provider != null ? provider.RawLoudness : 0f;
            float filteredLoudness = provider != null ? provider.Loudness : 0f;
            bool noiseFiltered = provider != null && provider.IsNoiseFiltered;

            DrawRow("Raw:", $"{rawLoudness:F4}");
            DrawRow("Filtered:", $"{filteredLoudness:F4}");
            DrawLoudnessBar(filteredLoudness);
            DrawRow("Noise Gate:", noiseFiltered
                ? "<color=#FFAA33>FILTERED</color>"
                : "<color=#AAAAAA>Pass</color>");

            if (provider != null)
            {
                DrawRow("Noise Floor:", $"{provider.NoiseFloor:F3}");
            }

            GUILayout.Space(6);

            // ── Voice Pattern ───────────────────────────────────────────────
            GUILayout.Label("PATTERN ANALYSIS", _headerStyle);

            VoiceMovementState state = provider != null
                ? provider.CurrentState
                : VoiceMovementState.Idle;

            float confidence = provider != null ? provider.Confidence : 0f;
            float sustain = provider != null ? provider.SustainDuration : 0f;
            int peaks = provider != null ? provider.PeakCount : 0;

            DrawStateLabel(state);
            DrawRow("Confidence:", $"{confidence:P0}");
            DrawRow("Sustain:", $"{sustain:F2}s");
            DrawRow("Peak Count:", $"{peaks}");

            GUILayout.Space(6);

            // ── Movement ────────────────────────────────────────────────────
            GUILayout.Label("MOVEMENT", _headerStyle);

            float currentSpeed = playerMovement != null ? playerMovement.CurrentSpeed : 0f;
            float targetSpeed = playerMovement != null ? playerMovement.TargetSpeed : 0f;

            DrawRow("Speed:", $"{currentSpeed:F2}");
            DrawRow("Target:", $"{targetSpeed:F2}");
            DrawRow("Moving:", playerMovement != null && playerMovement.IsMoving
                ? "<color=#55FF55>Yes</color>"
                : "<color=#AAAAAA>No</color>");

            GUILayout.Space(6);

            // ── Provider Info ───────────────────────────────────────────────
            GUILayout.Label("PROVIDER", _headerStyle);

            string providerName = "None";
            if (playerMovement != null && playerMovement.ActiveProvider != null)
            {
                providerName = playerMovement.ActiveProvider.GetType().Name;
            }
            DrawRow("Type:", providerName);

            GUILayout.Space(4);

            // Drag handle
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

        private void DrawLoudnessBar(float value)
        {
            Rect barRect = GUILayoutUtility.GetRect(280, 16);
            barRect.x += 4;
            barRect.width -= 8;

            // Background
            GUI.Box(barRect, GUIContent.none, _barBgStyle);

            // Fill
            Rect fillRect = barRect;
            fillRect.width *= Mathf.Clamp01(value);

            // Color gradient
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

            // Bar styles
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
            if (provider == null)
                provider = FindAnyObjectByType<LoudnessDetectionProvider>();

            if (playerMovement == null)
                playerMovement = FindAnyObjectByType<PlayerVoiceMovement>();

            if (micInput == null)
                micInput = FindAnyObjectByType<MicrophoneInput>();

            if (provider == null)
                Debug.LogWarning("[VoiceDebugUI] LoudnessDetectionProvider not found.");
            if (playerMovement == null)
                Debug.LogWarning("[VoiceDebugUI] PlayerVoiceMovement not found.");
        }
    }
}
