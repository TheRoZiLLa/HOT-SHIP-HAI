using UnityEngine;

namespace Cutscene
{
    /// <summary>
    /// A simple script to play audio during cutscenes, designed to be triggered via Animation Events.
    /// Attach this to the GameObject that has the Animator, or reference it in your animation events.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CutsceneAudioPlayer : MonoBehaviour
    {
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Plays the AudioClip currently assigned to the AudioSource.
        /// Useful for a simple Animation Event trigger.
        /// </summary>
        public void PlayDefaultAudio()
        {
            if (_audioSource.clip != null)
            {
                _audioSource.Play();
            }
            else
            {
                Debug.LogWarning("CutsceneAudioPlayer: Try to play default audio, but no clip is assigned to the AudioSource.", gameObject);
            }
        }

        /// <summary>
        /// Plays a specific AudioClip. You can assign the clip directly in the Animation Event.
        /// </summary>
        /// <param name="clipToPlay">The audio clip to play.</param>
        public void PlaySpecificAudio(AudioClip clipToPlay)
        {
            if (clipToPlay != null)
            {
                _audioSource.PlayOneShot(clipToPlay);
            }
            else
            {
                Debug.LogWarning("CutsceneAudioPlayer: Try to play specific audio, but the provided clip is null.", gameObject);
            }
        }

        /// <summary>
        /// Stops the currently playing audio on this AudioSource.
        /// </summary>
        public void StopAudio()
        {
            _audioSource.Stop();
        }
    }
}
