// ============================================================================
// MicUIController.cs
// Live microphone debug UI — volume bar, loudness, speed, device status.
// Compatible with Unity 6000.0.72f1
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using HotShipHai.Voice;
using HotShipHai.Player;

namespace HotShipHai.UI
{
    public class MicUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MicrophoneInput micInput;
        [SerializeField] private PlayerVoiceMovement playerMovement;

        [Header("Volume Bar")]
        [Tooltip("Image component for the volume fill bar (set Image Type to Filled).")]
        [SerializeField] private Image volumeFillBar;

        [Tooltip("Optional background image for the volume bar.")]
        [SerializeField] private Image volumeBarBackground;

        [Header("Debug Text")]
        [SerializeField] private Text loudnessText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text micStatusText;

        [Header("Visual Settings")]
        [SerializeField] private Color silentColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color quietColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color mediumColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color loudColor = new Color(1f, 0.3f, 0.2f, 1f);

        [Header("Waveform Visualization (Optional)")]
        [Tooltip("Array of Image bars for a simple waveform display. Leave empty to skip.")]
        [SerializeField] private RectTransform[] waveformBars;
        [SerializeField] private float waveformMaxHeight = 60f;
        [SerializeField] private float waveformAnimSpeed = 8f;

        private float[] _waveformTargets;

        private void Start()
        {
            // Auto-find references if not assigned
            if (micInput == null)
                micInput = FindAnyObjectByType<MicrophoneInput>();

            if (playerMovement == null)
                playerMovement = FindAnyObjectByType<PlayerVoiceMovement>();

            if (micInput == null)
                Debug.LogWarning("[MicUIController] MicrophoneInput not found!");

            if (playerMovement == null)
                Debug.LogWarning("[MicUIController] PlayerVoiceMovement not found!");

            // Initialize waveform targets
            if (waveformBars != null && waveformBars.Length > 0)
                _waveformTargets = new float[waveformBars.Length];
        }

        private void Update()
        {
            UpdateVolumeBar();
            UpdateDebugText();
            UpdateWaveform();
        }

        private void UpdateVolumeBar()
        {
            if (volumeFillBar == null || micInput == null) return;

            float loudness = micInput.Loudness;
            volumeFillBar.fillAmount = loudness;

            // Color gradient based on loudness level
            Color barColor;
            if (loudness < 0.01f)
                barColor = silentColor;
            else if (loudness < 0.3f)
                barColor = Color.Lerp(quietColor, mediumColor, loudness / 0.3f);
            else if (loudness < 0.7f)
                barColor = Color.Lerp(mediumColor, loudColor, (loudness - 0.3f) / 0.4f);
            else
                barColor = loudColor;

            volumeFillBar.color = barColor;
        }

        private void UpdateDebugText()
        {
            if (micInput != null && loudnessText != null)
            {
                loudnessText.text = $"Loudness: {micInput.Loudness:F3} " +
                    $"(Raw: {micInput.RawLoudness:F3})";
            }

            if (playerMovement != null && speedText != null)
            {
                speedText.text = $"Speed: {playerMovement.CurrentSpeed:F2} / " +
                    $"Target: {playerMovement.TargetSpeed:F2}";
            }

            if (micInput != null && micStatusText != null)
            {
                string status = micInput.IsDeviceReady
                    ? $"<color=#55FF55>MIC ACTIVE</color>: {micInput.DeviceName}"
                    : "<color=#FF5555>MIC NOT DETECTED</color>";
                micStatusText.text = status;
                micStatusText.supportRichText = true;
            }
        }

        private void UpdateWaveform()
        {
            if (waveformBars == null || waveformBars.Length == 0 || micInput == null)
                return;

            float loudness = micInput.Loudness;

            for (int i = 0; i < waveformBars.Length; i++)
            {
                if (waveformBars[i] == null) continue;

                // Create varied heights using sine offsets for visual interest
                float offset = (float)i / waveformBars.Length * Mathf.PI * 2f;
                float wave = Mathf.Sin(Time.time * 5f + offset) * 0.3f + 0.7f;
                float targetH = loudness * waveformMaxHeight * wave;

                // Ensure minimum visible height when there IS sound
                if (loudness > 0.01f)
                    targetH = Mathf.Max(targetH, 4f);

                _waveformTargets[i] = Mathf.Lerp(
                    _waveformTargets[i],
                    targetH,
                    Time.deltaTime * waveformAnimSpeed
                );

                Vector2 size = waveformBars[i].sizeDelta;
                size.y = _waveformTargets[i];
                waveformBars[i].sizeDelta = size;
            }
        }
    }
}
