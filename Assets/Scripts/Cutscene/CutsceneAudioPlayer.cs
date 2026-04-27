using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Cutscene
{
    /// <summary>
    /// A script to handle cutscene events such as audio and image manipulation.
    /// Designed to be triggered via Animation Events.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CutsceneAudioPlayer : MonoBehaviour
    {
        [Header("Component References")]
        private AudioSource _audioSource;
        
        [Tooltip("The default UI Image to manipulate. (Optional if you use Direct methods)")]
        public Image targetImage;

        [Header("Transition Settings")]
        [Tooltip("Default duration for transitions (fades, scaling) if duration is 0 or not provided.")]
        public float defaultTransitionDuration = 1f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        // ==========================================
        // DEFAULT AUDIO CONTROLS
        // ==========================================

        public void PlayDefaultAudio()
        {
            if (_audioSource.clip != null) _audioSource.Play();
            else Debug.LogWarning("Cutscene: No default clip assigned to AudioSource.", gameObject);
        }

        public void PlaySpecificAudio(AudioClip clipToPlay)
        {
            if (clipToPlay != null) _audioSource.PlayOneShot(clipToPlay);
            else Debug.LogWarning("Cutscene: Provided clip is null.", gameObject);
        }

        public void StopAudio()
        {
            _audioSource.Stop();
        }

        public void FadeAudioIn(float duration)
        {
            StartCoroutine(FadeAudioRoutine(_audioSource, 0f, 1f, duration > 0 ? duration : defaultTransitionDuration));
        }

        public void FadeAudioOut(float duration)
        {
            StartCoroutine(FadeAudioRoutine(_audioSource, _audioSource.volume, 0f, duration > 0 ? duration : defaultTransitionDuration));
        }

        // ==========================================
        // DIRECT AUDIO CONTROLS (Via Object Reference)
        // ==========================================

        public void DirectPlayAudioSource(AudioSource source)
        {
            if (source != null) source.Play();
        }

        public void DirectStopAudioSource(AudioSource source)
        {
            if (source != null) source.Stop();
        }

        public void DirectFadeAudioIn(AudioSource source)
        {
            if (source != null) StartCoroutine(FadeAudioRoutine(source, 0f, 1f, defaultTransitionDuration));
        }

        public void DirectFadeAudioOut(AudioSource source)
        {
            if (source != null) StartCoroutine(FadeAudioRoutine(source, source.volume, 0f, defaultTransitionDuration));
        }

        private IEnumerator FadeAudioRoutine(AudioSource source, float startVol, float endVol, float duration)
        {
            if (source == null) yield break;

            float elapsed = 0f;
            source.volume = startVol;
            if (endVol > 0 && !source.isPlaying) source.Play();
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, endVol, elapsed / duration);
                yield return null;
            }
            
            source.volume = endVol;
            if (endVol == 0f) source.Stop();
        }

        // ==========================================
        // DEFAULT IMAGE CONTROLS
        // ==========================================

        /// <summary>
        /// Animation Events don't support bools. Pass 1 for true, 0 for false.
        /// </summary>
        public void SetImageVisible(int isVisible)
        {
            if (targetImage != null) targetImage.enabled = (isVisible != 0);
        }

        public void FadeImageIn(float duration)
        {
            if (targetImage != null) 
            {
                targetImage.enabled = true;
                StartCoroutine(FadeImageRoutine(targetImage, 0f, 1f, duration > 0 ? duration : defaultTransitionDuration));
            }
        }

        public void FadeImageOut(float duration)
        {
            if (targetImage != null) 
            {
                StartCoroutine(FadeImageRoutine(targetImage, targetImage.color.a, 0f, duration > 0 ? duration : defaultTransitionDuration));
            }
        }

        public void SetImageScale(float scale)
        {
            if (targetImage != null) targetImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SmoothScaleImage(float targetScale)
        {
            if (targetImage != null) StartCoroutine(SmoothScaleRoutine(targetImage, targetScale, defaultTransitionDuration));
        }

        // ==========================================
        // DIRECT IMAGE CONTROLS (Via Object Reference)
        // ==========================================

        public void DirectSetImageVisible(Image img)
        {
            if (img != null) img.enabled = true;
        }

        public void DirectSetImageInvisible(Image img)
        {
            if (img != null) img.enabled = false;
        }

        public void DirectFadeImageIn(Image img)
        {
            if (img != null) 
            {
                img.enabled = true;
                StartCoroutine(FadeImageRoutine(img, 0f, 1f, defaultTransitionDuration));
            }
        }

        public void DirectFadeImageOut(Image img)
        {
            if (img != null) 
            {
                StartCoroutine(FadeImageRoutine(img, img.color.a, 0f, defaultTransitionDuration));
            }
        }

        private IEnumerator FadeImageRoutine(Image img, float startAlpha, float endAlpha, float duration)
        {
            if (img == null) yield break;

            float elapsed = 0f;
            Color c = img.color;
            c.a = startAlpha;
            img.color = c;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                img.color = c;
                yield return null;
            }
            
            c.a = endAlpha;
            img.color = c;
            
            if (endAlpha == 0f) img.enabled = false;
        }

        private IEnumerator SmoothScaleRoutine(Image img, float targetScale, float duration)
        {
            if (img == null) yield break;

            float elapsed = 0f;
            Vector3 startScale = img.rectTransform.localScale;
            Vector3 endScale = new Vector3(targetScale, targetScale, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                img.rectTransform.localScale = Vector3.Lerp(startScale, endScale, elapsed / duration);
                yield return null;
            }

            img.rectTransform.localScale = endScale;
        }
    }
}
