using UnityEngine;

public class Burning : MonoBehaviour
{
    [Header("Visual Effects")]
    [Tooltip("Assign the Particle System you want to play when the player is in the sunlight.")]
    public ParticleSystem burningVFX;

    [Header("Audio Effects")]
    [Tooltip("Assign an AudioSource component here to play the burning sound. Make sure 'Loop' is checked on the AudioSource!")]
    public AudioSource burningAudioSource;

    private bool isCurrentlyBurning = false;

    // This method is called by PlayerHealth.cs to turn the VFX on and off
    public void SetBurningState(bool state)
    {
        // Don't do anything if the state hasn't changed
        if (state == isCurrentlyBurning) return;

        isCurrentlyBurning = state;

        if (burningVFX != null)
        {
            if (state)
            {
                // Start the fire particles when entering sunlight
                burningVFX.Play();
            }
            else
            {
                // Stop the fire particles when taking cover
                burningVFX.Stop();
            }
        }

        // Handle Audio
        if (burningAudioSource != null)
        {
            if (state && !burningAudioSource.isPlaying)
            {
                burningAudioSource.Play();
            }
            else if (!state && burningAudioSource.isPlaying)
            {
                burningAudioSource.Stop();
            }
        }
    }
}
